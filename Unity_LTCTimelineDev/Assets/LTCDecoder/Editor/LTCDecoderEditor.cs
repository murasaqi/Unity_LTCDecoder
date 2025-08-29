using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;

namespace LTC.Timeline
{
    [CustomEditor(typeof(LTCDecoder))]
    public class LTCDecoderEditor : Editor
    {
        private LTCDecoder component;
        private int selectedDeviceIndex = -1;
        private string[] deviceOptions;
        private bool showAdvancedSettings = false;
        private bool showNoiseAnalysis = true;  // ノイズ解析グラフの表示状態
        private float graphTimeWindow = 5.0f; // グラフの表示時間範囲（秒）
        
        // スタイル
        private GUIStyle titleStyle;
        private GUIStyle timecodeStyle;
        private GUIStyle statusStyle;
        
        // グラフ描画最適化用
        private float lastRepaintTime = 0f;
        private const float RepaintInterval = 0.1f;  // 100ms間隔
        private Vector3[] graphPointsCache = new Vector3[50];  // 事前確保
        
        // FPS計測用
        private float fpsUpdateInterval = 0.5f;  // 0.5秒ごとに更新
        private float fpsAccumulator = 0f;
        private int fpsFrameCount = 0;
        private float currentFps = 0f;
        private float lastFpsUpdateTime = 0f;
        
        private void OnEnable()
        {
            component = (LTCDecoder)target;
            RefreshDeviceList();
            
            // 設定の復元
            showNoiseAnalysis = EditorPrefs.GetBool("LTCDecoder.ShowNoiseAnalysis", true);
            graphTimeWindow = EditorPrefs.GetFloat("LTCDecoder.GraphTimeWindow", 5.0f);
            showAdvancedSettings = EditorPrefs.GetBool("LTCDecoder.ShowAdvancedSettings", false);
        }
        
