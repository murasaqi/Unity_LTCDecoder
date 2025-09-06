using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;

namespace jp.iridescent.ltcdecoder.Editor
{
    [CustomEditor(typeof(LTCDecoder))]
    public class LTCDecoderEditor : UnityEditor.Editor
    {
        private LTCDecoder component;
        private int selectedDeviceIndex = -1;
        private string[] deviceOptions;
        private bool showAdvancedSettings = false;
        private bool advancedEditMode = false;  // Advanced Settings編集モード
        private bool showNoiseAnalysis = false;  // ノイズ解析グラフの表示状態（デフォルト非表示）
        private bool showDebugInfo = false;  // Debug Info折りたたみ状態
        private float graphTimeWindow = 5.0f; // グラフの表示時間範囲（秒）
        
        /// <summary>
        /// PlayMode終了時のInspectorリフレッシュ設定（静的初期化）
        /// </summary>
        [InitializeOnLoadMethod]
        static void SetupPlayModeInspectorRefresh()
        {
            EditorApplication.playModeStateChanged += (state) =>
            {
                if (state == PlayModeStateChange.EnteredEditMode)
                {
                    // InspectorWindowを強制的に再描画
                    EditorApplication.delayCall += () =>
                    {
                        // すべてのInspectorWindowを取得して再描画
                        var inspectorType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
                        if (inspectorType != null)
                        {
                            var windows = Resources.FindObjectsOfTypeAll(inspectorType);
                            foreach (var window in windows)
                            {
                                var editorWindow = window as EditorWindow;
                                if (editorWindow != null)
                                {
                                    editorWindow.Repaint();
                                    Debug.Log("[LTCDecoderEditor] Inspector refreshed after Play Mode");
                                }
                            }
                        }
                        
                        // LTCDecoderが選択されている場合は再選択して確実に更新
                        if (Selection.activeGameObject != null)
                        {
                            var decoder = Selection.activeGameObject.GetComponent<LTCDecoder>();
                            if (decoder != null)
                            {
                                var tempSelection = Selection.activeGameObject;
                                Selection.activeGameObject = null;
                                EditorApplication.delayCall += () =>
                                {
                                    Selection.activeGameObject = tempSelection;
                                };
                            }
                        }
                    };
                }
            };
        }
        
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
            showNoiseAnalysis = EditorPrefs.GetBool("LTCDecoder.ShowNoiseAnalysis", false);
            showDebugInfo = EditorPrefs.GetBool("LTCDecoder.ShowDebugInfo", false);
            graphTimeWindow = EditorPrefs.GetFloat("LTCDecoder.GraphTimeWindow", 5.0f);
            showAdvancedSettings = EditorPrefs.GetBool("LTCDecoder.ShowAdvancedSettings", false);
            advancedEditMode = EditorPrefs.GetBool("LTCDecoder.AdvancedEditMode", false);
            
            // Play終了後の再初期化
            if (!Application.isPlaying)
            {
                // SerializedObjectを再取得して最新状態を反映
                serializedObject.Update();
            }
        }
        
