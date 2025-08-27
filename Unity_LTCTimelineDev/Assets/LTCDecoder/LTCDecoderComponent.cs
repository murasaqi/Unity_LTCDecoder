using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace LTC.Timeline
{
    [AddComponentMenu("Audio/LTC Decoder")]
    public class LTCDecoderComponent : MonoBehaviour
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
        [SerializeField] private float noiseFloor = 0.001f;
        [SerializeField] private float peakHoldTime = 2f;
        
        [Header("Debug Settings")]
        [SerializeField] private bool enableDebugMode = false;
        [SerializeField] private bool useImprovedBufferHandling = false;
        [SerializeField] private bool useTimecodeValidation = false;
        [SerializeField] private bool useAdvancedBinarization = false;
        [SerializeField] private bool usePeriodStabilization = false;
        [SerializeField] private bool useNoiseHysteresis = false;
        [SerializeField] private bool logDebugInfo = false;
        [SerializeField] private int maxDebugLogs = 100;
        
        private AudioClip microphoneClip;
        private TimecodeDecoder decoder;
        private Timecode lastDecodedTimecode;
        private float[] audioBuffer;
        private int lastSamplePosition = 0;
        private Coroutine audioProcessingCoroutine;
        private float peakHoldTimer = 0f;
        private float lastPeakTime = 0f;
        
        // Debug logging
        private System.Collections.Generic.List<string> debugLogs = new System.Collections.Generic.List<string>();
        public System.Collections.Generic.List<string> DebugLogs => debugLogs;
        
        // For improved buffer handling
        private float[] tempBuffer;
        
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
                Debug.LogError($"Device '{selectedDevice}' not found. Available devices: {string.Join(", ", Microphone.devices)}");
                return;
            }
            
            int minFreq, maxFreq;
            Microphone.GetDeviceCaps(selectedDevice, out minFreq, out maxFreq);
            
            if (maxFreq == 0) maxFreq = sampleRate;
            int actualSampleRate = Mathf.Clamp(sampleRate, minFreq, maxFreq);
            
            microphoneClip = Microphone.Start(selectedDevice, true, 1, actualSampleRate);
            
            if (microphoneClip == null)
            {
                Debug.LogError($"Failed to start recording from device: {selectedDevice}");
                return;
            }
            
            isRecording = true;
            lastSamplePosition = 0;
            
            if (audioProcessingCoroutine != null)
            {
                StopCoroutine(audioProcessingCoroutine);
            }
            audioProcessingCoroutine = StartCoroutine(ProcessAudioData());
            
            Debug.Log($"Started recording from: {selectedDevice} at {actualSampleRate} Hz");
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
            
            Debug.Log($"Stopped recording from: {selectedDevice}");
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
                    if (enableDebugMode && useImprovedBufferHandling && isWrapped)
                    {
                        // Improved buffer handling for wrapped case
                        int part1Size = microphoneClip.samples - lastSamplePosition;
                        int part2Size = currentPosition;
                        
                        if (tempBuffer == null || tempBuffer.Length < samplesToRead)
                        {
                            tempBuffer = new float[samplesToRead];
                        }
                        
                        // Read the two parts separately
                        float[] part1 = new float[part1Size];
                        microphoneClip.GetData(part1, lastSamplePosition);
                        
                        float[] part2 = new float[part2Size];
                        microphoneClip.GetData(part2, 0);
                        
                        // Combine the parts
                        Array.Copy(part1, 0, tempBuffer, 0, part1Size);
                        Array.Copy(part2, 0, tempBuffer, part1Size, part2Size);
                        
                        LogDebug($"Buffer wrap handled: {part1Size}+{part2Size}={samplesToRead} samples");
                        
                        ProcessAudioBuffer(tempBuffer, samplesToRead);
                    }
                    else
                    {
                        // Original implementation
                        if (samplesToRead > audioBuffer.Length)
                        {
                            audioBuffer = new float[samplesToRead];
                        }
                        
                        microphoneClip.GetData(audioBuffer, lastSamplePosition);
                        
                        if (isWrapped && logDebugInfo)
                        {
                            LogDebug($"Buffer wrap (old method): {samplesToRead} samples");
                        }
                        
                        ProcessAudioBuffer(audioBuffer, samplesToRead);
                    }
                    
                    lastSamplePosition = currentPosition;
                }
                
                yield return new WaitForSeconds(0.01f);
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
                if (i % (length / 64) == 0 && waveformWriteIndex < waveformData.Length)
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
            hasSignal = signalLevel > noiseFloor;
            
            if (hasSignal)
            {
                var span = new ReadOnlySpan<float>(buffer, 0, length);
                decoder.ParseAudioData(span);
                
                if (decoder.LastTimecode != null)
                {
                    if (lastDecodedTimecode == null || !lastDecodedTimecode.Equals(decoder.LastTimecode))
                    {
                        lastDecodedTimecode = decoder.LastTimecode;
                        currentTimecode = lastDecodedTimecode.ToString();
                        dropFrame = lastDecodedTimecode.DropFrame;
                        framesSinceLastUpdate = 0;
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
        
        private void LogDebug(string message)
        {
            if (!logDebugInfo) return;
            
            string logEntry = $"[{Time.time:F3}] {message}";
            debugLogs.Add(logEntry);
            
            // Keep log size manageable
            while (debugLogs.Count > maxDebugLogs)
            {
                debugLogs.RemoveAt(0);
            }
            
            Debug.Log($"[LTC Debug] {logEntry}");
        }
        
        private float TimecodeToSeconds(Timecode tc)
        {
            if (tc == null) return 0f;
            
            float fps = tc.DropFrame ? 29.97f : 30f;
            return tc.Hour * 3600f + tc.Minute * 60f + tc.Second + (tc.Frame / fps);
        }
        
        private bool ValidateTimecode(Timecode newTC, Timecode lastTC)
        {
            if (!enableDebugMode || !useTimecodeValidation) return true;
            if (lastTC == null) return true;
            
            float newTime = TimecodeToSeconds(newTC);
            float lastTime = TimecodeToSeconds(lastTC);
            float diff = Mathf.Abs(newTime - lastTime);
            
            // Reject if difference is more than 1 second
            if (diff > 1.0f)
            {
                LogDebug($"Timecode rejected: {newTC} (diff={diff:F3}s from {lastTC})");
                return false;
            }
            
            // Warn if backward jump
            if (newTime < lastTime && diff > 0.1f)
            {
                LogDebug($"Timecode backward jump: {newTC} ({diff:F3}s behind {lastTC})");
            }
            
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