        private void OnDisable()
        {
            // 設定の保存
            EditorPrefs.SetBool("LTCDecoder.ShowNoiseAnalysis", showNoiseAnalysis);
            EditorPrefs.SetFloat("LTCDecoder.GraphTimeWindow", graphTimeWindow);
            EditorPrefs.SetBool("LTCDecoder.ShowAdvancedSettings", showAdvancedSettings);
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
                // 固定幅フォントを使用してTCの幅を一定にする
                timecodeStyle = new GUIStyle(EditorStyles.label);
                
                // Unityのビルトインmonospaceフォントを使用
                Font monoFont = Font.CreateDynamicFontFromOSFont(new string[] { 
                    "Consolas",           // Windows
                    "Monaco",             // macOS  
                    "Courier New",        // Fallback
                    "Lucida Console",     // Fallback
                    "monospace"           // Generic fallback
                }, 20);
                
                if (monoFont != null)
                {
                    timecodeStyle.font = monoFont;
                }
                
                timecodeStyle.fontSize = 20;
                timecodeStyle.fontStyle = FontStyle.Bold;
                timecodeStyle.alignment = TextAnchor.MiddleCenter;
                timecodeStyle.fixedWidth = 240; // TC表示用の固定幅
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
            
            // FPS計測（Play Mode時のみ）
            UpdateFPSMeasurement();
            
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
            
            // ノイズ解析グラフ
            DrawNoiseAnalysisGraph();
            
            EditorGUILayout.Space(10);
            
            // 詳細設定
            DrawAdvancedSettings();
            
            serializedObject.ApplyModifiedProperties();
            
            // リアルタイム更新（レート制限付き、グラフ表示時のみ）
            if (Application.isPlaying && component.IsRecording && showNoiseAnalysis)
            {
                float currentTime = (float)EditorApplication.timeSinceStartup;
                if (currentTime - lastRepaintTime > RepaintInterval)
                {
                    lastRepaintTime = currentTime;
                    Repaint();
                }
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
            
            // Signal状態 (固定高さ)
            EditorGUILayout.BeginHorizontal(GUILayout.Height(18));
            if (component.IsRecording)
            {
                EditorGUILayout.LabelField("Signal:", GUILayout.Width(70));
                GUI.color = component.HasSignal ? Color.green : Color.yellow;
                EditorGUILayout.LabelField(component.HasSignal ? "● Detected" : "● No Signal", GUILayout.Width(80));
                GUI.color = originalColor;
                
                if (component.HasSignal)
                {
                    EditorGUILayout.LabelField($"Level: {(component.SignalLevel * 100):F1}%");
                }
                else
                {
                    EditorGUILayout.LabelField(""); // 空白で幅を維持
                }
            }
            else
            {
                EditorGUILayout.LabelField(""); // Recording停止時も高さを維持
            }
            EditorGUILayout.EndHorizontal();
            
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
            
            // Status Info (固定高さ)
            EditorGUILayout.BeginVertical(GUILayout.Height(40));
            
            // Drop Frame と Time Difference を1行にまとめて表示
            string statusInfo = "";
            
            if (component.DropFrame)
            {
                statusInfo = "Drop Frame Mode";
            }
            
            if (Mathf.Abs(component.TimeDifference) > 0.001f)
            {
                string diffText = component.TimeDifference > 0 
                    ? $"Internal: +{component.TimeDifference:F3}s" 
                    : $"Internal: {component.TimeDifference:F3}s";
                
                if (!string.IsNullOrEmpty(statusInfo))
                    statusInfo += " | ";
                statusInfo += diffText;
            }
            
            if (!string.IsNullOrEmpty(statusInfo))
            {
                MessageType msgType = Mathf.Abs(component.TimeDifference) > 0.1f 
                    ? MessageType.Warning 
                    : MessageType.Info;
                EditorGUILayout.HelpBox(statusInfo, msgType);
            }
            else
            {
                // 空の場合でも高さを維持
                GUILayout.FlexibleSpace();
            }
            
            EditorGUILayout.EndVertical();
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
            
            // 状態説明 (固定高さ)
            EditorGUILayout.BeginVertical(GUILayout.Height(30));
            string description = GetStateDescription(component.State);
            EditorGUILayout.HelpBox(description, MessageType.None);
            EditorGUILayout.EndVertical();
            
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
        
        private void DrawNoiseAnalysisGraph()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // ヘッダーとフレームレート表示（Foldout付き）
            EditorGUILayout.BeginHorizontal();
            
            // Foldoutとタイトル
            showNoiseAnalysis = EditorGUILayout.Foldout(showNoiseAnalysis, "Noise Analysis Graph", true);
            
            GUILayout.FlexibleSpace();
            
            // Unityのフレームレート表示（常に表示）
            DrawFPSDisplay();
            
            EditorGUILayout.EndHorizontal();
            
            // グラフが折りたたまれている場合は早期リターン
            if (!showNoiseAnalysis)
            {
                EditorGUILayout.EndVertical();
                return;
            }
            
            if (!Application.isPlaying || !component.IsRecording)
            {
                EditorGUILayout.HelpBox("Start recording to see noise analysis", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }
            
            // 時間範囲設定
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Time Window:", GUILayout.Width(85));
            graphTimeWindow = EditorGUILayout.Slider(graphTimeWindow, 1.0f, 10.0f);
            EditorGUILayout.LabelField("seconds", GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();
            
            // グラフ描画エリア
            Rect graphRect = GUILayoutUtility.GetRect(400, 150);
            GUI.Box(graphRect, GUIContent.none);
            
            // データ取得
            float[] ltcNoise = component.LTCNoiseHistory;
            float[] internalNoise = component.InternalNoiseHistory;
            int currentIndex = component.NoiseHistoryCurrentIndex;
            int historySize = component.NoiseHistoryMaxSize;
            
            if (ltcNoise == null || internalNoise == null)
            {
                EditorGUILayout.EndVertical();
                return;
            }
            
            // グラフのパディング
            float padding = 10f;
            float graphWidth = graphRect.width - padding * 2;
            float graphHeight = graphRect.height - padding * 2;
            float graphLeft = graphRect.x + padding;
            float graphTop = graphRect.y + padding;
            
            // 背景グリッド描画
            Handles.color = new Color(0.5f, 0.5f, 0.5f, 0.2f);
            for (int i = 0; i <= 4; i++)
            {
                float y = graphTop + (graphHeight * i / 4);
                Handles.DrawLine(new Vector3(graphLeft, y, 0), new Vector3(graphLeft + graphWidth, y, 0));
            }
            
            // Y軸ラベル（ノイズレベル）
            GUIStyle smallLabel = new GUIStyle(EditorStyles.miniLabel);
            smallLabel.alignment = TextAnchor.MiddleRight;
            
            // より適切な幅を設定
            float labelWidth = 35f;
            
            // 上から順に100%, 75%, 50%, 25%, 0%
            GUI.Label(new Rect(graphRect.x + padding - labelWidth - 2, graphTop - 5, labelWidth, 10), 
                "100%", smallLabel);
            GUI.Label(new Rect(graphRect.x + padding - labelWidth - 2, graphTop + graphHeight * 0.25f - 5, labelWidth, 10), 
                "75%", smallLabel);
            GUI.Label(new Rect(graphRect.x + padding - labelWidth - 2, graphTop + graphHeight * 0.5f - 5, labelWidth, 10), 
                "50%", smallLabel);
            GUI.Label(new Rect(graphRect.x + padding - labelWidth - 2, graphTop + graphHeight * 0.75f - 5, labelWidth, 10), 
                "25%", smallLabel);
            GUI.Label(new Rect(graphRect.x + padding - labelWidth - 2, graphTop + graphHeight - 5, labelWidth, 10), 
                "0%", smallLabel);
            
            // Y軸タイトル（縦書き風）
            GUIStyle verticalLabel = new GUIStyle(EditorStyles.miniLabel);
            verticalLabel.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(graphRect.x - 5, graphTop + graphHeight/2 - 30, 20, 60), 
                "Noise\nLevel", verticalLabel);
            
            // X軸ラベル（時間）
            GUIStyle centerLabel = new GUIStyle(EditorStyles.miniLabel);
            centerLabel.alignment = TextAnchor.MiddleCenter;
            
            // 左端（過去）
            GUI.Label(new Rect(graphLeft - 10, graphTop + graphHeight + 2, 40, 10), 
                $"-{graphTimeWindow:F1}s", centerLabel);
            
            // 中央
            GUI.Label(new Rect(graphLeft + graphWidth/2 - 20, graphTop + graphHeight + 2, 40, 10), 
                $"-{graphTimeWindow/2:F1}s", centerLabel);
            
            // 右端（現在）
            GUI.Label(new Rect(graphLeft + graphWidth - 30, graphTop + graphHeight + 2, 40, 10), 
                "Now", centerLabel);
            
            // 実際の時間範囲を計算
            float sampleInterval = component.NoiseHistorySampleInterval;
            float actualTimeRange = historySize * sampleInterval;  // 実際にカバーしている時間
            int samplesToDisplay = Mathf.Min(historySize, (int)(graphTimeWindow / sampleInterval));
            
            // グラフ描画
            if (Event.current.type == EventType.Repaint)
            {
                // 表示するサンプル数を時間範囲に基づいて計算
                int displaySamples = Mathf.Min(historySize, samplesToDisplay);
                
                // LTCノイズ（赤）
                DrawNoiseGraph(ltcNoise, currentIndex, displaySamples, graphLeft, graphTop, graphWidth, graphHeight, 
                    new Color(1f, 0.3f, 0.3f, 0.8f), true);
                
                // Internal TCノイズ（緑）
                DrawNoiseGraph(internalNoise, currentIndex, displaySamples, graphLeft, graphTop, graphWidth, graphHeight, 
                    new Color(0.3f, 1f, 0.3f, 0.8f), false);
            }
            
            // 凡例
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            
            Color originalColor = GUI.color;
            GUI.color = new Color(1f, 0.3f, 0.3f);
            EditorGUILayout.LabelField("━ LTC Noise", GUILayout.Width(80));
            
            GUI.color = new Color(0.3f, 1f, 0.3f);
            EditorGUILayout.LabelField("━ Internal TC Noise", GUILayout.Width(120));
            
            GUI.color = originalColor;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            
            // 統計情報
            
            float ltcAvgNoise = CalculateAverageNoise(ltcNoise, currentIndex, samplesToDisplay);
            float internalAvgNoise = CalculateAverageNoise(internalNoise, currentIndex, samplesToDisplay);
            
            // パーセント表示に変換
            float ltcNoisePercent = ltcAvgNoise * 100f;
            float internalNoisePercent = internalAvgNoise * 100f;
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"LTC Avg: {ltcNoisePercent:F1}%", GUILayout.Width(120));
            EditorGUILayout.LabelField($"Internal Avg: {internalNoisePercent:F1}%", GUILayout.Width(120));
            
            string stability = "";
            Color stabilityColor = Color.white;
            if (ltcAvgNoise < 0.1f && internalAvgNoise < 0.01f)
            {
                stability = "● Excellent";
                stabilityColor = Color.green;
            }
            else if (ltcAvgNoise < 0.3f && internalAvgNoise < 0.05f)
            {
                stability = "● Good";
                stabilityColor = new Color(0.5f, 1f, 0.5f);
            }
            else if (ltcAvgNoise < 0.5f && internalAvgNoise < 0.1f)
            {
                stability = "● Fair";
                stabilityColor = Color.yellow;
            }
            else
            {
                stability = "● Poor";
                stabilityColor = Color.red;
            }
            
            Color originalStabilityColor = GUI.color;
            GUI.color = stabilityColor;
            EditorGUILayout.LabelField($"Stability: {stability}", GUILayout.Width(120));
            GUI.color = originalStabilityColor;
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawNoiseGraph(float[] data, int currentIndex, int dataSize, 
            float x, float y, float width, float height, Color color, bool drawArea)
        {
            if (data == null || dataSize <= 0) return;
            
            // 間引きサンプリング（100ポイント→50ポイント）
            const int samplePoints = 50;
            int step = Mathf.Max(1, dataSize / samplePoints);
            
            int pointCount = 0;
            for (int i = 0; i < dataSize && pointCount < samplePoints; i += step)
            {
                // 循環バッファからデータを取得（最新のデータが右側）
                int dataIndex = (currentIndex - dataSize + i + dataSize) % dataSize;
                float value = Mathf.Clamp01(data[dataIndex]);
                
                float px = x + (width * pointCount / (samplePoints - 1));
                float py = y + height * (1f - value);
                
                graphPointsCache[pointCount] = new Vector3(px, py, 0);
                pointCount++;
            }
            
            // Handlesでライン描画（軽量版）
            if (pointCount > 1)
            {
                Handles.color = color;
                Handles.DrawPolyLine(graphPointsCache[0..pointCount]);
            }
        }
        
        private void UpdateFPSMeasurement()
        {
            if (!Application.isPlaying)
            {
                currentFps = 0f;
                fpsFrameCount = 0;
                fpsAccumulator = 0f;
                lastFpsUpdateTime = 0f;
                return;
            }
            
            // シンプルなアプローチ: Time.deltaTimeを使用
            fpsFrameCount++;
            fpsAccumulator += Time.deltaTime;
            
            // 指定間隔でFPS更新
            if (fpsAccumulator >= fpsUpdateInterval)
            {
                currentFps = fpsFrameCount / fpsAccumulator;
                fpsFrameCount = 0;
                fpsAccumulator = 0f;
            }
        }
        
        private void DrawFPSDisplay()
        {
            Color originalColor = GUI.color;
            
            string fpsText;
            if (Application.isPlaying)
            {
                // Play Mode: 実測値を表示
                if (currentFps >= 60) GUI.color = Color.green;
                else if (currentFps >= 30) GUI.color = Color.yellow;
                else GUI.color = Color.red;
                fpsText = $"Unity FPS: {currentFps:F1}";
            }
            else
            {
                // Editor Mode: 非表示
                GUI.color = Color.gray;
                fpsText = "Unity FPS: --";
            }
            
            EditorGUILayout.LabelField(fpsText, GUILayout.Width(100));
            GUI.color = originalColor;
        }
        
        private float CalculateAverageNoise(float[] data, int currentIndex, int samples)
        {
            if (data == null || data.Length == 0) return 0f;
            
            float sum = 0f;
            int count = Mathf.Min(samples, data.Length);
            
            for (int i = 0; i < count; i++)
            {
                int index = (currentIndex - i + data.Length) % data.Length;
                sum += data[index];
            }
            
            return sum / count;
        }
        
        private void DrawAdvancedSettings()
        {
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Settings", true);
            
            if (showAdvancedSettings)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Sync Settings
                EditorGUILayout.LabelField("Sync Parameters", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("bufferQueueSize"), 
                    new GUIContent("LTC Buffer Queue Size", "Number of LTC samples to buffer for analysis"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("syncThreshold"),
                    new GUIContent("Sync Threshold (sec)", "Time difference to trigger synchronization"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("jumpThreshold"),
                    new GUIContent("Jump Detection Threshold (sec)", "Time difference to detect intentional jumps"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("stopThreshold"),
                    new GUIContent("Stop Detection Threshold (sec)", "Minimum change to detect LTC is running"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("driftCorrection"),
                    new GUIContent("Drift Correction Strength", "How strongly to correct clock drift (0.0-1.0)"));
                
                EditorGUILayout.Space(5);
                
                // Signal Detection
                EditorGUILayout.LabelField("Signal Detection", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("signalThreshold"),
                    new GUIContent("Signal Level Threshold", "Minimum audio level to detect LTC signal"));
                
                EditorGUILayout.Space(5);
                
                // Frame Rate
                EditorGUILayout.LabelField("LTC Format", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("frameRate"),
                    new GUIContent("LTC Signal Frame Rate (fps)", "Frame rate of incoming LTC signal (24/25/29.97/30 fps)"));
                
                EditorGUILayout.Space(5);
                
                // Audio Settings
                EditorGUILayout.LabelField("Audio Settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("sampleRate"),
                    new GUIContent("Audio Sample Rate (Hz)", "Sample rate for audio input"));
                EditorGUILayout.PropertyField(serializedObject.FindProperty("bufferSize"),
                    new GUIContent("Audio Buffer Size (samples)", "Size of audio processing buffer"));
                
                EditorGUILayout.Space(5);
                
                // Debug
                EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("enableDebugLogging"),
                    new GUIContent("Enable Debug Logging", "Output detailed debug information to console"));
                
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
                    return "Unknown state";
            }
        }
    }
}