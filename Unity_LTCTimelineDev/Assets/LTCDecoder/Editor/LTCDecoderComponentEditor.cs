using UnityEngine;
using UnityEditor;
using System.Linq;

namespace LTC.Timeline
{
    [CustomEditor(typeof(LTCDecoderComponent))]
    public class LTCDecoderComponentEditor : Editor
    {
        private LTCDecoderComponent component;
        private GUIStyle timecodeStyle;
        private GUIStyle statusStyle;
        private GUIStyle levelMeterStyle;
        private int selectedDeviceIndex = -1;
        private string[] deviceOptions;
        private bool showAdvancedSettings = false;
        private bool showAudioMonitoring = true;
        private Texture2D waveformTexture;
        private Color waveformColor = new Color(0.2f, 0.8f, 0.2f);
        private Color waveformBackgroundColor = new Color(0.1f, 0.1f, 0.1f);
        
        private void OnEnable()
        {
            component = (LTCDecoderComponent)target;
            RefreshDeviceList();
            waveformTexture = new Texture2D(512, 100);
        }
        
        private void OnDisable()
        {
            if (waveformTexture != null)
            {
                DestroyImmediate(waveformTexture);
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
            
            // Timecode Display Section
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Timecode Output", EditorStyles.boldLabel);
            
            // Large timecode display
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            Color originalColor = GUI.color;
            if (component.HasSignal)
            {
                GUI.color = Color.green;
            }
            else
            {
                GUI.color = Color.gray;
            }
            
            EditorGUILayout.LabelField(component.CurrentTimecode, timecodeStyle, GUILayout.Height(40));
            
            GUI.color = originalColor;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
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
            
            EditorGUILayout.EndVertical();
            
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