        private void OnDisable()
        {
            // 設定の保存
            EditorPrefs.SetBool("LTCDecoder.ShowNoiseAnalysis", showNoiseAnalysis);
            EditorPrefs.SetBool("LTCDecoder.ShowDebugInfo", showDebugInfo);
            EditorPrefs.SetFloat("LTCDecoder.GraphTimeWindow", graphTimeWindow);
            EditorPrefs.SetBool("LTCDecoder.ShowAdvancedSettings", showAdvancedSettings);
            EditorPrefs.SetBool("LTCDecoder.AdvancedEditMode", advancedEditMode);
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
            
            // 1. 基本設定セクション
            DrawBasicSettings();
            
            EditorGUILayout.Space(10);
            
            // 2. ステータス & タイムコード表示セクション（統合）
            DrawStatusAndTimecodeSection();
            
            EditorGUILayout.Space(10);
            
            // 3. イベント設定セクション
            DrawEventSettings();
            
            EditorGUILayout.Space(10);
            
            // 4. 詳細設定セクション
            DrawAdvancedSettings();
            
            EditorGUILayout.Space(10);
            
            // 5. Debug Toolsセクション
            DrawDebugTools();
            
            serializedObject.ApplyModifiedProperties();
            
            // リアルタイム更新（レート制限付き）
            if (Application.isPlaying && component.IsRecording)
            {
                // グラフまたはDebug Infoが表示されている時のみ高頻度更新
                if (showNoiseAnalysis || showDebugInfo)
                {
                    float currentTime = (float)EditorApplication.timeSinceStartup;
                    if (currentTime - lastRepaintTime > RepaintInterval)
                    {
                        lastRepaintTime = currentTime;
                        Repaint();
                    }
                }
                else
                {
                    // Output TCのみの場合は低頻度更新
                    float currentTime = (float)EditorApplication.timeSinceStartup;
                    if (currentTime - lastRepaintTime > 0.5f) // 500ms間隔
                    {
                        lastRepaintTime = currentTime;
                        Repaint();
                    }
                }
            }
        }
        
        private void DrawBasicSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Audio Input Settings", EditorStyles.boldLabel);
            
            // デバイス選択
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
            
            // LTC Frame Rate
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("LTC Settings", EditorStyles.boldLabel);
            
            var ltcFrameRateProp = serializedObject.FindProperty("ltcFrameRate");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(ltcFrameRateProp,
                new GUIContent("LTC Frame Rate", "Linear Timecode frame rate standard"));
            if (EditorGUI.EndChangeCheck() && Application.isPlaying)
            {
                serializedObject.ApplyModifiedProperties();
                component.SetLTCFrameRate((LTCDecoder.LTCFrameRate)ltcFrameRateProp.enumValueIndex);
            }
            
            // Sample Rate with dropdown
            EditorGUILayout.Space(5);
            int[] sampleRates = { 44100, 48000, 96000 };
            string[] sampleRateOptions = { "44100 Hz", "48000 Hz", "96000 Hz" };
            
            var sampleRateProp = serializedObject.FindProperty("sampleRate");
            int currentSampleRate = sampleRateProp.intValue;
            int currentIndex = System.Array.IndexOf(sampleRates, currentSampleRate);
            if (currentIndex == -1) currentIndex = 1; // Default to 48000
            
