using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LTC.Timeline
{
    /// <summary>
    /// DSPクロックベースのLTCデコーダー
    /// 内部クロックで自走し、デコードされたLTCと同期を取る
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
        [SerializeField] private float timeDifference = 0f;
        
        [Header("Frame Rate")]
        [SerializeField] private float frameRate = 30.0f;
        [SerializeField] private bool dropFrame = false;
        
        [Header("Debug")]
        [SerializeField] private bool enableDebugLogging = false;
        
        #endregion
        
        #region Private Fields
        
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
        
        #endregion
        
        #region Properties
        
        public string CurrentTimecode => currentTimecode;
        public string DecodedTimecode => decodedTimecode;
        public SyncState State => currentState;
        public bool HasSignal => hasSignal;
        public float SignalLevel => signalLevel;
        public float TimeDifference => timeDifference;
        public bool IsRecording => microphoneClip != null;
        public string SelectedDevice => selectedDevice;
        public string[] AvailableDevices => Microphone.devices;
        public bool DropFrame => dropFrame;
        
        #endregion
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            decoder = new TimecodeDecoder();
            ltcBuffer = new Queue<LTCSample>(bufferQueueSize);
            audioBuffer = new float[bufferSize];
        }
        
        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(selectedDevice))
            {
                StartRecording();
            }
        }
        
        private void OnDisable()
        {
            StopRecording();
        }
        
        private void Update()
        {
            // DSPクロック更新
            UpdateInternalClock();
        }
        
        #endregion
        
        #region DSP Clock Management
        
        /// <summary>
        /// 内部クロックを更新
        /// </summary>
        private void UpdateInternalClock()
        {
            if (!isRunning) return;
            
            double currentDsp = AudioSettings.dspTime;
            if (currentDsp <= 0) return;
            
            double deltaTime = currentDsp - dspTimeBase;
            if (deltaTime < 0 || deltaTime > 1.0)
            {
                dspTimeBase = currentDsp;
                return;
            }
            
            // 内部タイムコードを更新
            internalTcTime += deltaTime;
            dspTimeBase = currentDsp;
            
            // タイムコード文字列に変換
            currentTimecode = SecondsToTimecode(internalTcTime);
            
            // デコードされたTCとの差分を計算
            if (ltcBuffer.Count > 0)
            {
                var latest = ltcBuffer.Last();
                double age = currentDsp - latest.dspTime;
                timeDifference = (float)(internalTcTime - (latest.tcSeconds + age));
            }
        }
        
        #endregion
        
        #region Audio Processing
        
        public void SetDevice(string deviceName)
        {
            if (selectedDevice == deviceName) return;
            
            bool wasRecording = IsRecording;
            if (wasRecording) StopRecording();
            
            selectedDevice = deviceName;
            
            if (wasRecording && !string.IsNullOrEmpty(deviceName))
            {
                StartRecording();
            }
        }
        
        private void StartRecording()
        {
            if (string.IsNullOrEmpty(selectedDevice)) return;
            
            if (!Microphone.devices.Contains(selectedDevice))
            {
                Debug.LogWarning($"Device '{selectedDevice}' not found");
                return;
            }
            
            microphoneClip = Microphone.Start(selectedDevice, true, 1, sampleRate);
            if (microphoneClip == null)
            {
                Debug.LogError($"Failed to start recording from {selectedDevice}");
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
            
            signalLevel = Mathf.Lerp(signalLevel, maxAmplitude, 0.5f);
            hasSignal = signalLevel > signalThreshold;
            
            if (!hasSignal)
            {
                if (currentState != SyncState.NoSignal)
                {
                    currentState = SyncState.NoSignal;
                    LogDebug("Signal lost");
                }
                return;
            }
            
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
            dropFrame = tc.DropFrame;
            
            var sample = new LTCSample
            {
                dspTime = AudioSettings.dspTime,
                timecode = tcString,
                tcSeconds = TimecodeToSeconds(tc)
            };
            
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
                isRunning = false;
                currentState = SyncState.Locked;
                LogDebug("TC stopped - pausing internal clock");
            }
        }
        
        /// <summary>
        /// ジャンプを処理
        /// </summary>
        private void HandleJump(LTCSample target)
        {
            SyncToLTC(target);
            currentState = SyncState.Locked;
            consecutiveStops = 0;
            LogDebug($"Jump detected - synced to {target.timecode}");
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
                
                if (drift > syncThreshold)
                {
                    currentState = SyncState.Drifting;
                    // ソフトな補正
                    double correction = (expectedTc - internalTcTime) * driftCorrection;
                    internalTcTime += correction;
                    LogDebug($"Drift correction: {drift:F3}s");
                }
                else
                {
                    currentState = SyncState.Locked;
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
            totalSeconds += tc.Frame / frameRate;
            
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
            int frames = (int)((totalSeconds % 1.0) * frameRate);
            
            hours = hours % 24;
            frames = Math.Min(frames, (int)frameRate - 1);
            
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}:{frames:D2}";
        }
        
        /// <summary>
        /// デバッグログ
        /// </summary>
        private void LogDebug(string message)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[LTCDecoder] {message}");
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
                internalTcTime = h * 3600 + m * 60 + s + f / frameRate;
                dspTimeBase = AudioSettings.dspTime;
                isRunning = true;
                currentState = SyncState.Locked;
                LogDebug($"Manual sync to {timecode}");
            }
        }
        
        #endregion
    }
}