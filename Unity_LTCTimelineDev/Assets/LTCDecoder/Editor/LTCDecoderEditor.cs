using UnityEngine;
using UnityEditor;
using System.Linq;

namespace LTC.Timeline
{
    [CustomEditor(typeof(LTCDecoder))]
    public class LTCDecoderEditor : Editor
    {
        private LTCDecoder component;
        private int selectedDeviceIndex = -1;
        private string[] deviceOptions;
        private bool showAdvancedSettings = false;
        
        // スタイル
        private GUIStyle titleStyle;
        private GUIStyle timecodeStyle;
        private GUIStyle statusStyle;
        
        private void OnEnable()
        {
            component = (LTCDecoder)target;
            RefreshDeviceList();
        }
        
        private void RefreshDeviceList()
        {
            var devices = component.AvailableDevices;
            if (devices == null || devices.Length == 0)
            {
                deviceOptions = new string[] { "No devices found" };
                selectedDeviceIndex = 0;
            }
            else
            {
                deviceOptions = devices;
                
                for (int i = 0; i < devices.Length; i++)
                {
                    if (devices[i] == component.SelectedDevice)
                    {
                        selectedDeviceIndex = i;
                        break;
                    }
                }
                
                if (selectedDeviceIndex == -1 && devices.Length > 0)
                {
                    selectedDeviceIndex = 0;
                }
            }
        }
        
        private void InitializeStyles()
        {
            if (titleStyle == null)
            {
                titleStyle = new GUIStyle(EditorStyles.boldLabel);
                titleStyle.fontSize = 14;
                titleStyle.alignment = TextAnchor.MiddleCenter;
            }
            
            if (timecodeStyle == null)
            {
                timecodeStyle = new GUIStyle(EditorStyles.largeLabel);
                timecodeStyle.fontSize = 20;
                timecodeStyle.fontStyle = FontStyle.Bold;
                timecodeStyle.alignment = TextAnchor.MiddleCenter;
            }
            
            if (statusStyle == null)
            {
                statusStyle = new GUIStyle(EditorStyles.label);
                statusStyle.alignment = TextAnchor.MiddleCenter;
            }
        }
        
        public override void OnInspectorGUI()
        {
            InitializeStyles();
            serializedObject.Update();
            
            // タイトル
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("LTC Decoder - DSP Clock Based", titleStyle);
            EditorGUILayout.Space(5);
            
            // デバイス選択
            DrawDeviceSelection();
            
            EditorGUILayout.Space(10);
            
            // タイムコード表示
            DrawTimecodeDisplay();
            
            EditorGUILayout.Space(10);
            
            // 同期状態
            DrawSyncStatus();
            
            EditorGUILayout.Space(10);
            
            // 詳細設定
            DrawAdvancedSettings();
            
            serializedObject.ApplyModifiedProperties();
            
            // リアルタイム更新
            if (Application.isPlaying && component.IsRecording)
            {
                Repaint();
            }
        }
        