            EditorGUI.BeginChangeCheck();
            int newSampleIndex = EditorGUILayout.Popup("Sample Rate", currentIndex, sampleRateOptions);
            if (EditorGUI.EndChangeCheck())
            {
                sampleRateProp.intValue = sampleRates[newSampleIndex];
                serializedObject.ApplyModifiedProperties();
                if (Application.isPlaying)
                {
                    component.SetSampleRate(sampleRates[newSampleIndex]);
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawStatusAndTimecodeSection()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("LTC Status & Output", EditorStyles.boldLabel);
            
            // Recording & Signal 状態
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Recording:", GUILayout.Width(70));
            
            Color originalColor = GUI.color;
            GUI.color = component.IsRecording ? Color.green : Color.red;
            EditorGUILayout.LabelField(component.IsRecording ? "● Active" : "● Stopped", GUILayout.Width(80));
            GUI.color = originalColor;
            
            if (component.IsRecording)
            {
                EditorGUILayout.LabelField("Signal:", GUILayout.Width(50));
                GUI.color = component.HasSignal ? Color.green : Color.yellow;
                EditorGUILayout.LabelField(component.HasSignal ? "● Detected" : "● No Signal", GUILayout.Width(80));
                GUI.color = originalColor;
                
                if (component.HasSignal)
                {
                    EditorGUILayout.LabelField($"Level: {(component.SignalLevel * 100):F1}%");
                }
            }
            
            // Unity FPS表示（FlexibleSpaceを削除して右端のはみ出しを防ぐ）
            DrawFPSDisplay();
            
            EditorGUILayout.EndHorizontal();
            
            // Sync State
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Sync State:", GUILayout.Width(70));
            GUI.color = GetStateColor(component.State);
            EditorGUILayout.LabelField(GetStateText(component.State), EditorStyles.boldLabel, GUILayout.Width(100));
            GUI.color = originalColor;
            
            // 状態説明
            EditorGUILayout.LabelField(GetStateDescription(component.State), EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // Output TC（メイン表示、左詰め）
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUI.backgroundColor = new Color(0.9f, 1.0f, 0.9f);
            
            // ラベルとTCの両方を左詰め
            var leftStyle = new GUIStyle(statusStyle);
            leftStyle.alignment = TextAnchor.MiddleLeft;
            leftStyle.padding.left = 10;
            EditorGUILayout.LabelField("Output TC", leftStyle);
            
            GUI.color = GetStateColor(component.State);
            var tcLeftStyle = new GUIStyle(timecodeStyle);
            tcLeftStyle.alignment = TextAnchor.MiddleLeft;
            tcLeftStyle.padding.left = 10;
            EditorGUILayout.LabelField(component.CurrentTimecode, tcLeftStyle, GUILayout.Height(30));
            GUI.color = originalColor;
            
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
            
            // アクションボタン（Output TCの下に移動）
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
            
            EditorGUILayout.Space(5);
            
            // Debug Info (別セクションとして表示)
            showDebugInfo = EditorGUILayout.Foldout(showDebugInfo, "Debug Info", true);
            
            if (showDebugInfo)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // Decoded LTC
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                GUI.backgroundColor = new Color(1.0f, 0.95f, 0.9f);
                
                var decodedLabelStyle = new GUIStyle(statusStyle);
                decodedLabelStyle.alignment = TextAnchor.MiddleLeft;
                decodedLabelStyle.padding.left = 10;
                EditorGUILayout.LabelField("Decoded LTC", decodedLabelStyle);
                
                var decodedTCStyle = new GUIStyle(timecodeStyle);
                decodedTCStyle.alignment = TextAnchor.MiddleLeft;
                decodedTCStyle.padding.left = 10;
                EditorGUILayout.LabelField(component.DecodedTimecode, decodedTCStyle, GUILayout.Height(30));
                
                GUI.backgroundColor = Color.white;
                EditorGUILayout.EndVertical();
                
                // Time Difference
                if (Mathf.Abs(component.TimeDifference) > 0.001f)
                {
                    string diffText = component.TimeDifference > 0 
                        ? $"Output: +{component.TimeDifference:F3}s" 
                        : $"Output: {component.TimeDifference:F3}s";
                    
                    MessageType msgType = Mathf.Abs(component.TimeDifference) > 0.1f 
                        ? MessageType.Warning 
                        : MessageType.Info;
                    EditorGUILayout.HelpBox(diffText, msgType);
                }
                
                // Noise比較グラフ（Debug Info内に追加）
                EditorGUILayout.Space(5);
                DrawNoiseComparisonGraph();
                
                EditorGUILayout.EndVertical();
            }
        }
        
        // DrawStatusSection と DrawTimecodeDisplay は削除（DrawStatusAndTimecodeSectionに統合）
        
        private void DrawNoiseComparisonGraph()
        {
            if (!Application.isPlaying || !component.IsRecording)
            {
                EditorGUILayout.HelpBox("Noise data available during playback", MessageType.Info);
                return;
            }
            
            EditorGUILayout.LabelField("Noise Comparison", EditorStyles.miniBoldLabel);
            
            // グラフ描画エリア
            Rect graphRect = GUILayoutUtility.GetRect(350, 100);
            GUI.Box(graphRect, GUIContent.none);
            
            // データ取得
            float[] ltcNoise = component.LTCNoiseHistory;
            float[] outputNoise = component.InternalNoiseHistory;
            int currentIndex = component.NoiseHistoryCurrentIndex;
            int historySize = component.NoiseHistoryMaxSize;
            
            if (ltcNoise == null || outputNoise == null || historySize <= 0)
            {
                return;
            }
            
            // グラフ描画
            Handles.BeginGUI();
            
            // LTC Noise (赤)
            DrawNoiseGraphLine(graphRect, ltcNoise, currentIndex, historySize, new Color(1f, 0.3f, 0.3f, 0.8f));
            
            // Output Noise (緑)
            DrawNoiseGraphLine(graphRect, outputNoise, currentIndex, historySize, new Color(0.3f, 1f, 0.3f, 0.8f));
            
            Handles.EndGUI();
            
            // 凡例
            EditorGUILayout.BeginHorizontal();
            GUI.color = new Color(1f, 0.3f, 0.3f);
            EditorGUILayout.LabelField("■ LTC Noise", GUILayout.Width(100));
            GUI.color = new Color(0.3f, 1f, 0.3f);
            EditorGUILayout.LabelField("■ Output (Filtered)", GUILayout.Width(120));
            GUI.color = Color.white;
            
            // 平均ノイズ値
            float avgLTC = CalculateAverageNoise(ltcNoise, currentIndex, 30);
            float avgOutput = CalculateAverageNoise(outputNoise, currentIndex, 30);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"Avg: {avgLTC:F3}s / {avgOutput:F3}s", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawNoiseGraphLine(Rect rect, float[] data, int currentIndex, int dataSize, Color color)
        {
            if (data == null || dataSize <= 0) return;
            
            // 間引きサンプリング
            const int samplePoints = 50;
            int step = Mathf.Max(1, dataSize / samplePoints);
            
            Vector3[] points = new Vector3[samplePoints];
            int pointCount = 0;
            
            for (int i = 0; i < dataSize && pointCount < samplePoints; i += step)
            {
                int index = (currentIndex - dataSize + i + 1 + data.Length) % data.Length;
                if (index < 0 || index >= data.Length) continue;
                
                float value = data[index];
                float x = rect.x + (i / (float)dataSize) * rect.width;
                float y = rect.y + rect.height - (Mathf.Clamp01(value * 10f) * rect.height);
                points[pointCount] = new Vector3(x, y, 0);
                pointCount++;
            }
            
            if (pointCount > 1)
            {
                Handles.color = color;
                var linePoints = new Vector3[pointCount];
                System.Array.Copy(points, 0, linePoints, 0, pointCount);
                Handles.DrawPolyLine(linePoints);
            }
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
                
                // Output TCノイズ（緑）
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
            EditorGUILayout.LabelField("━ Output TC Noise", GUILayout.Width(120));
            
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
                // Create a sub-array for the polyline (Unity compatibility)
                var linePoints = new Vector3[pointCount];
                System.Array.Copy(graphPointsCache, 0, linePoints, 0, pointCount);
                Handles.DrawPolyLine(linePoints);
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
        
        private void DrawEventSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            // イベントセクションのタイトル
            EditorGUILayout.LabelField("Events", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            // 状態変化イベント
            EditorGUILayout.LabelField("State Change Events", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            
            // OnLTCStarted
            var onLTCStarted = serializedObject.FindProperty("OnLTCStarted");
            if (onLTCStarted != null)
            {
                EditorGUILayout.PropertyField(onLTCStarted, new GUIContent("On LTC Started", "LTC受信開始時に発火"));
            }
            
            // OnLTCStopped
            var onLTCStopped = serializedObject.FindProperty("OnLTCStopped");
            if (onLTCStopped != null)
            {
                EditorGUILayout.PropertyField(onLTCStopped, new GUIContent("On LTC Stopped", "LTC受信停止時に発火"));
            }
            
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(5);
            
            // 継続状態イベント
            EditorGUILayout.LabelField("Continuous Events", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            
            // OnLTCReceiving
            var onLTCReceiving = serializedObject.FindProperty("OnLTCReceiving");
            if (onLTCReceiving != null)
            {
                EditorGUILayout.PropertyField(onLTCReceiving, new GUIContent("On LTC Receiving", "LTC受信中、毎フレーム発火"));
            }
            
            // OnLTCNoSignal
            var onLTCNoSignal = serializedObject.FindProperty("OnLTCNoSignal");
            if (onLTCNoSignal != null)
            {
                EditorGUILayout.PropertyField(onLTCNoSignal, new GUIContent("On LTC No Signal", "LTC未受信中、毎フレーム発火"));
            }
            
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(5);
            
            // タイムコードイベント
            EditorGUILayout.LabelField("Timecode Events", EditorStyles.miniBoldLabel);
            
            var timecodeEvents = serializedObject.FindProperty("timecodeEvents");
            if (timecodeEvents != null)
            {
                EditorGUI.indentLevel++;
                
                // リストのサイズ表示
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Events Count: {timecodeEvents.arraySize}", GUILayout.Width(100));
                
                GUILayout.FlexibleSpace();
                
                // イベント追加ボタン
                if (GUILayout.Button("Add Event", GUILayout.Width(80)))
                {
                    timecodeEvents.InsertArrayElementAtIndex(timecodeEvents.arraySize);
                    var newElement = timecodeEvents.GetArrayElementAtIndex(timecodeEvents.arraySize - 1);
                    
                    // デフォルト値を設定
                    var eventNameProp = newElement.FindPropertyRelative("eventName");
                    if (eventNameProp != null) eventNameProp.stringValue = $"Event {timecodeEvents.arraySize}";
                    
                    var targetTcProp = newElement.FindPropertyRelative("targetTimecode");
                    if (targetTcProp != null) targetTcProp.stringValue = "00:00:00:00";
                    
                    var oneShotProp = newElement.FindPropertyRelative("oneShot");
                    if (oneShotProp != null) oneShotProp.boolValue = true;
                    
                    var enabledProp = newElement.FindPropertyRelative("enabled");
                    if (enabledProp != null) enabledProp.boolValue = true;
                }
                
                // 全削除ボタン
                if (GUILayout.Button("Clear All", GUILayout.Width(80)))
                {
                    if (EditorUtility.DisplayDialog("Clear All Events", "すべてのタイムコードイベントを削除しますか？", "Yes", "No"))
                    {
                        timecodeEvents.ClearArray();
                    }
                }
                
                EditorGUILayout.EndHorizontal();
                
                // 各イベントの表示
                for (int i = 0; i < timecodeEvents.arraySize; i++)
                {
                    var element = timecodeEvents.GetArrayElementAtIndex(i);
                    
                    EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                    EditorGUILayout.BeginHorizontal();
                    
                    // イベント名
                    var eventNameProp = element.FindPropertyRelative("eventName");
                    var eventName = eventNameProp != null ? eventNameProp.stringValue : $"Event {i + 1}";
                    
                    // フォールドアウト
                    element.isExpanded = EditorGUILayout.Foldout(element.isExpanded, eventName, true);
                    
                    GUILayout.FlexibleSpace();
                    
                    // 削除ボタン
                    if (GUILayout.Button("Remove", GUILayout.Width(60)))
                    {
                        timecodeEvents.DeleteArrayElementAtIndex(i);
                        break;
                    }
                    
                    EditorGUILayout.EndHorizontal();
                    
                    // 展開時の詳細表示
                    if (element.isExpanded)
                    {
                        EditorGUI.indentLevel++;
                        
                        // イベント名
                        if (eventNameProp != null)
                        {
                            EditorGUILayout.PropertyField(eventNameProp, new GUIContent("Name"));
                        }
                        
                        // ターゲットタイムコード
                        var targetTcProp = element.FindPropertyRelative("targetTimecode");
                        if (targetTcProp != null)
                        {
                            EditorGUILayout.PropertyField(targetTcProp, new GUIContent("Target TC"));
                        }
                        
                        // 許容誤差
                        var toleranceProp = element.FindPropertyRelative("toleranceFrames");
                        if (toleranceProp != null)
                        {
                            EditorGUILayout.PropertyField(toleranceProp, new GUIContent("Tolerance (frames)"));
                        }
                        
                        // 設定
                        EditorGUILayout.BeginHorizontal();
                        
                        var oneShotProp = element.FindPropertyRelative("oneShot");
                        if (oneShotProp != null)
                        {
                            EditorGUILayout.PropertyField(oneShotProp, new GUIContent("One Shot"), GUILayout.Width(150));
                        }
                        
                        var enabledProp = element.FindPropertyRelative("enabled");
                        if (enabledProp != null)
                        {
                            EditorGUILayout.PropertyField(enabledProp, new GUIContent("Enabled"), GUILayout.Width(150));
                        }
                        
                        EditorGUILayout.EndHorizontal();
                        
                        // イベント
                        var onTimecodeReached = element.FindPropertyRelative("onTimecodeReached");
                        if (onTimecodeReached != null)
                        {
                            EditorGUILayout.PropertyField(onTimecodeReached, new GUIContent("Actions"));
                        }
                        
                        EditorGUI.indentLevel--;
                    }
                    
                    EditorGUILayout.EndVertical();
                }
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawAdvancedSettings()
        {
            showAdvancedSettings = EditorGUILayout.Foldout(showAdvancedSettings, "Advanced Settings", true);
            
            if (showAdvancedSettings)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                
                // 編集モードToggle（チェックボックスを左側に配置）
                advancedEditMode = EditorGUILayout.ToggleLeft("Enable Editing", advancedEditMode);
                
                if (!advancedEditMode)
                {
                    EditorGUILayout.HelpBox("Advanced settings are locked. Enable editing to modify.", MessageType.Info);
                }
                
                EditorGUILayout.Space(5);
                
                // 編集モードに応じて有効/無効を切り替え
                GUI.enabled = advancedEditMode;
                
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
                
                // Audio Processing
                EditorGUILayout.LabelField("Audio Processing", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("bufferSize"),
                    new GUIContent("Buffer Size (samples)", "Audio processing buffer size"));
                
                EditorGUILayout.Space(5);
                
                // Logging Settings（"Debug"の連続を避ける）
                EditorGUILayout.LabelField("Logging Settings", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty("enableDebugLogging"),
                    new GUIContent("Enable Console Output", "Output detailed information to Unity console"));
                
                // GUI.enabledを元に戻す
                GUI.enabled = true;
                
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
                    return "No signal";
                case LTCDecoder.SyncState.Syncing:
                    return "Syncing...";
                case LTCDecoder.SyncState.Locked:
                    return "Synchronized";
                case LTCDecoder.SyncState.Drifting:
                    return "Drift correction";
                default:
                    return "Unknown";
            }
        }
        
        private void DrawDebugTools()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Debug Tools", EditorStyles.boldLabel);
            
            EditorGUILayout.Space(5);
            
            // 既存のDebug UIを探す
            GameObject existingUI = GameObject.Find("LTC Debug UI");
            
            if (existingUI != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Debug UI:", GUILayout.Width(70));
                
                // 既存UIの名前を表示
                GUI.enabled = false;
                EditorGUILayout.TextField(existingUI.name);
                GUI.enabled = true;
                
                // Selectボタン
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeGameObject = existingUI;
                    EditorGUIUtility.PingObject(existingUI);
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space(5);
            }
            
            // ボタン配置
            EditorGUILayout.BeginHorizontal();
            
            // Createボタン（既存UIがない場合のみ有効）
            GUI.enabled = existingUI == null;
            if (GUILayout.Button("Create Debug UI"))
            {
                CreateDebugUI();
            }
            GUI.enabled = true;
            
            // Updateボタン（既存UIがある場合のみ有効）
            GUI.enabled = existingUI != null;
            if (GUILayout.Button("Update Layout"))
            {
                UpdateDebugUI(existingUI);
            }
            GUI.enabled = true;
            
            // Open Docsボタン
            if (GUILayout.Button("Open Docs"))
            {
                OpenDocumentation();
            }
            
            EditorGUILayout.EndHorizontal();
            
            // ヘルプ情報
            EditorGUILayout.Space(5);
            EditorGUILayout.HelpBox(
                existingUI != null 
                    ? "Debug UI is already created. Use 'Update Layout' to apply the latest layout changes."
                    : "Click 'Create Debug UI' to generate a debug interface for LTC monitoring.",
                MessageType.Info);
            
            EditorGUILayout.EndVertical();
        }
        
        private void CreateDebugUI()
        {
            // Undo対応
            Undo.RegisterCompleteObjectUndo(component, "Create LTC Debug UI");
            
            // LTCDebugSetupWithUI.CreateDebugUIOnly()を呼ぶ（UIのみ作成）
            var setupType = System.Type.GetType("jp.iridescent.ltcdecoder.Editor.LTCDebugSetupWithUI, jp.iridescent.ltcdecoder.Editor");
            if (setupType != null)
            {
                var createMethod = setupType.GetMethod("CreateDebugUIOnly", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (createMethod != null)
                {
                    // 現在のLTCDecoderオブジェクトを引数として渡す
                    createMethod.Invoke(null, new object[] { component.gameObject });
                    Debug.Log("[LTC Debug Tools] Debug UI created successfully.");
                }
                else
                {
                    Debug.LogError("[LTC Debug Tools] Could not find CreateDebugUIOnly method.");
                }
            }
            else
            {
                Debug.LogError("[LTC Debug Tools] Could not find LTCDebugSetupWithUI type.");
            }
        }
        
        private void UpdateDebugUI(GameObject existingUI)
        {
            // Undo対応
            Undo.RegisterCompleteObjectUndo(existingUI, "Update LTC Debug UI Layout");
            
            // LTCTimelineSyncDebugSetup.UpdateTimelineSyncDebugUI()を呼ぶ
            var setupType = System.Type.GetType("jp.iridescent.ltcdecoder.Editor.LTCTimelineSyncDebugSetup, jp.iridescent.ltcdecoder.Editor");
            if (setupType != null)
            {
                var updateMethod = setupType.GetMethod("UpdateTimelineSyncDebugUI", 
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (updateMethod != null)
                {
                    updateMethod.Invoke(null, new object[] { existingUI });
                    Debug.Log("[LTC Debug Tools] Debug UI layout updated successfully.");
                }
                else
                {
                    // メソッドがない場合は基本的な更新処理
                    Debug.LogWarning("[LTC Debug Tools] UpdateTimelineSyncDebugUI method not found. Performing basic update.");
                    // 基本的なレイアウト更新をここに実装可能
                }
            }
        }
        
        private void OpenDocumentation()
        {
            // ローカルドキュメントのパスを構築
            string docsPath = System.IO.Path.Combine(Application.dataPath, "..", "Documents", "ltc-debug-gui-layout-refactor.md");
            
            // ファイルが存在する場合はローカルで開く
            if (System.IO.File.Exists(docsPath))
            {
                Application.OpenURL("file:///" + docsPath.Replace('\\', '/'));
                Debug.Log($"[LTC Debug Tools] Opening local documentation: {docsPath}");
            }
            else
            {
                // GitHubリポジトリのドキュメントURL
                string githubUrl = "https://github.com/murasaqi/Unity_LTCDecoder/blob/master/Documents/ltc-debug-gui-layout-refactor.md";
                Application.OpenURL(githubUrl);
                Debug.Log($"[LTC Debug Tools] Opening online documentation: {githubUrl}");
            }
        }
    }
}