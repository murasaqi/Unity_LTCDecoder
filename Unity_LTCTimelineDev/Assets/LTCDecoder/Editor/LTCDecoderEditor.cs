using UnityEngine;
using UnityEditor;
using System.Linq;
using UnityEditor.Graphs;

namespace LTC.Timeline
{
    [CustomEditor(typeof(LTCDecoder))]
    public class LTCDecoderEditor : Editor
    {
        private LTCDecoder component;
        private GUIStyle timecodeStyle;
        private GUIStyle statusStyle;
        private GUIStyle levelMeterStyle;
        private int selectedDeviceIndex = -1;
        private string[] deviceOptions;
        private bool showAdvancedSettings = false;
        private bool showAudioMonitoring = true;
        private bool showJitterAnalysis = false;
        private Texture2D waveformTexture;
        private Texture2D jitterGraphTexture;
        private Color waveformColor = new Color(0.2f, 0.8f, 0.2f);
        private Color waveformBackgroundColor = new Color(0.1f, 0.1f, 0.1f);
        private Color jitterGraphColor = new Color(0.8f, 0.4f, 0.2f);
        private Color jitterGraphBackgroundColor = new Color(0.15f, 0.15f, 0.15f);
        
        private void OnEnable()
        {
            component = (LTCDecoder)target;
            RefreshDeviceList();
            waveformTexture = new Texture2D(512, 100);
            jitterGraphTexture = new Texture2D(300, 150);
        }
        
        private void OnDisable()
        {
            if (waveformTexture != null)
            {
                DestroyImmediate(waveformTexture);
            }
            if (jitterGraphTexture != null)
            {
                DestroyImmediate(jitterGraphTexture);
            }
        }
        
        private void RefreshDeviceList()
        {
            deviceOptions = Microphone.devices;
            if (deviceOptions.Length == 0)
            {
                deviceOptions = new string[] { "No devices found" };
                selectedDeviceIndex = -1;
            }
            else
            {
                selectedDeviceIndex = System.Array.IndexOf(deviceOptions, component.SelectedDevice);
                if (selectedDeviceIndex < 0 && deviceOptions.Length > 0)
                {
                    selectedDeviceIndex = 0;
                }
            }
        }
        
