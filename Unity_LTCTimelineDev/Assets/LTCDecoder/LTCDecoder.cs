using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace LTC.Timeline
{
    [AddComponentMenu("Audio/LTC Decoder")]
    public class LTCDecoder : MonoBehaviour
    {
        [Header("Audio Input Settings")]
        [SerializeField] private string selectedDevice = "";
        [SerializeField] private int sampleRate = 48000;
        [SerializeField] private int bufferSize = 2048;
        
        [Header("Status")]
        [SerializeField] private bool isRecording = false;
        [SerializeField] private bool hasSignal = false;
        [SerializeField] private float signalLevel = 0f;
        
        [Header("Timecode Output")]
        [SerializeField] private string currentTimecode = "00:00:00:00";
        [SerializeField] private bool dropFrame = false;
        [SerializeField] private int framesSinceLastUpdate = 0;
        
        [Header("Audio Monitoring")]
        [SerializeField] private float currentLevel = 0f;
        [SerializeField] private float peakLevel = 0f;
        [SerializeField] private float[] waveformData = new float[512];
        [SerializeField] private int waveformWriteIndex = 0;
        [SerializeField] private float peakHoldTime = 2f;
        
        [Header("Signal Detection")]
        [SerializeField, Range(0.001f, 0.1f)] private float signalThreshold = 0.01f; // Signal detection threshold
        [SerializeField, Range(0.001f, 0.1f)] private float noiseFloor = 0.001f; // Noise floor level
        
        [Header("Jitter Detection Settings")]
        [SerializeField, Range(0.001f, 1.0f)] private float jitterThreshold = 0.1f; // 100ms default - threshold for detecting jitter
        [SerializeField, Range(0.5f, 10.0f)] private float intentionalJumpThreshold = 1.0f; // Minimum time for intentional jump detection (default: 1 second)
        [SerializeField, Range(0.1f, 7200.0f)] private float maxAllowedJitter = 3600.0f; // Maximum allowed time jump in seconds (default: 1 hour)
        [SerializeField, Range(1, 100)] private int jitterHistorySize = 50; // Sample count for averaging
        [SerializeField] private bool enableJitterDetection = true;
        
        [Header("Denoising Settings")]
        [SerializeField, Range(1, 10)] private int continuityCheckFrames = 3; // Frames to check for continuity
        [SerializeField, Range(0.001f, 0.1f)] private float timecodeStabilityWindow = 0.033f; // ~1 frame at 30fps
        [SerializeField, Range(0.0f, 1.0f)] private float denoisingStrength = 0.8f; // Filter strength (0=off, 1=max)
        [SerializeField] private bool enableAdaptiveFiltering = true; // Adjust filter based on signal quality
        [SerializeField, Range(1, 5)] private int minConsecutiveValidFrames = 2; // Min valid frames before accepting (reduced max for faster response)
        
        [Header("Debug Settings")]
        [SerializeField] private bool useTimecodeValidation = true;
        
        [Header("Logging Settings")]
        [SerializeField] private bool logDebugInfo = false;
        [SerializeField] private LogLevel logLevel = LogLevel.Warning;
        [SerializeField] private bool logToConsole = false; // Disable Console.Log by default for performance
        [SerializeField] private bool logValidation = false; // Log validation rejections
        [SerializeField] private bool logJumps = true; // Log significant jumps
        [SerializeField] private bool logBufferIssues = true; // Log buffer problems
        [SerializeField] private int maxDebugLogs = 100;
        
        public enum LogLevel
        {
            Error = 0,
            Warning = 1,
            Info = 2,
            Debug = 3,
            Verbose = 4
        }
        
        private AudioClip microphoneClip;
        private TimecodeDecoder decoder;
        private Timecode lastDecodedTimecode;
        private float[] audioBuffer;
        private int lastSamplePosition = 0;
        private Coroutine audioProcessingCoroutine;
        private float lastPeakTime = 0f;
        
        // Debug logging
        private System.Collections.Generic.List<string> debugLogs = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> DebugLogs => debugLogs;
        
        // For improved buffer handling
        private float[] tempBuffer;
        
        // Jitter tracking
        private System.Collections.Generic.Queue<float> jitterHistory;
        private float lastTimecodeSeconds = 0f;
        private float maxJump = 0f;
        private float averageJitter = 0f;
        private int jumpCount = 0;
        private int rejectedCount = 0;
        private int totalDecodedCount = 0;
        
        // Denoising state
        private int consecutiveValidFrames = 0;
        private Timecode lastAcceptedTimecode = null;
        // Simplified jump tracking
        private Timecode potentialJumpTarget = null; // Store potential new stable timecode
        
        public System.Collections.Generic.Queue<float> JitterHistory => jitterHistory;
        public float MaxJump => maxJump;
        public float AverageJitter => averageJitter;
        public int JumpCount => jumpCount;
        public int RejectedCount => rejectedCount;
        public int TotalDecodedCount => totalDecodedCount;
        public float RejectionRate => totalDecodedCount > 0 ? (float)rejectedCount / totalDecodedCount * 100f : 0f;
        
        public string[] AvailableDevices => Microphone.devices;
        public string SelectedDevice => selectedDevice;
        public string CurrentTimecode => currentTimecode;
        public bool IsRecording => isRecording;
        public bool HasSignal => hasSignal;
        public float SignalLevel => signalLevel;
        public float CurrentLevel => currentLevel;
        public float PeakLevel => peakLevel;
        public float[] WaveformData => waveformData;
        public float NoiseFloor => noiseFloor;
        
        private void Awake()
        {
            decoder = new TimecodeDecoder();
            audioBuffer = new float[bufferSize];
            
            // Initialize jitter tracking queue with user-defined size
            jitterHistory = new System.Collections.Generic.Queue<float>(jitterHistorySize);
            
            if (Microphone.devices.Length > 0 && string.IsNullOrEmpty(selectedDevice))
            {
                selectedDevice = Microphone.devices[0];
            }
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
        
        private void OnDestroy()
        {
            StopRecording();
        }
        
        public void SetDevice(string deviceName)
        {
            if (selectedDevice == deviceName) return;
            
            bool wasRecording = isRecording;
            if (wasRecording)
            {
                StopRecording();
            }
            
            selectedDevice = deviceName;
            
            if (wasRecording && !string.IsNullOrEmpty(deviceName))
            {
                StartRecording();
            }
        }
        
        public void StartRecording()
        {
            if (isRecording || string.IsNullOrEmpty(selectedDevice)) return;
            
            if (!Microphone.devices.Contains(selectedDevice))
            {
                LogDebug($"Device '{selectedDevice}' not found. Available devices: {string.Join(", ", Microphone.devices)}", LogLevel.Error);
                return;
            }
            
            int minFreq, maxFreq;
            Microphone.GetDeviceCaps(selectedDevice, out minFreq, out maxFreq);
            
            if (maxFreq == 0) maxFreq = sampleRate;
            int actualSampleRate = Mathf.Clamp(sampleRate, minFreq, maxFreq);
            
            microphoneClip = Microphone.Start(selectedDevice, true, 1, actualSampleRate);
            
            if (microphoneClip == null)
            {
                LogDebug($"Failed to start recording from device: {selectedDevice}", LogLevel.Error);
                return;
            }
            
            isRecording = true;
            lastSamplePosition = 0;
            
            if (audioProcessingCoroutine != null)
            {
                StopCoroutine(audioProcessingCoroutine);
            }
            audioProcessingCoroutine = StartCoroutine(ProcessAudioData());
            
            LogDebug($"Started recording from: {selectedDevice} at {actualSampleRate} Hz", LogLevel.Info);
        }
        
        public void StopRecording()
        {
            if (!isRecording) return;
            
            if (audioProcessingCoroutine != null)
            {
                StopCoroutine(audioProcessingCoroutine);
                audioProcessingCoroutine = null;
            }
            
            Microphone.End(selectedDevice);
            
            if (microphoneClip != null)
            {
                Destroy(microphoneClip);
                microphoneClip = null;
            }
            
            isRecording = false;
            hasSignal = false;
            signalLevel = 0f;
            
            LogDebug($"Stopped recording from: {selectedDevice}", LogLevel.Info);
        }
        
        private IEnumerator ProcessAudioData()
        {
            while (isRecording && microphoneClip != null)
            {
                int currentPosition = Microphone.GetPosition(selectedDevice);
                
                if (currentPosition < 0)
                {
                    yield return null;
                    continue;
                }
                
                int samplesToRead = 0;
                bool isWrapped = false;
                
                if (currentPosition >= lastSamplePosition)
                {
                    samplesToRead = currentPosition - lastSamplePosition;
                }
                else
                {
                    // Buffer has wrapped around
                    samplesToRead = (microphoneClip.samples - lastSamplePosition) + currentPosition;
                    isWrapped = true;
                }
                
                if (samplesToRead > 0)
                {
                    // Limit processing to prevent decoder overload
                    const int MAX_SAMPLES_PER_PROCESS = 2048;  // Reduced for better stability
                    
                    // Check if we need to process in chunks
                    if (samplesToRead > MAX_SAMPLES_PER_PROCESS)
                    {
                        if (logDebugInfo)
                        {
                            LogDebug($"Large buffer detected: {samplesToRead} samples, processing in chunks", LogLevel.Info, "buffer");
                        }
                    }
                    
                    // Always use improved buffer handling for wrapped case
                    if (isWrapped)
                    {
                        // Improved buffer handling for wrapped case
                        int part1Size = microphoneClip.samples - lastSamplePosition;
                        int part2Size = currentPosition;
                        
                        if (logDebugInfo)
                        {
                            LogDebug($"Buffer wrap detected: part1={part1Size}, part2={part2Size}, total={samplesToRead}", LogLevel.Debug, "buffer");
                        }
                        
                        if (tempBuffer == null || tempBuffer.Length < samplesToRead)
                        {
                            tempBuffer = new float[samplesToRead];
                        }
                        
                        // Read the two parts correctly
                        if (part1Size > 0)
                        {
                            float[] part1 = new float[part1Size];
                            microphoneClip.GetData(part1, lastSamplePosition);
                            Array.Copy(part1, 0, tempBuffer, 0, part1Size);
                        }
                        
                        if (part2Size > 0)
                        {
                            float[] part2 = new float[part2Size];
                            microphoneClip.GetData(part2, 0);
                            Array.Copy(part2, 0, tempBuffer, part1Size, part2Size);
                        }
                        
                        // Remove verbose success log for performance
                        // LogDebug($"Buffer wrap handled correctly: {part1Size}+{part2Size}={samplesToRead} samples", LogLevel.Verbose, "buffer");
                        
                        // Process in chunks if necessary
                        if (samplesToRead > MAX_SAMPLES_PER_PROCESS)
                        {
                            for (int offset = 0; offset < samplesToRead; offset += MAX_SAMPLES_PER_PROCESS)
                            {
                                int chunkSize = Mathf.Min(MAX_SAMPLES_PER_PROCESS, samplesToRead - offset);
                                float[] chunk = new float[chunkSize];
                                Array.Copy(tempBuffer, offset, chunk, 0, chunkSize);
                                ProcessAudioBuffer(chunk, chunkSize);
                            }
                        }
                        else
                        {
                            ProcessAudioBuffer(tempBuffer, samplesToRead);
                        }
                    }
                    else
                    {
                        // Non-wrapped case
                        if (samplesToRead > audioBuffer.Length)
                        {
                            audioBuffer = new float[samplesToRead];
                        }
                        
                        microphoneClip.GetData(audioBuffer, lastSamplePosition);
                        
                        // Process in chunks if necessary
                        if (samplesToRead > MAX_SAMPLES_PER_PROCESS)
                        {
                            for (int offset = 0; offset < samplesToRead; offset += MAX_SAMPLES_PER_PROCESS)
                            {
                                int chunkSize = Mathf.Min(MAX_SAMPLES_PER_PROCESS, samplesToRead - offset);
                                float[] chunk = new float[chunkSize];
                                Array.Copy(audioBuffer, offset, chunk, 0, chunkSize);
                                ProcessAudioBuffer(chunk, chunkSize);
                            }
                        }
                        else
                        {
                            ProcessAudioBuffer(audioBuffer, samplesToRead);
                        }
                    }
                    
                    lastSamplePosition = currentPosition;
                }
                
                yield return new WaitForSeconds(0.005f); // Poll more frequently (5ms) to avoid missing data
            }
        }
        
        private void ProcessAudioBuffer(float[] buffer, int length)
        {
            if (length <= 0) return;
            
            float maxAmplitude = 0f;
            float sumAmplitude = 0f;
            
            // Calculate levels and update waveform
            for (int i = 0; i < length; i++)
            {
                float sample = buffer[i];
                float absValue = Mathf.Abs(sample);
                
                if (absValue > maxAmplitude)
                {
                    maxAmplitude = absValue;
                }
                
                sumAmplitude += absValue;
                
                // Store samples for waveform visualization (decimated)
                int decimation = Mathf.Max(1, length / 64);  // Avoid divide by zero
                if (i % decimation == 0 && waveformWriteIndex < waveformData.Length)
                {
                    waveformData[waveformWriteIndex] = sample;
                    waveformWriteIndex = (waveformWriteIndex + 1) % waveformData.Length;
                }
            }
            
            // Update current level (RMS-like)
            float avgAmplitude = sumAmplitude / length;
            currentLevel = Mathf.Lerp(currentLevel, avgAmplitude, 0.3f);
            
            // Update peak level with hold
            if (maxAmplitude > peakLevel)
            {
                peakLevel = maxAmplitude;
                lastPeakTime = Time.time;
            }
            else if (Time.time - lastPeakTime > peakHoldTime)
            {
                peakLevel = Mathf.Lerp(peakLevel, maxAmplitude, 0.1f);
            }
            
            signalLevel = Mathf.Lerp(signalLevel, maxAmplitude, 0.5f);
            
            // Simple signal detection with adjustable threshold
            if (!hasSignal && signalLevel > signalThreshold)
            {
                hasSignal = true;
                LogDebug($"Signal ON: level={signalLevel:F4}, threshold={signalThreshold:F4}", LogLevel.Debug, "signal");
            }
            else if (hasSignal && signalLevel < signalThreshold * 0.5f)
            {
                hasSignal = false;
                LogDebug($"Signal OFF: level={signalLevel:F4}, threshold={signalThreshold * 0.5f:F4}", LogLevel.Debug, "signal");
            }
            
            if (hasSignal)
            {
                // オーディオフィルタリングを無効化し、元のバッファをそのまま使用
                var span = new ReadOnlySpan<float>(buffer, 0, length);
                decoder.ParseAudioData(span);
                
                if (decoder.LastTimecode != null)
                {
                    if (lastDecodedTimecode == null || !lastDecodedTimecode.Equals(decoder.LastTimecode))
                    {
                        totalDecodedCount++;
                        
                        // Apply timecode validation
                        if (ValidateTimecode(decoder.LastTimecode, lastDecodedTimecode))
                        {
                            // Track FILTERED jitter after validation
                            float newSeconds = TimecodeToSeconds(decoder.LastTimecode);
                            if (lastTimecodeSeconds > 0)
                            {
                                float timeDiff = newSeconds - lastTimecodeSeconds;
                                float jitter = Mathf.Abs(timeDiff - (1.0f / 30.0f)); // Assuming 30fps nominal
                                
                                // Add to jitter history with user-defined size
                                if (jitterHistory.Count >= jitterHistorySize)
                                    jitterHistory.Dequeue();
                                jitterHistory.Enqueue(jitter);
                                
                                // Track jumps using user-defined threshold
                                if (enableJitterDetection && Mathf.Abs(timeDiff) > jitterThreshold)
                                {
                                    jumpCount++;
                                    if (Mathf.Abs(timeDiff) > maxJump)
                                        maxJump = Mathf.Abs(timeDiff);
                                    
                                    if (logDebugInfo)
                                    {
                                        int framesSkipped = Mathf.RoundToInt(Mathf.Abs(timeDiff) * 30f);
                                        LogDebug($"TC Jump detected: {timeDiff:F3}s ({framesSkipped} frames) from {lastDecodedTimecode} to {decoder.LastTimecode}", LogLevel.Warning, "jump");
                                        // Remove verbose buffer state log
                                    }
                                }
                                
                                // Update average jitter
                                if (jitterHistory.Count > 0)
                                {
                                    float sum = 0;
                                    foreach (float j in jitterHistory)
                                        sum += j;
                                    averageJitter = sum / jitterHistory.Count;
                                }
                            }
                            lastTimecodeSeconds = newSeconds;
                            
                            lastDecodedTimecode = decoder.LastTimecode;
                            currentTimecode = lastDecodedTimecode.ToString();
                            dropFrame = lastDecodedTimecode.DropFrame;
                            framesSinceLastUpdate = 0;
                            
                            // Remove verbose timecode update log - too frequent
                            // if (logDebugInfo) LogDebug($"Timecode updated: {currentTimecode}", LogLevel.Verbose);
                        }
                        else
                        {
                            // Timecode was rejected
                            rejectedCount++;
                            // Log rejection statistics periodically, not every rejection
                            if (logDebugInfo && rejectedCount % 10 == 0)
                            {
                                LogDebug($"Timecode rejection rate: {rejectedCount}/{totalDecodedCount} ({RejectionRate:F1}%)", LogLevel.Info, "validation");
                            }
                        }
                    }
                    else
                    {
                        framesSinceLastUpdate++;
                    }
                }
            }
        }
        
        public void RefreshDevices()
        {
            if (Microphone.devices.Length > 0)
            {
                if (string.IsNullOrEmpty(selectedDevice) || !Microphone.devices.Contains(selectedDevice))
                {
                    selectedDevice = Microphone.devices[0];
                }
            }
            else
            {
                selectedDevice = "";
            }
        }
        
        public void ResetJitterStatistics()
        {
            // Reset jitter tracking data
            jitterHistory.Clear();
            lastTimecodeSeconds = 0f;
            maxJump = 0f;
            averageJitter = 0f;
            jumpCount = 0;
            rejectedCount = 0;
            totalDecodedCount = 0;
            
            // Reset denoising state
            consecutiveValidFrames = 0;
            lastAcceptedTimecode = null;
            potentialJumpTarget = null;
            
            LogDebug("All jitter statistics and denoising state reset");
        }
        
        private void LogDebug(string message, LogLevel level = LogLevel.Debug, string category = "general")
        {
            if (!logDebugInfo) return;
            if (level > logLevel) return; // Skip if log level is too low
            
            // Check category-specific settings
            if (!ShouldLogCategory(category)) return;
            
            // Store in internal buffer (always, for Inspector display)
            string logEntry = $"[{Time.time:F3}] [{level}] {message}";
            debugLogs.Add(logEntry);
            
            // Keep log size manageable
            while (debugLogs.Count > maxDebugLogs)
            {
                debugLogs.RemoveAt(0);
            }
            
            // Only output to Unity Console if explicitly enabled (performance critical)
            if (logToConsole)
            {
                switch (level)
                {
                    case LogLevel.Error:
                        Debug.LogError($"[LTC] {message}");
                        break;
                    case LogLevel.Warning:
                        Debug.LogWarning($"[LTC] {message}");
                        break;
                    default:
                        Debug.Log($"[LTC {level}] {message}");
                        break;
                }
            }
        }
        
        private bool ShouldLogCategory(string category)
        {
            switch (category)
            {
                case "validation":
                    return logValidation;
                case "jump":
                    return logJumps;
                case "buffer":
                    return logBufferIssues;
                default:
                    return true;
            }
        }
        
        private float TimecodeToSeconds(Timecode tc)
        {
            if (tc == null) return 0f;
            
            float fps = tc.DropFrame ? 29.97f : 30f;
            return tc.Hour * 3600f + tc.Minute * 60f + tc.Second + (tc.Frame / fps);
        }
        
        private bool ValidateTimecode(Timecode newTC, Timecode lastTC)
        {
            // Always validate basic timecode ranges
            if (newTC == null) return false;
            
            // Check for invalid time values
            if (newTC.Hour < 0 || newTC.Hour > 23)
            {
                LogDebug($"Timecode rejected: Invalid hour {newTC.Hour}", LogLevel.Warning, "validation");
                return false;
            }
            if (newTC.Minute < 0 || newTC.Minute > 59)
            {
                LogDebug($"Timecode rejected: Invalid minute {newTC.Minute}", LogLevel.Warning, "validation");
                return false;
            }
            if (newTC.Second < 0 || newTC.Second > 59)
            {
                LogDebug($"Timecode rejected: Invalid second {newTC.Second}", LogLevel.Warning, "validation");
                return false;
            }
            // Frame check based on drop frame mode
            int maxFrame = newTC.DropFrame ? 29 : 29;  // Both NTSC formats use 30 frames (0-29)
            if (newTC.Frame < 0 || newTC.Frame > maxFrame)
            {
                LogDebug($"Timecode rejected: Invalid frame {newTC.Frame} (max={maxFrame})", LogLevel.Warning, "validation");
                return false;
            }
            
            // If validation is disabled, only do basic range checks
            if (!useTimecodeValidation) return true;
            
            // Additional continuity validation when enabled
            if (lastTC == null)
            {
                // First timecode - always accept to avoid startup delays
                lastAcceptedTimecode = newTC;
                consecutiveValidFrames = minConsecutiveValidFrames; // Start in stable state
                return true;
            }
            
            float newTime = TimecodeToSeconds(newTC);
            float lastTime = TimecodeToSeconds(lastTC);
            float diff = Mathf.Abs(newTime - lastTime);
            
            // Reject jumps larger than maxAllowedJitter
            if (diff > maxAllowedJitter)
            {
                LogDebug($"Timecode rejected: Unrealistic jump {diff:F3}s (>{maxAllowedJitter}s) from {lastTC} to {newTC}", LogLevel.Error, "jump");
                return false;
            }
            
            // Apply denoising based on user settings
            if (enableAdaptiveFiltering && denoisingStrength > 0)
            {
                // Check for duplicate/stale timecodes with user-defined window
                // Only reject if it's truly a duplicate (same value), not just small changes
                if (diff < 0.001f) // Less than 1ms is considered duplicate
                {
                    LogDebug($"Timecode rejected: Duplicate value {newTC}", LogLevel.Debug, "validation");
                    return false;
                }
                
                // For very small changes that might be noise (but not duplicates)
                if (diff < timecodeStabilityWindow * denoisingStrength && diff > 0.001f)
                {
                    // Only reject if we're not in stable continuous playback
                    if (consecutiveValidFrames < minConsecutiveValidFrames)
                    {
                        LogDebug($"Timecode rejected: Within stability window {diff:F3}s for {newTC} (not yet stable)", LogLevel.Verbose, "validation");
                        return false;
                    }
                    // If stable, allow small changes as they might be legitimate frame advances
                }
                
                // Three-stage jump detection for better noise filtering
                if (diff > intentionalJumpThreshold)
                {
                    // Stage 3: Large jumps (>1 second) are intentional (user seeking)
                    // Accept immediately for responsive operation
                    LogDebug($"Intentional jump detected and accepted: {newTC} (jump: {diff:F3}s)", LogLevel.Info, "jump");
                    consecutiveValidFrames = minConsecutiveValidFrames; // Keep stable state
                    lastAcceptedTimecode = newTC;  // Update last accepted timecode
                    potentialJumpTarget = null;     // Clear any jump tracking
                    return true;
                }
                else if (diff > jitterThreshold)
                {
                    // Stage 2: Medium jumps (0.1s to 1s) are likely noise/glitches
                    // Reject these to maintain stable timecode
                    LogDebug($"Timecode rejected: Noisy jump {diff:F3}s from {lastTC} to {newTC}", LogLevel.Warning, "validation");
                    return false;
                }
                else
                {
                    // Stage 1: Small changes (<0.1s) are normal continuous playback
                    // Accept immediately for smooth operation
                    if (potentialJumpTarget != null)
                    {
                        // Clear any jump tracking
                        potentialJumpTarget = null;
                    }
                    
                    // Normal continuous playback - always accept
                    consecutiveValidFrames = minConsecutiveValidFrames; // Maintain stable state
                    lastAcceptedTimecode = newTC;
                    return true;
                }
            }
            else
            {
                // Simple backward jump check without adaptive filtering
                if (newTime < lastTime && diff > jitterThreshold)
                {
                    LogDebug($"Timecode backward jump: {newTC} ({diff:F3}s behind {lastTC})", LogLevel.Warning, "jump");
                    return false;
                }
            }
            
            lastAcceptedTimecode = newTC;
            return true;
        }
        
        #if UNITY_EDITOR
        private void OnValidate()
        {
            sampleRate = Mathf.Clamp(sampleRate, 8000, 192000);
            bufferSize = Mathf.Clamp(bufferSize, 256, 8192);
        }
        #endif
    }
}