        private void DrawDeviceSelection()
        {
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
            
            // Recording状態
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Recording:", GUILayout.Width(70));
            
            Color originalColor = GUI.color;
            GUI.color = component.IsRecording ? Color.green : Color.red;
            EditorGUILayout.LabelField(component.IsRecording ? "● Active" : "● Stopped");
            GUI.color = originalColor;
            
            EditorGUILayout.EndHorizontal();
            
            // Signal状態
            if (component.IsRecording)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Signal:", GUILayout.Width(70));
                GUI.color = component.HasSignal ? Color.green : Color.yellow;
                EditorGUILayout.LabelField(component.HasSignal ? "● Detected" : "● No Signal");
                GUI.color = originalColor;
                
                if (component.HasSignal)
                {
                    EditorGUILayout.LabelField($"Level: {(component.SignalLevel * 100):F1}%");
                }
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawTimecodeDisplay()
        {
            EditorGUILayout.LabelField("Timecode Display", EditorStyles.boldLabel);
            
            // 内部TC（DSPクロック）
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = new Color(0.9f, 1.0f, 0.9f);
            
            EditorGUILayout.LabelField("Internal TC (DSP Clock)", statusStyle);
            
            Color originalColor = GUI.color;
            GUI.color = GetStateColor(component.State);
            EditorGUILayout.LabelField(component.CurrentTimecode, timecodeStyle, GUILayout.Height(30));
            GUI.color = originalColor;
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
            
            // デコードされたTC
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = new Color(1.0f, 0.95f, 0.9f);
            
            EditorGUILayout.LabelField("Decoded LTC", statusStyle);
            EditorGUILayout.LabelField(component.DecodedTimecode, timecodeStyle, GUILayout.Height(30));
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
            
            // Drop Frame表示
            if (component.DropFrame)
            {
                EditorGUILayout.HelpBox("Drop Frame Mode", MessageType.Info);
            }
            
            // 時間差
            if (Mathf.Abs(component.TimeDifference) > 0.001f)
            {
                string diffText = component.TimeDifference > 0 
                    ? $"Internal is {component.TimeDifference:F3}s ahead" 
                    : $"Internal is {-component.TimeDifference:F3}s behind";
                    
                MessageType msgType = Mathf.Abs(component.TimeDifference) > 0.1f 
                    ? MessageType.Warning 
                    : MessageType.Info;
                    
                EditorGUILayout.HelpBox(diffText, msgType);
            }
        }
        
        private void DrawSyncStatus()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Sync Status", EditorStyles.boldLabel);
            
            // 状態表示
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("State:", GUILayout.Width(50));
            
            Color originalColor = GUI.color;
            GUI.color = GetStateColor(component.State);
            
            string stateText = GetStateText(component.State);
            EditorGUILayout.LabelField(stateText, EditorStyles.boldLabel);
            
            GUI.color = originalColor;
            EditorGUILayout.EndHorizontal();
            
            // 状態説明
            string description = GetStateDescription(component.State);
            if (!string.IsNullOrEmpty(description))
            {
                EditorGUILayout.HelpBox(description, MessageType.None);
            }
            
            // アクションボタン
            EditorGUILayout.BeginHorizontal();
            
            if (GUILayout.Button("Reset"))
            {
                component.ResetStatistics();
            }
            
            if (GUILayout.Button("Manual Sync to Decoded"))
            {
                component.ManualSync(component.DecodedTimecode);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawAdvancedSettings()
        {
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Settings", true);
            
            if (showAdvancedSettings)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Sync Settings
                EditorGUILayout.LabelField("Sync Parameters", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("bufferQueueSize"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("syncThreshold"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpThreshold"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("stopThreshold"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("driftCorrection"));
                
                EditorGUILayout.Space(5);
                
                // Signal Detection
                EditorGUILayout.LabelField("Signal Detection", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("signalThreshold"));
                
                EditorGUILayout.Space(5);
                
                // Frame Rate
                EditorGUILayout.LabelField("Frame Rate", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("frameRate"));
                
                EditorGUILayout.Space(5);
                
                // Audio Settings
                EditorGUILayout.LabelField("Audio Settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sampleRate"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("bufferSize"));
                
                EditorGUILayout.Space(5);
                
                // Debug
                EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("enableDebugLogging"));
                
                EditorGUILayout.EndVertical();
            }
        }
        
        private Color GetStateColor(LTCDecoder.SyncState state)
        {
            switch (state)
            {
                case LTCDecoder.SyncState.NoSignal:
                    return Color.gray;
                case LTCDecoder.SyncState.Syncing:
                    return Color.yellow;
                case LTCDecoder.SyncState.Locked:
                    return Color.green;
                case LTCDecoder.SyncState.Drifting:
                    return new Color(1f, 0.5f, 0f); // Orange
                default:
                    return Color.white;
            }
        }
        
        private string GetStateText(LTCDecoder.SyncState state)
        {
            switch (state)
            {
                case LTCDecoder.SyncState.NoSignal:
                    return "● No Signal";
                case LTCDecoder.SyncState.Syncing:
                    return "● Syncing...";
                case LTCDecoder.SyncState.Locked:
                    return "● Locked";
                case LTCDecoder.SyncState.Drifting:
                    return "● Drifting";
                default:
                    return "● Unknown";
            }
        }
        
        private string GetStateDescription(LTCDecoder.SyncState state)
        {
            switch (state)
            {
                case LTCDecoder.SyncState.NoSignal:
                    return "No LTC signal detected. Check audio input.";
                case LTCDecoder.SyncState.Syncing:
                    return "Collecting LTC samples for analysis...";
                case LTCDecoder.SyncState.Locked:
                    return "Synchronized with LTC. Internal clock is running.";
                case LTCDecoder.SyncState.Drifting:
                    return "Drift detected. Applying correction...";
                default:
                    return "";
            }
        }
    }
}