        public override void OnInspectorGUI()
        {
            InitializeStyles();
            
            serializedObject.Update();
            
            EditorGUILayout.Space(10);
            
            // Title
            EditorGUILayout.LabelField("LTC Decoder", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            // Device Selection Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Audio Device", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            EditorGUI.BeginChangeCheck();
            int newIndex = EditorGUILayout.Popup("Select Device", selectedDeviceIndex, deviceOptions);
            if (EditorGUI.EndChangeCheck() && newIndex >= 0 && newIndex < deviceOptions.Length)
            {
                selectedDeviceIndex = newIndex;
                if (deviceOptions[0] != "No devices found")
                {
                    component.SetDevice(deviceOptions[newIndex]);
                }
            }
            
            if (GUILayout.Button("Refresh", GUILayout.Width(60)))
            {
                RefreshDeviceList();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            
            // Timecode Comparison Section
            EditorGUILayout.LabelField("Timecode Comparison", EditorStyles.boldLabel);
            
            // Two-column layout for RAW and Filtered TC
            EditorGUILayout.BeginHorizontal();
            
            // RAW TC (Left side, Red theme)
            EditorGUILayout.BeginVertical();
            GUI.backgroundColor = new Color(1f, 0.9f, 0.9f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("RAW TC (No Filter)", EditorStyles.boldLabel);
            
            Color originalColor = GUI.color;
            GUI.color = new Color(0.8f, 0.2f, 0.2f);
            
            var rawStyle = new GUIStyle(EditorStyles.largeLabel);
            rawStyle.fontSize = 18;
            rawStyle.fontStyle = FontStyle.Bold;
            rawStyle.alignment = TextAnchor.MiddleCenter;
            
            EditorGUILayout.LabelField(component.RawTimecode ?? "00:00:00:00", rawStyle, GUILayout.Height(30));
            GUI.color = originalColor;
            
            EditorGUILayout.LabelField($"Frames since update: {component.RawFramesSinceLastUpdate}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
            
            GUILayout.Space(10);
            
            // Filtered TC (Right side, Green theme)
            EditorGUILayout.BeginVertical();
            GUI.backgroundColor = new Color(0.9f, 1f, 0.9f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Filtered TC", EditorStyles.boldLabel);
            
            if (component.HasSignal)
            {
                GUI.color = new Color(0.2f, 0.8f, 0.2f);
            }
            else
            {
                GUI.color = Color.gray;
            }
            
            EditorGUILayout.LabelField(component.CurrentTimecode, rawStyle, GUILayout.Height(30));
            GUI.color = originalColor;
            
            EditorGUILayout.LabelField($"Frames since update: {component.FramesSinceLastUpdate}", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.EndHorizontal();
            
            // Difference warning
            string rawTC = component.RawTimecode ?? "00:00:00:00";
            if (rawTC != component.CurrentTimecode)
            {
                EditorGUILayout.HelpBox(
                    $"Filtering active - RAW: {rawTC} | Filtered: {component.CurrentTimecode}",
                    MessageType.Info
                );
            }
            
            // Status indicators
            EditorGUILayout.BeginHorizontal();
            
            // Signal status
            string signalStatus = component.HasSignal ? "Signal Detected" : "No Signal";
            Color statusColor = component.HasSignal ? Color.green : Color.red;
            GUI.color = statusColor;
            EditorGUILayout.LabelField("Status:", signalStatus, statusStyle);
            GUI.color = originalColor;
            
            // Signal level
            if (component.HasSignal)
            {
                EditorGUILayout.LabelField($"Level: {(component.SignalLevel * 100):F1}%");
            }
            
            EditorGUILayout.EndHorizontal();
            
            // Drop frame indicator
            SerializedProperty dropFrameProp = serializedObject.FindProperty("dropFrame");
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Toggle("Drop Frame", dropFrameProp.boolValue);
            EditorGUI.EndDisabledGroup();
            
            EditorGUILayout.Space(10);
            
            // Audio Monitoring Section
            showAudioMonitoring = EditorGUILayout.Foldout(showAudioMonitoring, "Audio Monitoring", true);
            
            if (showAudioMonitoring)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Level Meters
                EditorGUILayout.LabelField("Audio Levels", EditorStyles.boldLabel);
                
                // Current Level
                Rect levelRect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
                DrawLevelMeter(levelRect, component.CurrentLevel, "Current", Color.green);
                
                // Peak Level
                Rect peakRect = GUILayoutUtility.GetRect(0, 20, GUILayout.ExpandWidth(true));
                DrawLevelMeter(peakRect, component.PeakLevel, "Peak", Color.yellow);
                
                // Noise Floor indicator
                EditorGUILayout.LabelField($"Noise Floor: {component.NoiseFloor:F4}", EditorStyles.miniLabel);
                
                // Waveform Display
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Waveform", EditorStyles.boldLabel);
                
                Rect waveformRect = GUILayoutUtility.GetRect(0, 100, GUILayout.ExpandWidth(true));
                DrawWaveform(waveformRect);
                
                // Audio statistics
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Statistics", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Current Level: {(component.CurrentLevel * 100):F1}%", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Peak Level: {(component.PeakLevel * 100):F1}%", EditorStyles.miniLabel);
                EditorGUILayout.LabelField($"Signal: {(component.HasSignal ? "Present" : "Absent")}", EditorStyles.miniLabel);
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.Space(10);
            
            // Control Buttons
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Controls", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            if (!component.IsRecording)
            {
                GUI.color = Color.green;
                if (GUILayout.Button("Start Recording", GUILayout.Height(30)))
                {
                    component.StartRecording();
                }
            }
            else
            {
                GUI.color = Color.red;
                if (GUILayout.Button("Stop Recording", GUILayout.Height(30)))
                {
                    component.StopRecording();
                }
            }
            
            GUI.color = originalColor;
            EditorGUILayout.EndHorizontal();
            
            // Recording status
            string recordingStatus = component.IsRecording ? "Recording..." : "Stopped";
            Color recordingColor = component.IsRecording ? Color.green : Color.gray;
            GUI.color = recordingColor;
            EditorGUILayout.LabelField("Recording Status:", recordingStatus);
            GUI.color = originalColor;
            
            EditorGUILayout.EndVertical();
            
            EditorGUILayout.Space(10);
            
            // Advanced Settings (Foldout)
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Settings", true);
            
            if (showAdvancedSettings)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                SerializedProperty sampleRateProp = serializedObject.FindProperty("sampleRate");
                SerializedProperty bufferSizeProp = serializedObject.FindProperty("bufferSize");
                
                EditorGUILayout.IntSlider(sampleRateProp, 8000, 192000, "Sample Rate (Hz)");
                EditorGUILayout.IntSlider(bufferSizeProp, 256, 8192, "Buffer Size");
                
                EditorGUILayout.Space(5);
                
                // Debug information
                EditorGUILayout.LabelField("Debug Info", EditorStyles.miniLabel);
                SerializedProperty framesSinceUpdateProp = serializedObject.FindProperty("framesSinceLastUpdate");
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.IntField("Frames Since Update", framesSinceUpdateProp.intValue);
                EditorGUI.EndDisabledGroup();
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.Space(10);
            
            // Jitter Analysis Section
            showJitterAnalysis = EditorGUILayout.Foldout(showJitterAnalysis, "Timecode Jitter Analysis & Comparison", true);
            
            if (showJitterAnalysis)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Filtering Effectiveness Statistics
                EditorGUILayout.LabelField("Filtering Effectiveness", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Total Decoded:", GUILayout.Width(120));
                EditorGUILayout.LabelField(component.TotalDecodedCount.ToString(), EditorStyles.boldLabel);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Rejected Count:", GUILayout.Width(120));
                GUI.color = component.RejectedCount > 0 ? Color.yellow : Color.green;
                EditorGUILayout.LabelField($"{component.RejectedCount} ({component.RejectionRate:F1}%)", EditorStyles.boldLabel);
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
                
                
                EditorGUILayout.Space(10);
                
                // FILTERED Data Statistics (After Filtering)
                EditorGUILayout.LabelField("FILTERED Data (After Validation)", EditorStyles.boldLabel);
                GUI.backgroundColor = new Color(0.8f, 1f, 0.8f); // Light green background
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Average Jitter:", GUILayout.Width(100));
                GUI.color = component.AverageJitter > 0.01f ? Color.yellow : Color.green;
                EditorGUILayout.LabelField($"{component.AverageJitter * 1000:F2} ms", EditorStyles.boldLabel);
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Max Jump:", GUILayout.Width(100));
                GUI.color = component.MaxJump > 0.1f ? Color.yellow : Color.green;
                EditorGUILayout.LabelField($"{component.MaxJump:F3} seconds", EditorStyles.boldLabel);
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Jump Count:", GUILayout.Width(100));
                GUI.color = component.JumpCount > 5 ? Color.yellow : Color.green;
                EditorGUILayout.LabelField(component.JumpCount.ToString(), EditorStyles.boldLabel);
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
                
                // Filtered Jitter Graph
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Filtered Jitter Graph (ms)", EditorStyles.miniLabel);
                Rect jitterGraphRect = GUILayoutUtility.GetRect(0, 120, GUILayout.ExpandWidth(true));
                DrawJitterGraph(jitterGraphRect, component.JitterHistory, new Color(0.2f, 0.8f, 0.2f));
                
                EditorGUILayout.EndVertical();
                GUI.backgroundColor = Color.white;
                
                // Control buttons
                EditorGUILayout.Space(10);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Reset All Statistics", GUILayout.Height(30)))
                {
                    component.ResetJitterStatistics();
                }
                EditorGUILayout.EndHorizontal();
                
                // Jump threshold indicator
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox($"Jump Threshold: 100ms\nFiltering removes invalid timecodes and large jumps\nGreen = Stable, Yellow = Warning, Red = Critical", MessageType.Info);
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.Space(10);
            
            // Jitter Detection Settings Section
            bool showJitterSettings = EditorGUILayout.Foldout(true, "Jitter Detection Settings", true);
            if (showJitterSettings)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Jitter Detection Parameters", EditorStyles.boldLabel);
                
                SerializedProperty enableJitterDetectionProp = serializedObject.FindProperty("enableJitterDetection");
                SerializedProperty jitterThresholdProp = serializedObject.FindProperty("jitterThreshold");
                SerializedProperty maxAllowedJitterProp = serializedObject.FindProperty("maxAllowedJitter");
                SerializedProperty jitterHistorySizeProp = serializedObject.FindProperty("jitterHistorySize");
                
                if (enableJitterDetectionProp != null)
                    EditorGUILayout.PropertyField(enableJitterDetectionProp, new GUIContent("Enable Jitter Detection", "Enable detection of timecode jumps"));
                
                if (jitterThresholdProp != null)
                {
                    EditorGUILayout.Slider(jitterThresholdProp, 0.001f, 1.0f, new GUIContent("Jitter Threshold (sec)", "Minimum time difference to consider as a jump"));
                    EditorGUILayout.HelpBox($"Current: {jitterThresholdProp.floatValue * 1000:F1} ms", MessageType.None);
                }
                
                if (maxAllowedJitterProp != null)
                {
                    EditorGUILayout.Slider(maxAllowedJitterProp, 0.0f, 1.0f, new GUIContent("Max Allowed Jitter (sec)", "Maximum allowed jump before rejection"));
                    EditorGUILayout.HelpBox($"Current: {maxAllowedJitterProp.floatValue * 1000:F1} ms", MessageType.None);
                }
                
                if (jitterHistorySizeProp != null)
                    EditorGUILayout.IntSlider(jitterHistorySizeProp, 1, 100, new GUIContent("History Size", "Number of samples to keep for jitter averaging"));
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.Space(10);
            
            // Denoising Settings Section
            bool showDenoisingSettings = EditorGUILayout.Foldout(true, "Denoising Settings", true);
            if (showDenoisingSettings)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Denoising Parameters", EditorStyles.boldLabel);
                
                SerializedProperty enableAdaptiveFilteringProp = serializedObject.FindProperty("enableAdaptiveFiltering");
                SerializedProperty denoisingStrengthProp = serializedObject.FindProperty("denoisingStrength");
                SerializedProperty continuityCheckFramesProp = serializedObject.FindProperty("continuityCheckFrames");
                SerializedProperty timecodeStabilityWindowProp = serializedObject.FindProperty("timecodeStabilityWindow");
                SerializedProperty minConsecutiveValidFramesProp = serializedObject.FindProperty("minConsecutiveValidFrames");
                
                if (enableAdaptiveFilteringProp != null)
                    EditorGUILayout.PropertyField(enableAdaptiveFilteringProp, new GUIContent("Enable Adaptive Filtering", "Adjust filter based on signal quality"));
                
                if (denoisingStrengthProp != null)
                {
                    EditorGUILayout.Slider(denoisingStrengthProp, 0.0f, 1.0f, new GUIContent("Denoising Strength", "Filter strength (0=off, 1=maximum)"));
                    if (denoisingStrengthProp.floatValue == 0)
                    {
                        EditorGUILayout.HelpBox("Denoising is disabled (strength = 0)", MessageType.Warning);
                    }
                }
                
                if (timecodeStabilityWindowProp != null)
                {
                    EditorGUILayout.Slider(timecodeStabilityWindowProp, 0.001f, 0.1f, new GUIContent("Stability Window (sec)", "Minimum time change to accept as valid"));
                    EditorGUILayout.HelpBox($"Current: {timecodeStabilityWindowProp.floatValue * 1000:F1} ms (~{timecodeStabilityWindowProp.floatValue * 30:F1} frames @ 30fps)", MessageType.None);
                }
                
                if (continuityCheckFramesProp != null)
                    EditorGUILayout.IntSlider(continuityCheckFramesProp, 1, 10, new GUIContent("Continuity Check Frames", "Number of frames to buffer for continuity checking"));
                
                if (minConsecutiveValidFramesProp != null)
                    EditorGUILayout.IntSlider(minConsecutiveValidFramesProp, 1, 5, new GUIContent("Min Consecutive Valid", "Minimum consecutive valid frames before accepting (also used for detecting intentional jumps)"));
                
                EditorGUILayout.Space(5);
                EditorGUILayout.HelpBox("Denoising helps filter out invalid timecodes and stabilize the output. Higher strength = more aggressive filtering.", MessageType.Info);
                
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.Space(10);
            
            // Debug Settings Section
            SerializedProperty enableDebugModeProp = serializedObject.FindProperty("enableDebugMode");
            if (enableDebugModeProp != null)
            {
                bool showDebugSettingsSection = EditorGUILayout.Foldout(true, "Debug Settings", true);
                
                if (showDebugSettingsSection)
                {
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    
                    EditorGUILayout.LabelField("Debug Options", EditorStyles.boldLabel);
                    
                    // Enable Debug Mode checkbox
                    EditorGUILayout.PropertyField(enableDebugModeProp, new GUIContent("Enable Debug Mode"));
                    EditorGUILayout.Space(5);
                    
                    SerializedProperty useImprovedBufferProp = serializedObject.FindProperty("useImprovedBufferHandling");
                    SerializedProperty useTimecodeValidationProp = serializedObject.FindProperty("useTimecodeValidation");
                    SerializedProperty useAdvancedBinarizationProp = serializedObject.FindProperty("useAdvancedBinarization");
                    SerializedProperty usePeriodStabilizationProp = serializedObject.FindProperty("usePeriodStabilization");
                    SerializedProperty useNoiseHysteresisProp = serializedObject.FindProperty("useNoiseHysteresis");
                    SerializedProperty logDebugInfoProp = serializedObject.FindProperty("logDebugInfo");
                    SerializedProperty maxDebugLogsProp = serializedObject.FindProperty("maxDebugLogs");
                    
                    if (useImprovedBufferProp != null)
                        EditorGUILayout.PropertyField(useImprovedBufferProp, new GUIContent("Improved Buffer Handling"));
                    
                    if (useTimecodeValidationProp != null)
                        EditorGUILayout.PropertyField(useTimecodeValidationProp, new GUIContent("Timecode Validation"));
                    
                    if (useAdvancedBinarizationProp != null)
                        EditorGUILayout.PropertyField(useAdvancedBinarizationProp, new GUIContent("Advanced Binarization"));
                    
                    if (usePeriodStabilizationProp != null)
                        EditorGUILayout.PropertyField(usePeriodStabilizationProp, new GUIContent("Period Stabilization"));
                    
                    if (useNoiseHysteresisProp != null)
                        EditorGUILayout.PropertyField(useNoiseHysteresisProp, new GUIContent("Noise Hysteresis"));
                    
                    EditorGUILayout.EndVertical();
                }
            }
            
            EditorGUILayout.Space(10);
            
            // Logging Settings Section
            bool showLoggingSettings = EditorGUILayout.Foldout(true, "Logging Settings", true);
            if (showLoggingSettings)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.LabelField("Logging Configuration", EditorStyles.boldLabel);
                
                SerializedProperty logDebugInfoProp = serializedObject.FindProperty("logDebugInfo");
                SerializedProperty logLevelProp = serializedObject.FindProperty("logLevel");
                SerializedProperty logToConsoleProp = serializedObject.FindProperty("logToConsole");
                SerializedProperty logValidationProp = serializedObject.FindProperty("logValidation");
                SerializedProperty logJumpsProp = serializedObject.FindProperty("logJumps");
                SerializedProperty logBufferIssuesProp = serializedObject.FindProperty("logBufferIssues");
                SerializedProperty maxDebugLogsProp = serializedObject.FindProperty("maxDebugLogs");
                SerializedProperty logThrottleIntervalProp = serializedObject.FindProperty("logThrottleInterval");
                
                if (logDebugInfoProp != null)
                {
                    EditorGUILayout.PropertyField(logDebugInfoProp, new GUIContent("Enable Logging", "Master switch for all logging"));
                    
                    if (logDebugInfoProp.boolValue)
                    {
                        EditorGUI.indentLevel++;
                        
                        if (logLevelProp != null)
                            EditorGUILayout.PropertyField(logLevelProp, new GUIContent("Log Level", "Minimum level to log"));
                        
                        if (logToConsoleProp != null)
                        {
                            EditorGUILayout.PropertyField(logToConsoleProp, new GUIContent("Log to Unity Console", "WARNING: Can impact performance!"));
                            if (logToConsoleProp.boolValue)
                            {
                                EditorGUILayout.HelpBox("Console logging is enabled. This may impact frame rate during heavy logging!", MessageType.Warning);
                            }
                        }
                        
                        EditorGUILayout.Space(5);
                        EditorGUILayout.LabelField("Log Categories", EditorStyles.miniLabel);
                        
                        if (logValidationProp != null)
                            EditorGUILayout.PropertyField(logValidationProp, new GUIContent("Log Validation", "Log timecode validation rejections"));
                        
                        if (logJumpsProp != null)
                            EditorGUILayout.PropertyField(logJumpsProp, new GUIContent("Log Jumps", "Log timecode jumps"));
                        
                        if (logBufferIssuesProp != null)
                            EditorGUILayout.PropertyField(logBufferIssuesProp, new GUIContent("Log Buffer Issues", "Log audio buffer problems"));
                        
                        EditorGUILayout.Space(5);
                        
                        if (logThrottleIntervalProp != null)
                            EditorGUILayout.Slider(logThrottleIntervalProp, 0.1f, 5.0f, new GUIContent("Throttle Interval (sec)", "Minimum time between similar messages"));
                        
                        if (maxDebugLogsProp != null)
                            EditorGUILayout.PropertyField(maxDebugLogsProp, new GUIContent("Max Log Buffer", "Max logs kept in memory"));
                        
                        EditorGUI.indentLevel--;
                    }
                }
                    
                // Display debug logs if available
                if (logDebugInfoProp != null && logDebugInfoProp.boolValue && component.DebugLogs != null && component.DebugLogs.Count > 0)
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField($"Recent Logs (showing last 5 of {component.DebugLogs.Count}):", EditorStyles.boldLabel);
                    
                    int startIndex = Mathf.Max(0, component.DebugLogs.Count - 5);
                    for (int i = startIndex; i < component.DebugLogs.Count; i++)
                    {
                        // Parse log level from the log string to determine message type
                        MessageType msgType = MessageType.Info;
                        if (component.DebugLogs[i].Contains("[Error]"))
                            msgType = MessageType.Error;
                        else if (component.DebugLogs[i].Contains("[Warning]"))
                            msgType = MessageType.Warning;
                        
                        EditorGUILayout.HelpBox(component.DebugLogs[i], msgType);
                    }
                    
                    if (GUILayout.Button("Clear Logs"))
                    {
                        component.DebugLogs.Clear();
                    }
                }
                
                EditorGUILayout.EndVertical();
            }
            
            serializedObject.ApplyModifiedProperties();
            
            // Repaint continuously when recording for real-time updates
            if (component.IsRecording && Application.isPlaying)
            {
                Repaint();
            }
        }
        
        private void DrawLevelMeter(Rect rect, float level, string label, Color color)
        {
            // Background
            EditorGUI.DrawRect(rect, new Color(0.2f, 0.2f, 0.2f));
            
            // Level bar
            float width = rect.width * Mathf.Clamp01(level);
            Rect levelRect = new Rect(rect.x, rect.y, width, rect.height);
            
            // Gradient color based on level
            if (level > 0.9f)
                color = Color.red;
            else if (level > 0.7f)
                color = Color.yellow;
            
            EditorGUI.DrawRect(levelRect, color);
            
            // Draw dB scale marks
            for (float db = -60; db <= 0; db += 10)
            {
                float linear = Mathf.Pow(10, db / 20);
                float x = rect.x + rect.width * linear;
                EditorGUI.DrawRect(new Rect(x - 1, rect.y, 2, rect.height), Color.gray);
            }
            
            // Label
            GUI.Label(rect, $" {label}: {level * 100:F1}% ({20 * Mathf.Log10(Mathf.Max(0.0001f, level)):F1} dB)", EditorStyles.miniLabel);
        }
        
        private void DrawWaveform(Rect rect)
        {
            if (waveformTexture == null) return;
            
            // Clear texture
            Color[] clearColors = new Color[waveformTexture.width * waveformTexture.height];
            for (int i = 0; i < clearColors.Length; i++)
                clearColors[i] = waveformBackgroundColor;
            waveformTexture.SetPixels(clearColors);
            
            // Draw waveform
            float[] waveform = component.WaveformData;
            if (waveform != null && waveform.Length > 0)
            {
                int textureWidth = waveformTexture.width;
                int textureHeight = waveformTexture.height;
                int centerY = textureHeight / 2;
                
                for (int x = 0; x < textureWidth && x < waveform.Length; x++)
                {
                    float sample = waveform[x];
                    int height = Mathf.Clamp((int)(sample * centerY), -centerY + 1, centerY - 1);
                    
                    // Draw vertical line from center to sample height
                    if (height > 0)
                    {
                        for (int y = centerY; y <= centerY + height && y < textureHeight; y++)
                            waveformTexture.SetPixel(x, y, waveformColor);
                    }
                    else
                    {
                        for (int y = centerY + height; y <= centerY && y >= 0; y++)
                            waveformTexture.SetPixel(x, y, waveformColor);
                    }
                }
                
                // Draw center line
                for (int x = 0; x < textureWidth; x++)
                    waveformTexture.SetPixel(x, centerY, Color.gray);
            }
            
            waveformTexture.Apply();
            GUI.DrawTexture(rect, waveformTexture);
            
            // Draw border
            Handles.DrawSolidRectangleWithOutline(rect, Color.clear, Color.gray);
        }
        
        private void DrawJitterGraph(Rect rect)
        {
            DrawJitterGraph(rect, component.JitterHistory, jitterGraphColor);
        }
        
        private void DrawJitterGraph(Rect rect, System.Collections.Generic.Queue<float> jitterData, Color graphColor)
        {
            if (jitterGraphTexture == null) return;
            
            // Clear texture
            Color[] clearColors = new Color[jitterGraphTexture.width * jitterGraphTexture.height];
            for (int i = 0; i < clearColors.Length; i++)
                clearColors[i] = jitterGraphBackgroundColor;
            jitterGraphTexture.SetPixels(clearColors);
            
            var jitterHistory = jitterData;
            if (jitterHistory != null && jitterHistory.Count > 0)
            {
                int textureWidth = jitterGraphTexture.width;
                int textureHeight = jitterGraphTexture.height;
                
                // Convert queue to array
                float[] jitterArray = jitterHistory.ToArray();
                
                // Find max value for scaling
                float maxJitter = 0.05f; // 50ms default max scale
                foreach (float j in jitterArray)
                {
                    if (j > maxJitter) maxJitter = j;
                }
                
                // Draw grid lines
                Color gridColor = new Color(0.3f, 0.3f, 0.3f);
                
                // Horizontal grid lines (time values)
                for (int i = 1; i <= 5; i++)
                {
                    int y = (textureHeight * i) / 5;
                    for (int x = 0; x < textureWidth; x += 5)
                    {
                        jitterGraphTexture.SetPixel(x, y, gridColor);
                    }
                }
                
                // Draw threshold line at 100ms
                float thresholdY = (0.1f / maxJitter) * textureHeight;
                if (thresholdY < textureHeight)
                {
                    Color thresholdColor = new Color(0.8f, 0.2f, 0.2f, 0.5f);
                    for (int x = 0; x < textureWidth; x++)
                    {
                        jitterGraphTexture.SetPixel(x, (int)thresholdY, thresholdColor);
                    }
                }
                
                // Draw jitter values
                for (int i = 0; i < jitterArray.Length && i < textureWidth; i++)
                {
                    float normalizedJitter = jitterArray[i] / maxJitter;
                    int height = Mathf.Clamp((int)(normalizedJitter * textureHeight), 0, textureHeight - 1);
                    
                    // Choose color based on jitter value
                    Color barColor = graphColor;
                    if (jitterArray[i] > 0.1f) // > 100ms
                        barColor = Color.red;
                    else if (jitterArray[i] > 0.05f) // > 50ms
                        barColor = Color.yellow;
                    else if (jitterArray[i] > 0.01f) // > 10ms
                        barColor = graphColor;
                    else
                        barColor = Color.green;
                    
                    // Draw vertical bar from bottom to height
                    int xPos = (i * textureWidth) / jitterArray.Length;
                    for (int y = 0; y <= height; y++)
                    {
                        jitterGraphTexture.SetPixel(xPos, y, barColor);
                        if (xPos + 1 < textureWidth)
                            jitterGraphTexture.SetPixel(xPos + 1, y, barColor);
                    }
                }
                
                // Add scale labels (overlay text on GUI after texture)
                jitterGraphTexture.Apply();
                GUI.DrawTexture(rect, jitterGraphTexture);
                
                // Draw scale labels
                GUI.Label(new Rect(rect.x + 5, rect.y + 5, 100, 20), $"Max: {maxJitter * 1000:F1}ms", EditorStyles.miniLabel);
                GUI.Label(new Rect(rect.x + 5, rect.y + rect.height - 20, 100, 20), "0ms", EditorStyles.miniLabel);
                
                // Draw sample count
                GUI.Label(new Rect(rect.x + rect.width - 100, rect.y + 5, 95, 20), 
                    $"Samples: {jitterArray.Length}", EditorStyles.miniLabel);
            }
            else
            {
                jitterGraphTexture.Apply();
                GUI.DrawTexture(rect, jitterGraphTexture);
                GUI.Label(rect, "No jitter data available", EditorStyles.centeredGreyMiniLabel);
            }
            
            // Draw border
            Handles.DrawSolidRectangleWithOutline(rect, Color.clear, Color.gray);
        }
        
        private void InitializeStyles()
        {
            if (timecodeStyle == null)
            {
                timecodeStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 28,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal = { textColor = Color.white }
                };
            }
            
            if (statusStyle == null)
            {
                statusStyle = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = FontStyle.Bold
                };
            }
            
            if (levelMeterStyle == null)
            {
                levelMeterStyle = new GUIStyle(EditorStyles.label)
                {
                    fontSize = 10,
                    normal = { textColor = Color.white }
                };
            }
        }
    }
}