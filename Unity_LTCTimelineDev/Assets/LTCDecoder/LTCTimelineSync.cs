using System;
using UnityEngine;
using UnityEngine.Playables;
using LTC.Timeline;

/// <summary>
/// LTC信号とUnity Timelineを同期するシンプルなコンポーネント
/// LTCDecoderのInternal TCに追従してTimelineを制御
/// </summary>
[AddComponentMenu("Audio/LTC Timeline Sync")]
[RequireComponent(typeof(PlayableDirector))]
public class LTCTimelineSync : MonoBehaviour
{
    [Header("LTC Source")]
    [SerializeField] private LTCDecoder ltcDecoder;
    
    [Header("Sync Settings")]
    [Tooltip("時間差がこの値を超えたらTimelineをジャンプさせる（秒）")]
    [SerializeField] private float syncThreshold = 0.1f;
    
    [Tooltip("LTC信号がない時にTimelineを停止する")]
    [SerializeField] private bool pauseWhenNoSignal = true;
    
    [Tooltip("同期を有効にする")]
    [SerializeField] private bool enableSync = true;
    
    [Header("Status")]
    [SerializeField] private bool isPlaying = false;
    [SerializeField] private float currentTimeDifference = 0f;
    [SerializeField] private float timelineTime = 0f;
    [SerializeField] private float ltcTime = 0f;
    [SerializeField] private string currentLTC = "00:00:00:00";
    
    [Header("Debug")]
    [SerializeField] private bool enableDebugLog = false;
    
    // Private fields
    private PlayableDirector playableDirector;
    private float lastSyncTime = 0f;
    private bool wasPlaying = false;
    
    // Properties
    public bool IsPlaying => isPlaying;
    public float TimeDifference => currentTimeDifference;
    public bool EnableSync 
    { 
        get => enableSync; 
        set => enableSync = value; 
    }
    
    #region Unity Lifecycle
    
    private void Awake()
    {
        playableDirector = GetComponent<PlayableDirector>();
        
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
            playableDirector.timeUpdateMode = DirectorUpdateMode.GameTime;
        }
    }
    
    private void Update()
    {
        // 同期処理
        if (enableSync && ltcDecoder != null && playableDirector != null)
        {
            ProcessSync();
        }
    }
    
    #endregion
    
    #region Sync Logic
    
    /// <summary>
    /// メインの同期処理
    /// </summary>
    private void ProcessSync()
    {
        // LTC信号の状態を確認
        bool hasSignal = ltcDecoder.HasSignal && ltcDecoder.IsRecording;
        
        if (!hasSignal)
        {
            // 信号なし → Timeline停止
            if (pauseWhenNoSignal && playableDirector.state == PlayState.Playing)
            {
                playableDirector.Pause();
                isPlaying = false;
                LogDebug("No LTC signal - Timeline paused");
            }
            return;
        }
        
        // Internal TCを取得（これが最終的な同期対象）
        string ltcTimecode = ltcDecoder.CurrentTimecode;
        if (string.IsNullOrEmpty(ltcTimecode))
        {
            return;
        }
        
        currentLTC = ltcTimecode;
        
        // タイムコードを秒に変換
        ltcTime = ParseTimecodeToSeconds(ltcTimecode);
        if (ltcTime < 0)
        {
            return;
        }
        
        // Timeline側の現在時刻
        timelineTime = (float)playableDirector.time;
        
        // 時間差を計算
        currentTimeDifference = Mathf.Abs(ltcTime - timelineTime);
        
        // 閾値を超えたらジャンプ
        if (currentTimeDifference > syncThreshold)
        {
            playableDirector.time = ltcTime;
            playableDirector.Evaluate();
            lastSyncTime = Time.time;
            LogDebug($"Timeline jumped to LTC: {ltcTimecode} ({ltcTime:F3}s), diff was {currentTimeDifference:F3}s");
        }
        
        // 再生状態の管理
        if (playableDirector.state != PlayState.Playing)
        {
            playableDirector.Play();
            isPlaying = true;
            wasPlaying = true;
            LogDebug($"Timeline started at {ltcTimecode}");
        }
        else
        {
            isPlaying = true;
        }
    }
    
    /// <summary>
    /// タイムコード文字列を秒に変換
    /// </summary>
    private float ParseTimecodeToSeconds(string timecodeString)
    {
        if (string.IsNullOrEmpty(timecodeString))
            return -1f;
        
        string[] parts = timecodeString.Split(':');
        if (parts.Length != 4)
            return -1f;
        
        if (int.TryParse(parts[0], out int hours) &&
            int.TryParse(parts[1], out int minutes) &&
            int.TryParse(parts[2], out int seconds) &&
            int.TryParse(parts[3], out int frames))
        {
            // フレームレートは LTCDecoder側の設定を使用（通常30fps）
            float frameRate = 30f;
            if (ltcDecoder != null)
            {
                // LTCDecoderからフレームレート情報を取得できる場合はそれを使用
                // ※現在の実装では30fps固定
                frameRate = 30f;
            }
            
            float totalSeconds = hours * 3600f + 
                               minutes * 60f + 
                               seconds + 
                               (frames / frameRate);
            
            return totalSeconds;
        }
        
        return -1f;
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
        
        LogDebug("Timeline sync reset");
    }
    
    /// <summary>
    /// 指定タイムコードにシーク
    /// </summary>
    public void SeekToTimecode(string timecodeString)
    {
        float targetTime = ParseTimecodeToSeconds(timecodeString);
        if (targetTime >= 0 && playableDirector != null)
        {
            playableDirector.time = targetTime;
            playableDirector.Evaluate();
            LogDebug($"Timeline seeked to {timecodeString} ({targetTime:F3}s)");
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
        syncThreshold = Mathf.Max(0.001f, syncThreshold);
    }
    #endif
    
    #endregion
}