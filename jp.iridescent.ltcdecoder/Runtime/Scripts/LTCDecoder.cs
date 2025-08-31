using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace jp.iridescent.ltcdecoder
{
    /// <summary>
    /// DSPクロックベースのLTCデコーダー
    /// 出力タイムコード（Output TC）を生成し、デコードされたLTCと同期を取る
    /// </summary>
    [AddComponentMenu("Audio/LTC Decoder")]
    public class LTCDecoder : MonoBehaviour
    {
        #region Enums
        
        /// <summary>
        /// 同期状態
        /// </summary>
        public enum SyncState
        {
            NoSignal,    // 信号なし
            Syncing,     // 同期中（バッファ蓄積）
            Locked,      // 同期確立（DSP自走）
            Drifting     // ドリフト検出（補正中）
        }
        
        /// <summary>
        /// LTCフレームレート
        /// </summary>
        public enum LTCFrameRate
        {
            FPS_24 = 24,
            FPS_25 = 25,
            FPS_29_97_DF = 2997,  // Drop Frame (内部では29.97として処理)
            FPS_29_97_NDF = 2998, // Non-Drop Frame (内部では29.97として処理)
            FPS_30 = 30
        }
        
        #endregion
        
        #region Serialized Fields
        
        [Header("Audio Input Settings")]
        [SerializeField] private string selectedDevice = "";
        [SerializeField] private int sampleRate = 48000;
        [SerializeField] private int bufferSize = 2048;
        
        [Header("Sync Settings")]
        [SerializeField, Range(5, 30)] private int bufferQueueSize = 15;
        [SerializeField, Range(0.01f, 1.0f)] private float syncThreshold = 0.1f;
        [SerializeField, Range(0.5f, 5.0f)] private float jumpThreshold = 1.0f;
        [SerializeField, Range(0.0001f, 0.01f)] private float stopThreshold = 0.001f;
        [SerializeField, Range(0.01f, 1.0f)] private float driftCorrection = 0.1f;
        
        [Header("Signal Detection")]
        [SerializeField, Range(0.001f, 0.1f)] private float signalThreshold = 0.01f;
        
        [Header("Status")]
        [SerializeField] private SyncState currentState = SyncState.NoSignal;
        [SerializeField] private string currentTimecode = "00:00:00:00";
        [SerializeField] private string decodedTimecode = "00:00:00:00";
        [SerializeField] private bool hasSignal = false;
        [SerializeField] private float signalLevel = 0f;
        
        // 自動ゲイン調整（AGC）用
        private float signalLevelMax = 0.1f;  // 動的な最大値
        private const float AGC_DECAY = 0.995f;  // 減衰率
        private const float AGC_MIN_THRESHOLD = 0.01f;  // 最小閾値
        [SerializeField] private float timeDifference = 0f;
        
        [Header("LTC Settings")]
        [SerializeField] private LTCFrameRate ltcFrameRate = LTCFrameRate.FPS_30;
        [SerializeField] private bool useDropFrame = false;
        
        [Header("Time Offset")]
        [Tooltip("出力タイムコード（Output TC）に適用するオフセット（秒）")]
        [SerializeField] private float timeOffset = 0f;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = false;
        
        [Header("Events")]
        [Space(5)]
        [Tooltip("LTC受信開始の瞬間に発火")]
        public LTCUnityEvent OnLTCStarted = new LTCUnityEvent();
        
        [Tooltip("LTC受信停止の瞬間に発火")]
        public LTCUnityEvent OnLTCStopped = new LTCUnityEvent();
        
        [Tooltip("LTC受信中、毎フレーム発火")]
        public LTCUnityEvent OnLTCReceiving = new LTCUnityEvent();
        
        [Tooltip("LTC未受信中、毎フレーム発火")]
        public LTCUnityEvent OnLTCNoSignal = new LTCUnityEvent();
        
        [Header("Timecode Events")]
        [SerializeField] private List<TimecodeEvent> timecodeEvents = new List<TimecodeEvent>();
        [Tooltip("タイムコード巻き戻し時にイベントをリセット")]
        [SerializeField] private bool resetOnRewind = true;
        
        [Header("Advanced Drift Control")]
        [Tooltip("このサイズ以下のドリフトは完全に無視（ノイズとみなす）")]
        [SerializeField, Range(0.01f, 0.1f)] private float driftDeadzoneSmall = 0.03f;
        
        [Tooltip("このサイズ以下のドリフトは緩やかに補正")]
        [SerializeField, Range(0.05f, 0.3f)] private float driftDeadzoneMedium = 0.1f;
        
        [Tooltip("このサイズ以上のドリフトは即座に同期")]
        [SerializeField, Range(0.2f, 1.0f)] private float driftThresholdLarge = 0.3f;
        
        [Tooltip("小さなドリフトの補正率（0-1、小さいほどゆっくり）")]
        [SerializeField, Range(0.001f, 0.1f)] private float driftCorrectionSlow = 0.01f;
        
        [Tooltip("通常ドリフトの補正率（0-1、小さいほどゆっくり）")]
        [SerializeField, Range(0.01f, 0.5f)] private float driftCorrectionNormal = 0.1f;
        
        #endregion
        
        #region Private Fields
        
        // PlayerPrefs設定キー
        private const string PREF_DEVICE = "LTCDecoder.Device";
        private const string PREF_FRAMERATE = "LTCDecoder.FrameRate";
        private const string PREF_SAMPLERATE = "LTCDecoder.SampleRate";
        private const string PREF_DROPFRAME = "LTCDecoder.DropFrame";
        
        // 状態管理フラグ
        private bool isInitialized = false;
        private bool isDirtyFromRuntime = false;  // 実行時に変更されたか
        private string lastSavedDevice = "";
        private LTCFrameRate lastSavedFrameRate;
        private int lastSavedSampleRate;
        
        // DSPクロック管理
        private double dspTimeBase;
        private double internalTcTime;
        private bool isRunning;
        
        // オーディオ処理
        private AudioClip microphoneClip;
        private TimecodeDecoder decoder;
        private float[] audioBuffer;
        private int lastSamplePosition;
        private Coroutine audioProcessingCoroutine;
        
        // LTCバッファリング
        private struct LTCSample
        {
            public double dspTime;
            public string timecode;
            public double tcSeconds;
        }
        private Queue<LTCSample> ltcBuffer;
        
        // 統計情報
        private int consecutiveStops = 0;
        private string lastDecodedTc = "";
        private double lastSyncTime = 0;
        
        // ノイズ解析用データ (グラフ表示用)
        private const int NoiseHistorySize = 100;  // 100サンプル分の履歴
        private float[] ltcNoiseHistory;           // LTC側のノイズ度合い (0-1)
        private float[] internalNoiseHistory;      // Output TC側のノイズ度合い (0-1)
        private int noiseHistoryIndex = 0;
        private float lastLtcTime = 0f;
        private float lastInternalTime = 0f;
        private float lastNoiseUpdateTime = 0f;    // 最後のノイズ更新時刻
        private const float NoiseUpdateInterval = 0.1f;  // 100ms間隔で更新
        
        // イベント管理用
        private bool wasReceivingLTC = false;      // 前フレームでLTC受信していたか
        private bool isDecodingLTC = false;        // 現在LTCをデコード中か
        private float lastDecodedTime = 0f;        // 最後にデコード成功した時刻
        private const float decodeTimeoutSeconds = 0.5f; // デコードタイムアウト（秒）
        private string lastCheckedTimecode = "00:00:00:00"; // 最後にチェックしたタイムコード（巻き戻し検知用）
        private bool wasDecodingLTC = false;       // 前フレームでデコード中だったか
        
        // デバッガー参照（オプショナル）
        private LTCEventDebugger debugger;
        
        #endregion
        
        #region Properties
        
        public string CurrentTimecode
        {
            get
            {
                // Output TCにオフセットを適用
                return ApplyTimeOffset(currentTimecode);
            }
        }
        public string DecodedTimecode => decodedTimecode;
        public SyncState State => currentState;
        public bool HasSignal => hasSignal;
        public bool IsRunning => isRunning;  // 内部クロックが動作中かどうか
        public float SignalLevel => signalLevel;
        public float TimeDifference => timeDifference;
        public bool IsRecording => microphoneClip != null;
        public string SelectedDevice => selectedDevice;
        public string[] AvailableDevices => Microphone.devices;
        public bool DropFrame => useDropFrame;
        public LTCFrameRate FrameRate
        {
            get => ltcFrameRate;
            set => SetLTCFrameRate(value);
        }
        public int SampleRate
        {
            get => sampleRate;
            set => SetSampleRate(value);
        }
        
        // ノイズ解析データアクセス
        public float[] LTCNoiseHistory => ltcNoiseHistory;
        public float[] InternalNoiseHistory => internalNoiseHistory;
        public int NoiseHistoryCurrentIndex => noiseHistoryIndex;
        public int NoiseHistoryMaxSize => NoiseHistorySize;
        public float NoiseHistorySampleInterval => NoiseUpdateInterval;  // サンプル間隔（秒）
        
        #endregion
        
        #region Settings Persistence
        
        /// <summary>
        /// 設定を保存
        /// </summary>
        public void SaveSettings()
        {
            try
            {
                PlayerPrefs.SetString(PREF_DEVICE, selectedDevice);
                PlayerPrefs.SetInt(PREF_FRAMERATE, (int)ltcFrameRate);
                PlayerPrefs.SetInt(PREF_SAMPLERATE, sampleRate);
                PlayerPrefs.SetInt(PREF_DROPFRAME, useDropFrame ? 1 : 0);
                PlayerPrefs.Save();  // 即座に永続化
                
                // 最後に保存した値を記録
                lastSavedDevice = selectedDevice;
                lastSavedFrameRate = ltcFrameRate;
                lastSavedSampleRate = sampleRate;
                
                LogDebug($"Settings saved - Device: {selectedDevice}, FrameRate: {ltcFrameRate}, SampleRate: {sampleRate}");
                
                #if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);  // Inspectorを更新
                #endif
            }
            catch (System.Exception e)
            {
                UnityEngine.Debug.LogError($"[LTCDecoder] Failed to save settings: {e.Message}");
            }
        }
        
        /// <summary>
        /// 設定を読み込み
        /// </summary>
        private void LoadSettings()
        {
            if (PlayerPrefs.HasKey(PREF_DEVICE))
            {
                selectedDevice = PlayerPrefs.GetString(PREF_DEVICE, selectedDevice);
                ltcFrameRate = (LTCFrameRate)PlayerPrefs.GetInt(PREF_FRAMERATE, (int)ltcFrameRate);
                sampleRate = PlayerPrefs.GetInt(PREF_SAMPLERATE, sampleRate);
                useDropFrame = PlayerPrefs.GetInt(PREF_DROPFRAME, useDropFrame ? 1 : 0) == 1;
                
                // 設定の妥当性チェック
                ValidateAndFixSettings();
                
                // 最後に保存した値を記録
                lastSavedDevice = selectedDevice;
                lastSavedFrameRate = ltcFrameRate;
                lastSavedSampleRate = sampleRate;
                
                isDirtyFromRuntime = true;  // 保存された設定があることを記録
                
                LogDebug($"Settings loaded - Device: {selectedDevice}, FrameRate: {ltcFrameRate}, SampleRate: {sampleRate}");
            }
        }
        
        /// <summary>
        /// 設定の妥当性チェックと修正
        /// </summary>
        private void ValidateAndFixSettings()
        {
            // デバイスが存在するか確認
            if (!string.IsNullOrEmpty(selectedDevice))
            {
                if (!System.Linq.Enumerable.Contains(Microphone.devices, selectedDevice))
                {
                    // デバイスが見つからない場合
                    if (Microphone.devices.Length > 0)
                    {
                        string oldDevice = selectedDevice;
                        selectedDevice = Microphone.devices[0];
                        UnityEngine.Debug.LogWarning($"[LTCDecoder] Saved device '{oldDevice}' not found, using: {selectedDevice}");
                    }
                }
            }
            
            // サンプルレートの妥当性確認
            if (sampleRate != 44100 && sampleRate != 48000 && sampleRate != 96000)
            {
                sampleRate = 48000;
                UnityEngine.Debug.LogWarning($"[LTCDecoder] Invalid sample rate, reset to: {sampleRate}");
            }
        }
        
        /// <summary>
        /// Inspector値が変更されたかチェック
        /// </summary>
        private bool HasInspectorChanges()
        {
            return selectedDevice != lastSavedDevice ||
                   ltcFrameRate != lastSavedFrameRate ||
                   sampleRate != lastSavedSampleRate;
        }
        
        /// <summary>
        /// 設定をリセット（Editor専用）
        /// </summary>
        #if UNITY_EDITOR
        [ContextMenu("Reset Settings")]
        private void ResetSettings()
        {
            PlayerPrefs.DeleteKey(PREF_DEVICE);
            PlayerPrefs.DeleteKey(PREF_FRAMERATE);
            PlayerPrefs.DeleteKey(PREF_SAMPLERATE);
            PlayerPrefs.DeleteKey(PREF_DROPFRAME);
            PlayerPrefs.Save();
            
            // デフォルト値に戻す
            selectedDevice = "";
            ltcFrameRate = LTCFrameRate.FPS_30;
            sampleRate = 48000;
            useDropFrame = false;
            
            LogDebug("Settings reset to default");
        }
        #endif
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // 設定を読み込み
            LoadSettings();
            isInitialized = true;
            
            decoder = new TimecodeDecoder();
            ltcBuffer = new Queue<LTCSample>(bufferQueueSize);
            audioBuffer = new float[bufferSize];
            
            // ノイズ履歴バッファの初期化
            ltcNoiseHistory = new float[NoiseHistorySize];
            internalNoiseHistory = new float[NoiseHistorySize];
            for (int i = 0; i < NoiseHistorySize; i++)
            {
                ltcNoiseHistory[i] = 0f;
                internalNoiseHistory[i] = 0f;
            }
            
            // デバッガーを取得（オプショナル）
            debugger = GetComponent<LTCEventDebugger>();
        }
        
        private void OnEnable()
        {
            // Play中は録音を開始
            if (Application.isPlaying)
            {
                // デバイスが設定されていない場合は、利用可能な最初のデバイスを使用
                if (string.IsNullOrEmpty(selectedDevice) && Microphone.devices.Length > 0)
                {
                    selectedDevice = Microphone.devices[0];
                    SaveSettings();
                    LogDebug($"No device selected, using first available: {selectedDevice}");
                }
                
                if (!string.IsNullOrEmpty(selectedDevice))
                {
                    StartRecording();
                }
            }
        }
        
        private void OnDisable()
        {
            StopRecording();
        }
        
        private void OnDestroy()
        {
            if (isDirtyFromRuntime)
            {
                SaveSettings();  // 実行時変更があった場合のみ保存
            }
        }
        
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus && isDirtyFromRuntime)
            {
                SaveSettings();  // アプリがバックグラウンドに移行時に保存
            }
        }
        
        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && isDirtyFromRuntime)
            {
                SaveSettings();  // アプリがフォーカスを失った時に保存
            }
        }
        
        private void OnValidate()
        {
            // 初期化前は処理しない
            if (!isInitialized) return;
            
            // Play中のInspector変更を反映
            if (Application.isPlaying)
            {
                // Inspector値が変更されたかチェック
                if (HasInspectorChanges())
                {
                    isDirtyFromRuntime = true;
                    SaveSettings();
                    
                    // 必要に応じてシステムに適用
                    if (IsRecording)
                    {
                        // デバイスが変更された場合は再起動が必要
                        if (selectedDevice != lastSavedDevice)
                        {
                            StopRecording();
                            StartRecording();
                        }
                    }
                }
            }
            // Editor時（Play前）は何もしない - デフォルト値として保持
        }
        
        private void Update()
        {
            // デコードタイムアウトチェック
            CheckDecodeTimeout();
            
            // DSPクロック更新
            UpdateInternalClock();
        }
        
        #endregion
        
        #region DSP Clock Management
        
        /// <summary>
        /// デコードタイムアウトをチェック
        /// </summary>
        private void CheckDecodeTimeout()
        {
            // デコード中でタイムアウトしたかチェック
            if (isDecodingLTC)
            {
                float timeSinceLastDecode = Time.realtimeSinceStartup - lastDecodedTime;
                if (timeSinceLastDecode > decodeTimeoutSeconds)
                {
                    // デコードがタイムアウト = LTC停止
                    isDecodingLTC = false;
                    hasSignal = false;
                    isRunning = false;
                    currentState = SyncState.NoSignal;
                    
                    LogDebug($"LTC decoding stopped - timeout after {timeSinceLastDecode:F2}s");
                    
                    // LTC Stoppedイベントを発火
                    var eventData = new LTCEventData(
                        currentTimecode,
                        (float)internalTcTime,
                        false,
                        0f
                    );
                    OnLTCStopped?.Invoke(eventData);
                    
                    // デバッグメッセージ
                    debugger?.AddDebugMessage($"LTC Decoding Stopped at {currentTimecode} (Timeout)", 
                        DebugMessage.EVENT, UnityEngine.Color.yellow);
                }
            }
        }
        
        /// <summary>
        /// 出力タイムコード（Output TC）を更新
        /// </summary>
        private void UpdateInternalClock()
        {
            // isRunningのみをチェック（hasSignalはここで変更しない）
            // isRunningは内部クロックが動作中かどうかを示す
            if (!isRunning) 
            {
                return;
            }
            
            double currentDsp = AudioSettings.dspTime;
            if (currentDsp <= 0) return;
            
            double deltaTime = currentDsp - dspTimeBase;
            if (deltaTime < 0 || deltaTime > 1.0)
            {
                // 異常な時間差の場合はリセット
                dspTimeBase = currentDsp;
                return;
            }
            
            // 出力タイムコードを更新
            internalTcTime += deltaTime;
            dspTimeBase = currentDsp;
            
            // タイムコード文字列に変換
            currentTimecode = SecondsToTimecode(internalTcTime);
            
            // hasSignalはProcessDecodedLTCとProcessAudioBufferで管理される
            // ここでは設定しない
            
            // デコードされたTCとの差分を計算
            if (ltcBuffer.Count > 0)
            {
                var latest = ltcBuffer.Last();
                double age = currentDsp - latest.dspTime;
                timeDifference = (float)(internalTcTime - (latest.tcSeconds + age));
            }
            
            // Output TCのノイズ度合いを記録（変化の滑らかさ）
            float currentInternalTime = (float)internalTcTime;
            float internalNoise = 0f;
            if (lastInternalTime > 0)
            {
                float expectedDelta = (float)deltaTime;
                float actualDelta = currentInternalTime - lastInternalTime;
                float deviation = Mathf.Abs(actualDelta - expectedDelta);
                // 0.001秒（1ms）の誤差を基準にノイズ度合いを計算
                internalNoise = Mathf.Clamp01(deviation / 0.001f);
            }
            lastInternalTime = currentInternalTime;
            
            // ノイズ履歴に追加（時間ベースで更新）
            float currentTime = Time.time;
            if (currentTime - lastNoiseUpdateTime >= NoiseUpdateInterval)
            {
                internalNoiseHistory[noiseHistoryIndex] = internalNoise;
                // ProcessDecodedLTCで同じインデックスを更新するため、ここではインデックスを進めない
            }
            
            // イベント処理
            ProcessLTCEvents();
        }
        
        /// <summary>
        /// LTC関連イベントを処理
        /// </summary>
        private void ProcessLTCEvents()
        {
            // イベント用データ作成
            var eventData = new LTCEventData(
                currentTimecode,
                (float)internalTcTime,
                hasSignal,
                signalLevel
            );
            
            // Started/StoppedイベントはProcessDecodedLTCとCheckDecodeTimeoutで発火
            // ここでは継続状態イベントのみ処理
            
            // 継続状態イベント
            if (hasSignal)
            {
                OnLTCReceiving?.Invoke(eventData);
                CheckTimecodeEvents(eventData);  // TC指定イベントチェック
            }
            else
            {
                OnLTCNoSignal?.Invoke(eventData);
            }
            
            // wasReceivingLTCの更新（互換性のため残す）
            wasReceivingLTC = hasSignal;
            
            // タイムコードイベントのリセット処理（LTC停止時）
            if (!isDecodingLTC && wasDecodingLTC)
            {
                ResetTimecodeEvents();
                lastCheckedTimecode = "00:00:00:00";  // リセット
                LogDebug("All timecode events reset due to LTC stop");
            }
            
            // デコード状態の更新
            wasDecodingLTC = isDecodingLTC;
        }
        
        /// <summary>
        /// タイムコード指定イベントをチェック
        /// </summary>
        private void CheckTimecodeEvents(LTCEventData eventData)
        {
            // タイムコード巻き戻し検知
            if (resetOnRewind && IsTimecodeRewind(lastCheckedTimecode, eventData.currentTimecode))
            {
                ResetPassedTimecodeEvents(eventData.currentTimecode);
                LogDebug($"Timecode rewind detected: {lastCheckedTimecode} -> {eventData.currentTimecode}");
            }
            
            // イベントチェック
            foreach (var tcEvent in timecodeEvents)
            {
                if (tcEvent.IsMatch(eventData.currentTimecode, GetActualFrameRate()))
                {
                    tcEvent.onTimecodeReached?.Invoke(eventData);
                    tcEvent.triggered = true;
                    LogDebug($"Timecode Event '{tcEvent.eventName}' triggered at {eventData.currentTimecode}");
                    
                    // デバッグメッセージ追加
                    debugger?.AddDebugMessage($"Timecode Event '{tcEvent.eventName}' triggered at {eventData.currentTimecode}", 
                        DebugMessage.TIMECODE_EVENT, UnityEngine.Color.cyan);
                }
            }
            
            // 最後にチェックしたタイムコードを更新
            lastCheckedTimecode = eventData.currentTimecode;
        }
        
        /// <summary>
        /// すべてのタイムコードイベントをリセット
        /// </summary>
        private void ResetTimecodeEvents()
        {
            foreach (var tcEvent in timecodeEvents)
            {
                tcEvent.Reset();
            }
        }
        
        /// <summary>
        /// 巻き戻し時に該当するイベントをリセット
        /// </summary>
        private void ResetPassedTimecodeEvents(string currentTC)
        {
            float currentSeconds = TimecodeToSeconds(currentTC);
            
            foreach (var tcEvent in timecodeEvents)
            {
                if (tcEvent.triggered)
                {
                    float eventSeconds = TimecodeToSeconds(tcEvent.targetTimecode);
                    
                    // 現在のタイムコードより後のイベントをリセット
                    if (eventSeconds > currentSeconds)
                    {
                        tcEvent.Reset();
                        LogDebug($"Timecode Event '{tcEvent.eventName}' reset due to rewind");
                        
                        // デバッグメッセージ
                        debugger?.AddDebugMessage($"Event '{tcEvent.eventName}' reset (rewind detected)", 
                            DebugMessage.INFO, UnityEngine.Color.yellow);
                    }
                }
            }
        }
        
        /// <summary>
        /// タイムコードが巻き戻されたかチェック
        /// </summary>
        private bool IsTimecodeRewind(string previousTC, string currentTC)
        {
            float prevSeconds = TimecodeToSeconds(previousTC);
            float currSeconds = TimecodeToSeconds(currentTC);
            
            // 1秒以上戻った場合を巻き戻しとみなす（小さな誤差は無視）
            return (prevSeconds - currSeconds) > 1.0f;
        }
        
        /// <summary>
        /// タイムコード文字列を秒に変換
        /// </summary>
        private float TimecodeToSeconds(string timecode)
        {
            if (string.IsNullOrEmpty(timecode)) return 0f;
            
            string[] parts = timecode.Split(':');
            if (parts.Length != 4) return 0f;
            
            if (float.TryParse(parts[0], out float hours) &&
                float.TryParse(parts[1], out float minutes) &&
                float.TryParse(parts[2], out float seconds) &&
                float.TryParse(parts[3], out float frames))
            {
                return hours * 3600f + minutes * 60f + seconds + (frames / GetActualFrameRate());
            }
            
            return 0f;
        }
        
        /// <summary>
        /// タイムコードにオフセットを適用
        /// </summary>
        private string ApplyTimeOffset(string timecode)
        {
            if (timeOffset == 0f) return timecode;
            if (string.IsNullOrEmpty(timecode)) return timecode;
            
            float seconds = TimecodeToSeconds(timecode);
            seconds += timeOffset;
            
            // 負の値は0にクランプ
            if (seconds < 0) seconds = 0;
            
            return SecondsToTimecode(seconds);
        }
        
        /// <summary>
        /// 秒数をタイムコード文字列に変換
        /// </summary>
        private string SecondsToTimecode(float totalSeconds)
        {
            // 24時間を超える場合の処理
            totalSeconds = totalSeconds % 86400; // 24 * 60 * 60
            if (totalSeconds < 0) totalSeconds += 86400;
            
            int hours = (int)(totalSeconds / 3600);
            int minutes = (int)((totalSeconds % 3600) / 60);
            int seconds = (int)(totalSeconds % 60);
            int frames = (int)((totalSeconds % 1) * GetActualFrameRate());
            
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}:{frames:D2}";
        }
        
        #endregion
        
        #region Audio Processing
        
        public void SetDevice(string deviceName)
        {
            if (selectedDevice == deviceName) return;
            
            bool wasRecording = IsRecording;
            if (wasRecording) StopRecording();
            
            selectedDevice = deviceName;
            isDirtyFromRuntime = true;  // 実行時変更フラグを立てる
            SaveSettings();  // 設定を保存
            
            // Play中は常に録音を開始（初回設定時も含む）
            if (Application.isPlaying && !string.IsNullOrEmpty(deviceName))
            {
                StartRecording();
            }
            
            // Inspectorを更新
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
        
        private void StartRecording()
        {
            if (string.IsNullOrEmpty(selectedDevice))
            {
                // デバイスが設定されていない場合、利用可能な最初のデバイスを使用
                if (Microphone.devices.Length > 0)
                {
                    selectedDevice = Microphone.devices[0];
                    SaveSettings();
                    LogDebug($"No device selected, using first available: {selectedDevice}");
                }
                else
                {
                    UnityEngine.Debug.LogWarning("No audio devices available");
                    return;
                }
            }
            
            if (!Microphone.devices.Contains(selectedDevice))
            {
                // 指定されたデバイスが見つからない場合、利用可能な最初のデバイスを使用
                if (Microphone.devices.Length > 0)
                {
                    string oldDevice = selectedDevice;
                    selectedDevice = Microphone.devices[0];
                    SaveSettings();
                    UnityEngine.Debug.LogWarning($"Device '{oldDevice}' not found, using '{selectedDevice}' instead");
                }
                else
                {
                    UnityEngine.Debug.LogWarning($"Device '{selectedDevice}' not found and no other devices available");
                    return;
                }
            }
            
            microphoneClip = Microphone.Start(selectedDevice, true, 1, sampleRate);
            if (microphoneClip == null)
            {
                UnityEngine.Debug.LogError($"Failed to start recording from {selectedDevice}");
                return;
            }
            
            lastSamplePosition = 0;
            
            if (audioProcessingCoroutine != null)
                StopCoroutine(audioProcessingCoroutine);
            audioProcessingCoroutine = StartCoroutine(ProcessAudioData());
            
            LogDebug($"Started recording from {selectedDevice}");
        }
        
        private void StopRecording()
        {
            if (audioProcessingCoroutine != null)
            {
                StopCoroutine(audioProcessingCoroutine);
                audioProcessingCoroutine = null;
            }
            
            if (!string.IsNullOrEmpty(selectedDevice))
            {
                Microphone.End(selectedDevice);
            }
            
            if (microphoneClip != null)
            {
                Destroy(microphoneClip);
                microphoneClip = null;
            }
            
            currentState = SyncState.NoSignal;
            hasSignal = false;
            isRunning = false;
            
            LogDebug("Stopped recording");
        }
        
        private IEnumerator ProcessAudioData()
        {
            while (microphoneClip != null)
            {
                int currentPosition = Microphone.GetPosition(selectedDevice);
                
                if (currentPosition < lastSamplePosition)
                {
                    int samplesToRead = microphoneClip.samples - lastSamplePosition;
                    if (samplesToRead > 0)
                    {
                        ProcessAudioSegment(lastSamplePosition, samplesToRead);
                    }
                    lastSamplePosition = 0;
                }
                
                if (currentPosition > lastSamplePosition)
                {
                    int samplesToRead = currentPosition - lastSamplePosition;
                    ProcessAudioSegment(lastSamplePosition, samplesToRead);
                    lastSamplePosition = currentPosition;
                }
                
                yield return new WaitForSeconds(0.01f);
            }
        }
        
        private void ProcessAudioSegment(int startPosition, int length)
        {
            if (length > audioBuffer.Length)
            {
                audioBuffer = new float[length];
            }
            
            microphoneClip.GetData(audioBuffer, startPosition);
            
            // 信号レベル検出
            float maxAmplitude = 0f;
            for (int i = 0; i < length; i++)
            {
                float absValue = Mathf.Abs(audioBuffer[i]);
                if (absValue > maxAmplitude)
                    maxAmplitude = absValue;
            }
            
            // 自動ゲイン調整（AGC）
            // 最大値を徐々に減衰させながら、新しいピークで更新
            signalLevelMax = Mathf.Max(signalLevelMax * AGC_DECAY, maxAmplitude);
            
            // 正規化されたレベル（0-1の範囲）
            float normalizedLevel = maxAmplitude / Mathf.Max(signalLevelMax, AGC_MIN_THRESHOLD);
            
            // スムーズング適用
            signalLevel = Mathf.Lerp(signalLevel, Mathf.Clamp01(normalizedLevel), 0.5f);
            
            // 音声信号の有無判定（生の振幅で判定）
            bool audioSignalPresent = maxAmplitude > signalThreshold;
            
            if (!audioSignalPresent)
            {
                // 音声信号なし - デコードタイムアウトで処理されるため、ここでは何もしない
                // CheckDecodeTimeout()で適切なタイミングでStoppedイベントが発火される
                return;
            }
            
            // 音声信号があるが、まだhasSignalはLTCデコード成功まで待つ
            
            // LTCデコード
            var span = new ReadOnlySpan<float>(audioBuffer, 0, length);
            decoder.ParseAudioData(span);
            
            if (decoder.LastTimecode != null)
            {
                string tcString = decoder.LastTimecode.ToString();
                if (tcString != lastDecodedTc)
                {
                    ProcessDecodedLTC(tcString, decoder.LastTimecode);
                    lastDecodedTc = tcString;
                }
            }
        }
        
        #endregion
        
        #region LTC Buffer Analysis
        
        /// <summary>
        /// デコードされたLTCを処理
        /// </summary>
        private void ProcessDecodedLTC(string tcString, Timecode tc)
        {
            decodedTimecode = tcString;
            useDropFrame = tc.DropFrame;
            
            // LTCデコード成功 = 有効な信号あり
            consecutiveStops = 0;  // 停止カウントをリセット
            hasSignal = true;
            lastDecodedTime = Time.realtimeSinceStartup; // デコード成功時刻を記録
            
            // デコード開始/再開の検出
            if (!isDecodingLTC)
            {
                isDecodingLTC = true;
                LogDebug($"LTC decoding started/resumed - {tcString}");
                
                // LTC Startedイベントを発火
                var eventData = new LTCEventData(
                    tcString,
                    (float)TimecodeToSeconds(tc),
                    true,
                    signalLevel
                );
                OnLTCStarted?.Invoke(eventData);
                
                // デバッグメッセージ
                debugger?.AddDebugMessage($"LTC Decoding Started at {tcString}", 
                    DebugMessage.EVENT, UnityEngine.Color.green);
            }
            
            var sample = new LTCSample
            {
                dspTime = AudioSettings.dspTime,
                timecode = tcString,
                tcSeconds = TimecodeToSeconds(tc)
            };
            
            // LTCのノイズ度合いを計算（フレーム間の時間差のばらつき）
            float ltcNoise = 0f;
            float currentLtcTime = (float)sample.tcSeconds;
            if (lastLtcTime > 0)
            {
                float actualDelta = currentLtcTime - lastLtcTime;
                // 期待される差分（1フレーム分の時間）
                float expectedDelta = 1.0f / GetActualFrameRate();
                float deviation = Mathf.Abs(actualDelta - expectedDelta);
                // 0.01秒（10ms）の誤差を基準にノイズ度合いを計算
                ltcNoise = Mathf.Clamp01(deviation / 0.01f);
            }
            lastLtcTime = currentLtcTime;
            
            // ノイズ履歴に追加（時間ベースで更新）
            float currentTime = Time.time;
            if (currentTime - lastNoiseUpdateTime >= NoiseUpdateInterval)
            {
                ltcNoiseHistory[noiseHistoryIndex] = ltcNoise;
                // インデックスを進める
                noiseHistoryIndex = (noiseHistoryIndex + 1) % NoiseHistorySize;
                lastNoiseUpdateTime = currentTime;
            }
            
            // バッファに追加
            ltcBuffer.Enqueue(sample);
            while (ltcBuffer.Count > bufferQueueSize)
            {
                ltcBuffer.Dequeue();
            }
            
            // バッファ解析
            AnalyzeBuffer();
        }
        
        /// <summary>
        /// バッファを解析して同期状態を判定
        /// </summary>
        private void AnalyzeBuffer()
        {
            if (ltcBuffer.Count < 3)
            {
                if (currentState != SyncState.Syncing)
                {
                    currentState = SyncState.Syncing;
                    LogDebug("Acquiring LTC samples...");
                }
                return;
            }
            
            var samples = ltcBuffer.TakeLast(5).ToArray();
            
            // 停止検出
            if (DetectStop(samples))
            {
                HandleStop();
                return;
            }
            
            // ジャンプ検出
            if (DetectJump(samples))
            {
                HandleJump(samples.Last());
                return;
            }
            
            // 安定性チェック
            if (CheckStability(samples))
            {
                HandleStableProgression(samples.Last());
            }
        }
        
        /// <summary>
        /// 停止状態を検出
        /// </summary>
        private bool DetectStop(LTCSample[] samples)
        {
            if (samples.Length < 2) return false;
            
            int sameCount = 0;
            for (int i = 1; i < samples.Length; i++)
            {
                double diff = Math.Abs(samples[i].tcSeconds - samples[i - 1].tcSeconds);
                if (diff < stopThreshold)
                {
                    sameCount++;
                }
            }
            
            return sameCount >= samples.Length - 1;
        }
        
        /// <summary>
        /// ジャンプを検出
        /// </summary>
        private bool DetectJump(LTCSample[] samples)
        {
            if (samples.Length < 2) return false;
            
            double diff = Math.Abs(samples[samples.Length - 1].tcSeconds - samples[samples.Length - 2].tcSeconds);
            return diff > jumpThreshold;
        }
        
        /// <summary>
        /// 安定した進行をチェック
        /// </summary>
        private bool CheckStability(LTCSample[] samples)
        {
            if (samples.Length < 3) return false;
            
            var diffs = new List<double>();
            for (int i = 1; i < samples.Length; i++)
            {
                double timeDiff = samples[i].tcSeconds - samples[i - 1].tcSeconds;
                double dspDiff = samples[i].dspTime - samples[i - 1].dspTime;
                
                if (dspDiff > 0)
                {
                    diffs.Add(Math.Abs(timeDiff - dspDiff));
                }
            }
            
            if (diffs.Count < 2) return false;
            
            // 差分が全て閾値以内
            return diffs.All(d => d < syncThreshold);
        }
        
        #endregion
        
        #region Sync Handlers
        
        /// <summary>
        /// 停止状態を処理
        /// </summary>
        private void HandleStop()
        {
            consecutiveStops++;
            if (consecutiveStops > 2)
            {
                // LTC信号が停止したので、出力タイムコードも停止
                // HandleStopはDetectStopから呼ばれるが、実際の信号喪失は
                // ProcessAudioBufferで処理されるため、ここでは処理しない
                // （二重処理を避ける）
                LogDebug("TC stopped detected in buffer analysis");
            }
        }
        
        /// <summary>
        /// ジャンプを処理
        /// </summary>
        private void HandleJump(LTCSample target)
        {
            // ジャンプ前のタイムコードを保存
            string oldTC = currentTimecode;
            
            SyncToLTC(target);
            currentState = SyncState.Locked;
            consecutiveStops = 0;
            LogDebug($"Jump detected - synced to {target.timecode}");
            
            // デバッグメッセージ追加
            debugger?.AddDebugMessage($"Jump detected: {oldTC} → {target.timecode}", 
                DebugMessage.WARNING, UnityEngine.Color.yellow);
        }
        
        /// <summary>
        /// 安定した進行を処理
        /// </summary>
        private void HandleStableProgression(LTCSample target)
        {
            consecutiveStops = 0;
            
            if (currentState == SyncState.Syncing || currentState == SyncState.NoSignal)
            {
                // 初回同期
                SyncToLTC(target);
                currentState = SyncState.Locked;
                LogDebug($"Initial sync to {target.timecode}");
            }
            else if (currentState == SyncState.Locked || currentState == SyncState.Drifting)
            {
                // ドリフトチェック
                double currentDsp = AudioSettings.dspTime;
                double age = currentDsp - target.dspTime;
                double expectedTc = target.tcSeconds + age;
                double drift = Math.Abs(internalTcTime - expectedTc);
                
                // 段階的なドリフト処理
                if (drift <= driftDeadzoneSmall)
                {
                    // 小さなドリフトは完全無視（ノイズとみなす）
                    currentState = SyncState.Locked;
                    // 補正なし
                }
                else if (drift <= driftDeadzoneMedium)
                {
                    // 中程度のドリフトは非常にゆっくり補正
                    currentState = SyncState.Locked;
                    double correction = (expectedTc - internalTcTime) * driftCorrectionSlow;
                    internalTcTime += correction;
                    if (enableDebugLogging)
                    {
                        LogDebug($"Small drift correction: {drift:F3}s (correction: {correction:F4}s)");
                    }
                }
                else if (drift <= driftThresholdLarge)
                {
                    // 通常のドリフトは段階的に補正（既存のdriftCorrectionまたはdriftCorrectionNormalを使用）
                    currentState = SyncState.Drifting;
                    double correction = (expectedTc - internalTcTime) * driftCorrectionNormal;
                    internalTcTime += correction;
                    LogDebug($"Normal drift correction: {drift:F3}s (correction: {correction:F4}s)");
                }
                else
                {
                    // 大きなドリフトは即座に同期
                    SyncToLTC(target);
                    currentState = SyncState.Locked;
                    LogDebug($"Large drift - immediate sync: {drift:F3}s");
                }
            }
            
            if (!isRunning)
            {
                isRunning = true;
                dspTimeBase = AudioSettings.dspTime;
            }
        }
        
        /// <summary>
        /// LTCに同期
        /// </summary>
        private void SyncToLTC(LTCSample target)
        {
            double currentDsp = AudioSettings.dspTime;
            double age = currentDsp - target.dspTime;
            
            dspTimeBase = currentDsp;
            internalTcTime = target.tcSeconds + age;
            isRunning = true;
            lastSyncTime = currentDsp;
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// タイムコードを秒に変換
        /// </summary>
        private double TimecodeToSeconds(Timecode tc)
        {
            if (tc == null) return 0;
            
            double totalSeconds = tc.Hour * 3600 + tc.Minute * 60 + tc.Second;
            totalSeconds += tc.Frame / GetActualFrameRate();
            
            return totalSeconds;
        }
        
        /// <summary>
        /// 秒をタイムコード文字列に変換
        /// </summary>
        private string SecondsToTimecode(double totalSeconds)
        {
            if (totalSeconds < 0) totalSeconds = 0;
            
            int hours = (int)(totalSeconds / 3600);
            int minutes = (int)((totalSeconds % 3600) / 60);
            int seconds = (int)(totalSeconds % 60);
            int frames = (int)((totalSeconds % 1.0) * GetActualFrameRate());
            
            hours = hours % 24;
            frames = Math.Min(frames, (int)GetActualFrameRate() - 1);
            
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}:{frames:D2}";
        }
        
        /// <summary>
        /// デバッグログ
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogging)
            {
                UnityEngine.Debug.Log($"[LTCDecoder] {message}");
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// デバイスリストを更新
        /// </summary>
        public void RefreshDevices()
        {
            LogDebug($"Found {AvailableDevices.Length} audio devices");
        }
        
        /// <summary>
        /// LTCフレームレートを設定
        /// </summary>
        public void SetLTCFrameRate(LTCFrameRate newFrameRate)
        {
            if (ltcFrameRate == newFrameRate) return;
            
            ltcFrameRate = newFrameRate;
            
            // ドロップフレームの自動設定
            if (ltcFrameRate == LTCFrameRate.FPS_29_97_DF)
            {
                useDropFrame = true;
            }
            else if (ltcFrameRate == LTCFrameRate.FPS_29_97_NDF)
            {
                useDropFrame = false;
            }
            
            isDirtyFromRuntime = true;  // 実行時変更フラグを立てる
            SaveSettings();  // 設定を保存
            
            // 内部時計のリセット
            if (Application.isPlaying && IsRecording)
            {
                ResetStatistics();
                LogDebug($"LTC Frame Rate changed to {GetActualFrameRate()} fps");
            }
            
            // Inspectorを更新
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
        
        /// <summary>
        /// サンプルレートを設定
        /// </summary>
        public void SetSampleRate(int newSampleRate)
        {
            if (sampleRate == newSampleRate) return;
            
            bool wasRecording = IsRecording;
            if (wasRecording) StopRecording();
            
            sampleRate = newSampleRate;
            isDirtyFromRuntime = true;  // 実行時変更フラグを立てる
            SaveSettings();  // 設定を保存
            
            if (wasRecording && !string.IsNullOrEmpty(selectedDevice))
            {
                StartRecording();
                LogDebug($"Sample Rate changed to {sampleRate} Hz");
            }
            
            // Inspectorを更新
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
        
        /// <summary>
        /// ドロップフレーム設定を変更
        /// </summary>
        public void SetDropFrame(bool dropFrame)
        {
            if (useDropFrame == dropFrame) return;
            
            useDropFrame = dropFrame;
            
            // 29.97fpsの場合、フレームレート設定も更新
            if (ltcFrameRate == LTCFrameRate.FPS_29_97_DF || ltcFrameRate == LTCFrameRate.FPS_29_97_NDF)
            {
                ltcFrameRate = dropFrame ? LTCFrameRate.FPS_29_97_DF : LTCFrameRate.FPS_29_97_NDF;
            }
            
            LogDebug($"Drop Frame {(dropFrame ? "enabled" : "disabled")}");
            
            // Inspectorを更新
            #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            #endif
        }
        
        /// <summary>
        /// 実際のフレームレート値を取得
        /// </summary>
        private float GetActualFrameRate()
        {
            switch (ltcFrameRate)
            {
                case LTCFrameRate.FPS_29_97_DF:
                case LTCFrameRate.FPS_29_97_NDF:
                    return 29.97f;
                case LTCFrameRate.FPS_24:
                    return 24.0f;
                case LTCFrameRate.FPS_25:
                    return 25.0f;
                case LTCFrameRate.FPS_30:
                    return 30.0f;
                default:
                    return 30.0f;
            }
        }
        
        /// <summary>
        /// 統計をリセット
        /// </summary>
        public void ResetStatistics()
        {
            ltcBuffer.Clear();
            consecutiveStops = 0;
            currentState = SyncState.NoSignal;
            timeDifference = 0;
            isRunning = false;
            LogDebug("Statistics reset");
        }
        
        /// <summary>
        /// 手動同期
        /// </summary>
        public void ManualSync(string timecode)
        {
            var parts = timecode.Split(':');
            if (parts.Length == 4 &&
                int.TryParse(parts[0], out int h) &&
                int.TryParse(parts[1], out int m) &&
                int.TryParse(parts[2], out int s) &&
                int.TryParse(parts[3], out int f))
            {
                internalTcTime = h * 3600 + m * 60 + s + f / GetActualFrameRate();
                dspTimeBase = AudioSettings.dspTime;
                isRunning = true;
                currentState = SyncState.Locked;
                LogDebug($"Manual sync to {timecode}");
            }
        }
        
        /// <summary>
        /// タイムコードイベントを追加
        /// </summary>
        public void AddTimecodeEvent(string eventName, string targetTimecode, UnityAction<LTCEventData> action)
        {
            var newEvent = new TimecodeEvent();
            newEvent.eventName = eventName;
            newEvent.targetTimecode = targetTimecode;
            newEvent.onTimecodeReached.AddListener(action);
            timecodeEvents.Add(newEvent);
        }
        
        /// <summary>
        /// タイムコードイベントを削除
        /// </summary>
        public void RemoveTimecodeEvent(string eventName)
        {
            timecodeEvents.RemoveAll(e => e.eventName == eventName);
        }
        
        /// <summary>
        /// すべてのタイムコードイベントをクリア
        /// </summary>
        public void ClearTimecodeEvents()
        {
            timecodeEvents.Clear();
        }
        
        #endregion
        
        #region Editor PlayMode State Handler
        
        #if UNITY_EDITOR
        /// <summary>
        /// Play Mode終了時の設定保存とEditor復帰時の復元（Editor限定）
        /// </summary>
        [UnityEditor.InitializeOnLoadMethod]
        static void SetupPlayModeStateChanged()
        {
            UnityEditor.EditorApplication.playModeStateChanged += (state) =>
            {
                if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                {
                    // 全インスタンスの設定を保存
                    foreach (var decoder in FindObjectsOfType<LTCDecoder>())
                    {
                        if (decoder != null && decoder.isDirtyFromRuntime)
                        {
                            decoder.SaveSettings();
                            UnityEngine.Debug.Log("[LTCDecoder] Settings saved on exiting play mode");
                        }
                    }
                }
                else if (state == UnityEditor.PlayModeStateChange.EnteredEditMode)
                {
                    // EditMode復帰時に保存された設定をInspectorに反映
                    UnityEditor.EditorApplication.delayCall += () =>
                    {
                        foreach (var decoder in FindObjectsOfType<LTCDecoder>())
                        {
                            if (decoder != null)
                            {
                                decoder.LoadSettingsForEditor();
                            }
                        }
                    };
                }
            };
        }
        
        /// <summary>
        /// Editor用の設定読み込み（Play終了後のInspector更新用）
        /// </summary>
        private void LoadSettingsForEditor()
        {
            // PlayerPrefsから設定を読み込んでシリアライズフィールドに反映
            if (PlayerPrefs.HasKey(PREF_DEVICE))
            {
                selectedDevice = PlayerPrefs.GetString(PREF_DEVICE, selectedDevice);
                ltcFrameRate = (LTCFrameRate)PlayerPrefs.GetInt(PREF_FRAMERATE, (int)ltcFrameRate);
                sampleRate = PlayerPrefs.GetInt(PREF_SAMPLERATE, sampleRate);
                useDropFrame = PlayerPrefs.GetInt(PREF_DROPFRAME, useDropFrame ? 1 : 0) == 1;
                
                // SerializedObjectを使用してInspectorを更新
                using (var so = new UnityEditor.SerializedObject(this))
                {
                    so.FindProperty("selectedDevice").stringValue = selectedDevice;
                    so.FindProperty("ltcFrameRate").enumValueIndex = (int)ltcFrameRate;
                    so.FindProperty("sampleRate").intValue = sampleRate;
                    so.FindProperty("useDropFrame").boolValue = useDropFrame;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                
                // Inspectorを更新
                UnityEditor.EditorUtility.SetDirty(this);
                
                UnityEngine.Debug.Log($"[LTCDecoder] Settings restored in Editor - Device: {selectedDevice}, FrameRate: {ltcFrameRate}, SampleRate: {sampleRate}");
            }
        }
        #endif
        
        #endregion
    }
}