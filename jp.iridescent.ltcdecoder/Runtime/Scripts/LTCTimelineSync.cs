using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace jp.iridescent.ltcdecoder
{
    /// <summary>
/// LTC信号とUnity Timelineをシンプルに同期するコンポーネント
/// - LTC開始時：TimelineをOutputTCに合わせて再生開始
/// - LTC停止時：Timeline停止
/// - 継続的なドリフト検出時：即座に同期
/// </summary>
[AddComponentMenu("Audio/LTC Timeline Sync")]
public class LTCTimelineSync : MonoBehaviour
{
    [Header("LTC Source")]
    [SerializeField] private LTCDecoder ltcDecoder;
    
    [Tooltip("PlayableDirector to sync. Auto-searches from same GameObject if not set / 同期対象のPlayableDirector。未設定の場合は同じGameObjectから自動検索")]
    [SerializeField] private PlayableDirector playableDirector;
    
    [Header("Sync Settings")]
    [Tooltip("Sync when time difference exceeds this value continuously (seconds) / 時間差がこの値を超えた状態が継続したら同期（秒）")]
    [SerializeField, Range(0.1f, 2.0f)] private float syncThreshold = 0.5f;
    
    [Tooltip("Continuous observation time required for sync decision (seconds) / 同期判定に必要な連続観測時間（秒）")]
    [SerializeField, Range(0.1f, 5.0f)] private float continuousObservationTime = 1.0f;
    
    [Tooltip("Enable synchronization / 同期を有効にする")]
    [SerializeField] private bool enableSync = true;
    
    [Tooltip("Offset applied during Timeline sync (seconds) / Timeline同期時に適用するオフセット（秒）")]
    [SerializeField] private float timelineOffset = 0f;
    
    [Header("Drive Mode")]
    [Tooltip("Timeline drive mode / Timeline駆動モード")]
    [SerializeField] private DirectorUpdateMode updateMode = DirectorUpdateMode.DSPClock;
    
    [Header("Hard Resync Settings (Phase F)")]
    [Tooltip("Enable hard resync when LTC playback restarts / LTC再生再開時のハード同期を有効にする")]
    [SerializeField] private bool hardResyncOnLTCStart = true;
    
    [Tooltip("Use DSP gate scheduling for synchronized start (experimental) / 同期開始のためのDSPゲートスケジューリングを使用（実験的）")]
    [SerializeField] private bool useDspGateOnStart = false;
    
    [Tooltip("Snap to Timeline FPS boundaries / TimelineのFPS境界にスナップ")]
    [SerializeField] private bool snapToTimelineFps = false;
    
    [Header("Status")]
    [SerializeField] private bool isPlaying = false;
    [SerializeField] private float currentTimeDifference = 0f;
    [SerializeField] private float timelineTime = 0f;
    [SerializeField] private float ltcTime = 0f;
    [SerializeField] private string currentLTC = "00:00:00:00";
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = false;
    [SerializeField] private bool enableMeasurementLog = false;  // Phase F: 測定用ログ
    
    // Private fields
    private float driftStartTime = 0f;
    private bool isDrifting = false;
    private bool wasReceivingLTC = false;
    
    // Phase F-3: DSPゲートスケジューリング用
    private bool isScheduledForGate = false;
    private double scheduledGateDspTime = 0.0;
    private float scheduledTargetTime = 0f;
    private string scheduledTargetTC = "00:00:00:00";
    
    // Properties
    public bool IsPlaying => isPlaying;
    public float TimeDifference => currentTimeDifference;
    public bool EnableSync 
    { 
        get => enableSync; 
        set => enableSync = value; 
    }
    public float SyncThreshold
    {
        get => syncThreshold;
        set => syncThreshold = Mathf.Max(0.1f, value);
    }
    public float ContinuousObservationTime
    {
        get => continuousObservationTime;
        set => continuousObservationTime = Mathf.Max(0.1f, value);
    }
    public bool HardResyncOnLTCStart
    {
        get => hardResyncOnLTCStart;
        set => hardResyncOnLTCStart = value;
    }
    public bool UseDspGateOnStart
    {
        get => useDspGateOnStart;
        set => useDspGateOnStart = value;
    }
    public bool SnapToTimelineFps
    {
        get => snapToTimelineFps;
        set => snapToTimelineFps = value;
    }
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        // PlayableDirectorが未設定の場合の処理
        if (playableDirector == null)
        {
            // 同じGameObjectから検索
            playableDirector = GetComponent<PlayableDirector>();
            
            if (playableDirector == null)
            {
                // シーン内から検索（オプション）
                Debug.LogWarning($"[LTC Sync] PlayableDirector not assigned on {gameObject.name}. Please assign it manually in the Inspector.");
            }
            else
            {
                Debug.Log($"[LTC Sync] PlayableDirector found on the same GameObject: {gameObject.name}");
            }
        }
        
        // LTCDecoderが未設定の場合は自動検索
        if (ltcDecoder == null)
        {
            ltcDecoder = FindFirstObjectByType<LTCDecoder>();
            if (ltcDecoder == null)
            {
                Debug.LogError("[LTC Sync] LTC Decoder not found. Please assign it manually.");
            }
        }
    }
    
    private void OnEnable()
    {
        // 初期状態の設定
        if (playableDirector != null)
        {
            // 設定された駆動モードを適用
            playableDirector.timeUpdateMode = updateMode;
        }
    }
    
    private void Update()
    {
        // DSPゲートスケジューリングのチェック
        if (isScheduledForGate)
        {
            CheckAndExecuteScheduledGate();
        }
        
        // 同期処理
        if (enableSync && ltcDecoder != null && playableDirector != null)
        {
            ProcessSync();
        }
    }
    
    private void LateUpdate()
    {
        // DSPClockモードの場合、フレーム末尾で微調整
        if (enableSync && ltcDecoder != null && playableDirector != null && 
            updateMode == DirectorUpdateMode.DSPClock && isPlaying)
        {
            // 微小なドリフトを補正（1フレーム以内の誤差）
            float frameTime = 1f / ltcDecoder.GetActualFrameRate();
            if (currentTimeDifference > 0 && currentTimeDifference < frameTime)
            {
                // スムーズな補正を適用
                float correction = currentTimeDifference * 0.1f; // 10%ずつ補正
                playableDirector.time += correction;
            }
        }
    }
    
    #endregion
    
    #region Sync Logic
    
    /// <summary>
    /// メインの同期処理（シンプル版）
    /// </summary>
    private void ProcessSync()
    {
        // LTCの受信状態を確認
        bool isReceivingLTC = ltcDecoder.HasSignal && ltcDecoder.IsRecording;
        
        // LTC開始時の処理
        if (isReceivingLTC && !wasReceivingLTC)
        {
            // Phase F: ハード同期の実装
            if (hardResyncOnLTCStart)
            {
                // DecodedTimecodeを優先して使用（より正確なLTC時刻）
                string targetTC = !string.IsNullOrEmpty(ltcDecoder.DecodedTimecode) 
                    ? ltcDecoder.DecodedTimecode 
                    : ltcDecoder.CurrentTimecode;
                
                float targetTime = ParseTimecodeToSeconds(targetTC) + timelineOffset;
                
                if (targetTime >= 0)
                {
                    // TL-FPSスナップ（任意）
                    if (snapToTimelineFps)
                    {
                        float timelineFps = GetTimelineFps();
                        if (timelineFps > 0)
                        {
                            float dt = 1f / timelineFps;
                            targetTime = Mathf.Round(targetTime / dt) * dt;
                            LogDebug($"Snapped to Timeline FPS boundary: {targetTime:F3}s (FPS: {timelineFps})");
                        }
                    }
                    
                    // DSPゲートスケジューリング（Phase F-3）
                    if (useDspGateOnStart && updateMode == DirectorUpdateMode.DSPClock)
                    {
                        // DSPゲートスケジューリングを試みる
                        if (TryScheduleDspGate(targetTime, targetTC))
                        {
                            LogDebug($"Scheduled DSP gate sync at {scheduledGateDspTime:F3}s for TC {targetTC}");
                        }
                        else
                        {
                            // スケジューリング失敗時は通常のハード同期
                            PerformHardSync(targetTime, targetTC);
                        }
                    }
                    else
                    {
                        // 通常のハード同期
                        PerformHardSync(targetTime, targetTC);
                    }
                }
            }
            else
            {
                // 従来の動作（ハード同期なし - ただしフラッシュ防止のため順序は修正）
                // DecodedTimecodeを優先して使用（より正確なLTC時刻）
                string outputTC = !string.IsNullOrEmpty(ltcDecoder.DecodedTimecode) 
                    ? ltcDecoder.DecodedTimecode 
                    : ltcDecoder.CurrentTimecode;
                    
                float targetTime = ParseTimecodeToSeconds(outputTC) + timelineOffset;
                
                if (targetTime >= 0)
                {
                    // フラッシュ防止のため、time → Evaluate → Playの順序で実行
                    playableDirector.time = targetTime;
                    
                    // Evaluateを呼んで描画を更新
                    playableDirector.Evaluate();
                    
                    // その後Playを開始
                    playableDirector.Play();
                    isPlaying = true;
                    
                    // ドリフト観測状態もリセット
                    isDrifting = false;
                    driftStartTime = 0f;
                    
                    LogDebug($"LTC Started - Timeline synced to {outputTC} ({targetTime:F3}s) and playing");
                }
            }
        }
        
        // LTC停止時の処理
        else if (!isReceivingLTC && wasReceivingLTC)
        {
            // LTC停止 → Timeline停止
            playableDirector.Pause();
            isPlaying = false;
            isDrifting = false;  // ドリフト状態もリセット
            
            // DSPゲートスケジューリングもキャンセル
            if (isScheduledForGate)
            {
                isScheduledForGate = false;
                LogDebug("DSP Gate scheduling cancelled due to LTC stop");
            }
            
            LogDebug("LTC Stopped - Timeline paused");
        }
        
        // LTC受信中の継続的な同期チェック
        else if (isReceivingLTC)
        {
            string outputTC = ltcDecoder.CurrentTimecode;
            ltcTime = ParseTimecodeToSeconds(outputTC) + timelineOffset;
            
            if (ltcTime >= 0)
            {
                timelineTime = (float)playableDirector.time;
                currentTimeDifference = Mathf.Abs(ltcTime - timelineTime);
                currentLTC = outputTC;
                
                if (currentTimeDifference > syncThreshold)
                {
                    // 閾値を超えた差を検出
                    if (!isDrifting)
                    {
                        // ドリフト開始時刻を記録
                        isDrifting = true;
                        driftStartTime = Time.time;
                        LogDebug($"Drift detected: {currentTimeDifference:F3}s - starting observation");
                    }
                    else
                    {
                        // 連続観測時間をチェック
                        float driftDuration = Time.time - driftStartTime;
                        if (driftDuration >= continuousObservationTime)
                        {
                            // 指定時間以上ドリフトが継続 → 即座に同期
                            playableDirector.time = ltcTime;  // オフセット適用済みのltcTimeを使用
                            
                            // DSPClockモードの場合はEvaluateを呼ばない（自動更新されるため）
                            if (updateMode != DirectorUpdateMode.DSPClock)
                            {
                                playableDirector.Evaluate();
                            }
                            
                            LogDebug($"Drift persisted for {driftDuration:F1}s - Timeline jumped to {outputTC} ({ltcTime:F3}s with offset: {timelineOffset:F3}s)");
                            
                            // ドリフト状態をリセット
                            isDrifting = false;
                        }
                    }
                }
                else
                {
                    // 差が閾値以内に収まった
                    if (isDrifting)
                    {
                        LogDebug($"Drift resolved - difference now {currentTimeDifference:F3}s");
                        isDrifting = false;
                    }
                }
                
                // Timelineが再生中でない場合は再開
                if (playableDirector.state != PlayState.Playing)
                {
                    playableDirector.Play();
                    isPlaying = true;
                    LogDebug($"Timeline resumed at {outputTC}");
                }
            }
        }
        
        // 前フレームの状態を記録
        wasReceivingLTC = isReceivingLTC;
    }
    
    /// <summary>
    /// DSPゲートスケジューリングを試みる
    /// </summary>
    private bool TryScheduleDspGate(float targetTime, string targetTC)
    {
        // LTCDecoderからDSP情報を取得できるか確認
        double currentDspTime = AudioSettings.dspTime;
        
        // ゲート時刻を決定（次の整数秒または0.5秒単位）
        double gateInterval = 0.5; // 0.5秒間隔のゲート
        double nextGateTime = Math.Ceiling(targetTime / gateInterval) * gateInterval;
        
        // DSPゲート時刻を計算
        // 現在のDSP時刻からゲートまでの時間を加算
        double timeTillGate = nextGateTime - targetTime;
        
        // ゲートが近すぎる場合（100ms未満）はスケジューリングしない
        if (timeTillGate < 0.1)
        {
            return false;
        }
        
        // スケジューリング設定
        scheduledGateDspTime = currentDspTime + timeTillGate;
        scheduledTargetTime = (float)nextGateTime;
        scheduledTargetTC = targetTC;
        isScheduledForGate = true;
        
        // Timelineを一時停止（ゲートで開始）
        if (playableDirector.state == PlayState.Playing)
        {
            playableDirector.Pause();
        }
        
        LogDebug($"DSP Gate Scheduled: Target={nextGateTime:F3}s, DSP={scheduledGateDspTime:F3}, Wait={timeTillGate:F3}s");
        
        return true;
    }
    
    /// <summary>
    /// スケジュールされたDSPゲートのチェックと実行
    /// </summary>
    private void CheckAndExecuteScheduledGate()
    {
        if (!isScheduledForGate) return;
        
        double currentDspTime = AudioSettings.dspTime;
        
        // ゲート時刻に到達したかチェック
        if (currentDspTime >= scheduledGateDspTime)
        {
            // スケジュールされた同期を実行
            playableDirector.time = scheduledTargetTime;
            
            // DSPClockモード以外の場合のEvaluate
            if (updateMode != DirectorUpdateMode.DSPClock)
            {
                playableDirector.Evaluate();
            }
            
            playableDirector.Play();
            isPlaying = true;
            
            // ドリフト状態をリセット
            isDrifting = false;
            driftStartTime = 0f;
            
            // 測定用ログ
            if (enableMeasurementLog)
            {
                float actualTime = (float)playableDirector.time;
                float difference = Mathf.Abs(actualTime - scheduledTargetTime);
                float frameTime = 1f / ltcDecoder.GetActualFrameRate();
                float differenceInFrames = difference / frameTime;
                
                Debug.Log($"[LTC Sync Measurement] DSP Gate Sync Executed:\n" +
                         $"  Scheduled TC: {scheduledTargetTC}\n" +
                         $"  Scheduled Time: {scheduledTargetTime:F4}s\n" +
                         $"  Actual Time: {actualTime:F4}s\n" +
                         $"  Difference: {difference:F4}s ({differenceInFrames:F2} frames)\n" +
                         $"  DSP Gate Time: {scheduledGateDspTime:F4}\n" +
                         $"  Current DSP: {currentDspTime:F4}");
            }
            
            LogDebug($"DSP Gate Sync Executed - Timeline jumped to {scheduledTargetTC} ({scheduledTargetTime:F3}s)");
            
            // スケジューリング状態をクリア
            isScheduledForGate = false;
            scheduledGateDspTime = 0.0;
            scheduledTargetTime = 0f;
            scheduledTargetTC = "00:00:00:00";
        }
    }
    
    /// <summary>
    /// ハード同期の実行
    /// </summary>
    private void PerformHardSync(float targetTime, string targetTC)
    {
        // ハード同期: time設定 → Evaluate → Playの順番
        playableDirector.time = targetTime;
        
        // DSPClockモード以外の場合のEvaluate
        if (updateMode != DirectorUpdateMode.DSPClock)
        {
            playableDirector.Evaluate();
        }
        
        playableDirector.Play();
        isPlaying = true;
        
        // ドリフト状態を即座にリセット
        isDrifting = false;
        driftStartTime = 0f;
        
        // Phase F: 測定用ログ
        if (enableMeasurementLog)
        {
            float actualTime = (float)playableDirector.time;
            float difference = Mathf.Abs(actualTime - targetTime);
            float frameTime = 1f / ltcDecoder.GetActualFrameRate();
            float differenceInFrames = difference / frameTime;
            
            Debug.Log($"[LTC Sync Measurement] Hard Sync Executed:\n" +
                     $"  Target TC: {targetTC}\n" +
                     $"  Target Time: {targetTime:F4}s\n" +
                     $"  Actual Time: {actualTime:F4}s\n" +
                     $"  Difference: {difference:F4}s ({differenceInFrames:F2} frames)\n" +
                     $"  Update Mode: {updateMode}\n" +
                     $"  DSP Time: {AudioSettings.dspTime:F4}");
        }
        
        LogDebug($"Hard Sync - Timeline jumped to {targetTC} ({targetTime:F3}s with offset: {timelineOffset:F3}s)");
    }
    
    /// <summary>
    /// TimelineのFPSを取得
    /// </summary>
    private float GetTimelineFps()
    {
        if (playableDirector != null && playableDirector.playableAsset != null)
        {
            var timeline = playableDirector.playableAsset as TimelineAsset;
            if (timeline != null)
            {
                // TimelineAssetのEditorSettingsからFPSを取得
                // 注: EditorSettingsはEditorでのみアクセス可能なため、
                // Runtimeではデフォルト値を返す
                #if UNITY_EDITOR
                return (float)timeline.editorSettings.frameRate;
                #else
                // Runtimeではデフォルトの30fpsを使用
                // またはLTCDecoderのフレームレートを使用
                return ltcDecoder != null ? ltcDecoder.GetActualFrameRate() : 30f;
                #endif
            }
        }
        return 30f; // デフォルト値
    }
    
    /// <summary>
    /// タイムコード文字列を秒に変換（LTCDecoderのフレームレート設定を使用）
    /// </summary>
    private float ParseTimecodeToSeconds(string timecodeString)
    {
        if (string.IsNullOrEmpty(timecodeString))
            return -1f;
        
        if (ltcDecoder == null)
            return -1f;
            
        // LTCDecoderの変換ユーティリティを使用（DropFrame対応）
        long absoluteFrames = LTCDecoder.TimecodeToAbsoluteFrames(
            timecodeString, 
            ltcDecoder.IsDropFrame, 
            ltcDecoder.GetNominalFrameRate()
        );
        
        // 絶対フレーム数を実フレームレートで秒に変換
        float actualFrameRate = ltcDecoder.GetActualFrameRate();
        return absoluteFrames / actualFrameRate;
    }
    
    #endregion
    
    #region Public Methods
    
    /// <summary>
    /// LTCDecoderを設定
    /// </summary>
    public void SetLTCDecoder(LTCDecoder decoder)
    {
        ltcDecoder = decoder;
        LogDebug($"LTC Decoder set: {decoder != null}");
    }
    
    /// <summary>
    /// PlayableDirectorを設定
    /// </summary>
    public void SetPlayableDirector(PlayableDirector director)
    {
        playableDirector = director;
        if (director != null)
        {
            playableDirector.timeUpdateMode = updateMode;
        }
        LogDebug($"PlayableDirector set: {director != null}, UpdateMode: {updateMode}");
    }
    
    /// <summary>
    /// TimelineAssetを設定（PlayableDirectorも必要）
    /// </summary>
    public void SetTimeline(TimelineAsset timeline)
    {
        if (playableDirector == null)
        {
            LogDebug("Cannot set Timeline - PlayableDirector is not assigned");
            return;
        }
        
        playableDirector.playableAsset = timeline;
        LogDebug($"Timeline set: {(timeline != null ? timeline.name : "null")}");
    }
    
    /// <summary>
    /// TimelineAssetとPlayableDirectorを同時に設定
    /// </summary>
    public void SetTimelineAndDirector(TimelineAsset timeline, PlayableDirector director)
    {
        SetPlayableDirector(director);
        if (director != null && timeline != null)
        {
            director.playableAsset = timeline;
            LogDebug($"Timeline and Director set: Timeline={timeline.name}, Director={director.name}");
        }
    }
    
    /// <summary>
    /// 現在のTimelineAssetを取得
    /// </summary>
    public TimelineAsset GetTimeline()
    {
        if (playableDirector != null && playableDirector.playableAsset is TimelineAsset timeline)
        {
            return timeline;
        }
        return null;
    }
    
    /// <summary>
    /// 現在のPlayableDirectorを取得
    /// </summary>
    public PlayableDirector GetPlayableDirector()
    {
        return playableDirector;
    }
    
    /// <summary>
    /// TimelineのBindingsを設定
    /// </summary>
    public void SetBinding(string trackName, UnityEngine.Object bindingObject)
    {
        if (playableDirector == null || playableDirector.playableAsset == null)
        {
            LogDebug("Cannot set binding - PlayableDirector or Timeline is not assigned");
            return;
        }
        
        TimelineAsset timeline = playableDirector.playableAsset as TimelineAsset;
        if (timeline == null)
        {
            LogDebug("Cannot set binding - PlayableAsset is not a Timeline");
            return;
        }
        
        foreach (var track in timeline.GetOutputTracks())
        {
            if (track.name == trackName)
            {
                playableDirector.SetGenericBinding(track, bindingObject);
                LogDebug($"Binding set for track '{trackName}' to '{bindingObject.name}'");
                return;
            }
        }
        
        LogDebug($"Track '{trackName}' not found in Timeline");
    }
    
    /// <summary>
    /// タイムラインオフセットを設定
    /// </summary>
    public void SetTimelineOffset(float offset)
    {
        timelineOffset = offset;
        LogDebug($"Timeline offset set to {offset:F3}s");
    }
    
    /// <summary>
    /// タイムラインオフセットを取得
    /// </summary>
    public float GetTimelineOffset()
    {
        return timelineOffset;
    }
    
    /// <summary>
    /// 駆動モードを設定
    /// </summary>
    public void SetUpdateMode(DirectorUpdateMode mode)
    {
        updateMode = mode;
        if (playableDirector != null)
        {
            playableDirector.timeUpdateMode = mode;
        }
        LogDebug($"Update mode changed to: {mode}");
    }
    
    /// <summary>
    /// 駆動モードを取得
    /// </summary>
    public DirectorUpdateMode GetUpdateMode()
    {
        return updateMode;
    }
    
    /// <summary>
    /// 同期をリセット
    /// </summary>
    public void ResetSync()
    {
        if (playableDirector != null)
        {
            playableDirector.time = 0;
            playableDirector.Evaluate();
            playableDirector.Stop();
        }
        
        isPlaying = false;
        currentTimeDifference = 0f;
        timelineTime = 0f;
        ltcTime = 0f;
        currentLTC = "00:00:00:00";
        isDrifting = false;
        driftStartTime = 0f;
        
        // DSPゲートスケジューリングもリセット
        isScheduledForGate = false;
        scheduledGateDspTime = 0.0;
        scheduledTargetTime = 0f;
        scheduledTargetTC = "00:00:00:00";
        
        LogDebug("Timeline sync reset");
    }
    
    /// <summary>
    /// 指定タイムコードにシーク
    /// </summary>
    public void SeekToTimecode(string timecodeString)
    {
        if (playableDirector == null)
        {
            LogDebug("Cannot seek - PlayableDirector is not assigned");
            return;
        }
        
        float targetTime = ParseTimecodeToSeconds(timecodeString) + timelineOffset;
        if (targetTime >= 0)
        {
            // ドュレーション内にクランプ
            targetTime = Mathf.Clamp(targetTime, 0, (float)playableDirector.duration);
            
            playableDirector.time = targetTime;
            playableDirector.Evaluate();
            LogDebug($"Timeline seeked to {timecodeString} ({targetTime:F3}s with offset: {timelineOffset:F3}s)");
        }
    }
    
    /// <summary>
    /// 手動で再生開始
    /// </summary>
    public void Play()
    {
        if (playableDirector != null)
        {
            playableDirector.Play();
            isPlaying = true;
        }
        else
        {
            LogDebug("Cannot play - PlayableDirector is not assigned");
        }
    }
    
    /// <summary>
    /// 手動で一時停止
    /// </summary>
    public void Pause()
    {
        if (playableDirector != null)
        {
            playableDirector.Pause();
            isPlaying = false;
        }
        else
        {
            LogDebug("Cannot pause - PlayableDirector is not assigned");
        }
    }
    
    /// <summary>
    /// 手動で停止
    /// </summary>
    public void Stop()
    {
        if (playableDirector != null)
        {
            playableDirector.Stop();
            isPlaying = false;
        }
        else
        {
            LogDebug("Cannot stop - PlayableDirector is not assigned");
        }
    }
    
    #endregion
    
    #region Debug
    
    private void LogDebug(string message)
    {
        if (enableDebugLog)
        {
            Debug.Log($"[LTC Sync] {message}");
        }
    }
    
    #if UNITY_EDITOR
    private void OnValidate()
    {
        // 値の範囲チェック
        syncThreshold = Mathf.Max(0.1f, syncThreshold);
        continuousObservationTime = Mathf.Max(0.1f, continuousObservationTime);
    }
    #endif
    
    #endregion
    }
}