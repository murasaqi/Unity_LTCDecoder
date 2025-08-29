using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using LTC.Debug;

namespace LTC.Editor
{
    /// <summary>
    /// LTCEventDebugger用カスタムインスペクター
    /// </summary>
    [CustomEditor(typeof(LTCEventDebugger))]
    public class LTCEventDebuggerEditor : UnityEditor.Editor
    {
        private LTCEventDebugger debugger;
        private SerializedProperty enableDebuggerProp;
        private SerializedProperty maxHistorySizeProp;
        private SerializedProperty logToConsoleProp;
        private SerializedProperty simulationModeProp;
        private SerializedProperty simulatedTimecodeProp;
        private SerializedProperty simulatedSignalLevelProp;
        
        // UI状態
        private bool showEventStatus = true;
        private bool showManualTriggers = true;
        private bool showEventHistory = true;
        private bool showStatistics = true;
        private bool showTimecodeEvents = true;
        
        // スタイル
        private GUIStyle headerStyle;
        private GUIStyle statusStyle;
        private GUIStyle buttonStyle;
        private GUIStyle successStyle;
        private GUIStyle warningStyle;
        private GUIStyle errorStyle;
        
        void OnEnable()
        {
            debugger = (LTCEventDebugger)target;
            
            // プロパティを取得
            enableDebuggerProp = serializedObject.FindProperty("enableDebugger");
            maxHistorySizeProp = serializedObject.FindProperty("maxHistorySize");
            logToConsoleProp = serializedObject.FindProperty("logToConsole");
            simulationModeProp = serializedObject.FindProperty("simulationMode");
            simulatedTimecodeProp = serializedObject.FindProperty("simulatedTimecode");
            simulatedSignalLevelProp = serializedObject.FindProperty("simulatedSignalLevel");
            
            InitializeStyles();
        }
        
        private void InitializeStyles()
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };
            
            statusStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                wordWrap = true
            };
            
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 25
            };
            
            successStyle = new GUIStyle(statusStyle);
            successStyle.normal.textColor = new Color(0f, 0.7f, 0f);
            
            warningStyle = new GUIStyle(statusStyle);
            warningStyle.normal.textColor = new Color(0.7f, 0.5f, 0f);
            
            errorStyle = new GUIStyle(statusStyle);
            errorStyle.normal.textColor = Color.red;
        }
        
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            
            // ヘッダー
            EditorGUILayout.LabelField("LTC Event Debugger", headerStyle);
            EditorGUILayout.Space(5);
            
            // 基本設定
            DrawBasicSettings();
            
            EditorGUILayout.Space(10);
            
            // 実行中のみ表示
            if (Application.isPlaying && debugger.enabled)
            {
                // マニュアルトリガー
                showManualTriggers = EditorGUILayout.Foldout(showManualTriggers, "Manual Triggers", true);
                if (showManualTriggers)
                {
                    DrawManualTriggers();
                }
                
                EditorGUILayout.Space(5);
                
                // イベントステータス
                showEventStatus = EditorGUILayout.Foldout(showEventStatus, "Event Status", true);
                if (showEventStatus)
                {
                    DrawEventStatus();
                }
                
                EditorGUILayout.Space(5);
                
                // タイムコードイベント
                showTimecodeEvents = EditorGUILayout.Foldout(showTimecodeEvents, "Timecode Events", true);
                if (showTimecodeEvents)
                {
                    DrawTimecodeEvents();
                }
                
                EditorGUILayout.Space(5);
                
                // イベント履歴
                showEventHistory = EditorGUILayout.Foldout(showEventHistory, "Event History", true);
                if (showEventHistory)
                {
                    DrawEventHistory();
                }
                
                EditorGUILayout.Space(5);
                
                // 統計情報
                showStatistics = EditorGUILayout.Foldout(showStatistics, "Statistics", true);
                if (showStatistics)
                {
                    DrawStatistics();
                }
            }
            else if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to use debugging features", MessageType.Info);
            }
            
            serializedObject.ApplyModifiedProperties();
            
            // リアルタイム更新
            if (Application.isPlaying && debugger.enabled)
            {
                Repaint();
            }
        }
        
        private void DrawBasicSettings()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.PropertyField(enableDebuggerProp, new GUIContent("Enable Debugger"));
            EditorGUILayout.PropertyField(maxHistorySizeProp, new GUIContent("Max History Size"));
            EditorGUILayout.PropertyField(logToConsoleProp, new GUIContent("Log to Console"));
            
            EditorGUILayout.Space(5);
            
            // シミュレーションモード
            EditorGUILayout.LabelField("Simulation Mode", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(simulationModeProp, new GUIContent("Enable Simulation"));
            
            if (simulationModeProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(simulatedTimecodeProp, new GUIContent("Simulated TC"));
                EditorGUILayout.PropertyField(simulatedSignalLevelProp, new GUIContent("Signal Level"));
                
                if (Application.isPlaying)
                {
                    if (GUILayout.Button("Simulate Current TC", buttonStyle))
                    {
                        debugger.SimulateTimecode(simulatedTimecodeProp.stringValue);
                    }
                }
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawManualTriggers()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.LabelField("Trigger Events Manually", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            
            // 基本イベントトリガー
            if (GUILayout.Button("Start", buttonStyle))
            {
                debugger.TriggerEvent(LTCEventDebugger.EventType.LTCStarted);
            }
            
            if (GUILayout.Button("Stop", buttonStyle))
            {
                debugger.TriggerEvent(LTCEventDebugger.EventType.LTCStopped);
            }
            
            if (GUILayout.Button("Receiving", buttonStyle))
            {
                debugger.TriggerEvent(LTCEventDebugger.EventType.LTCReceiving);
            }
            
            if (GUILayout.Button("No Signal", buttonStyle))
            {
                debugger.TriggerEvent(LTCEventDebugger.EventType.LTCNoSignal);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // タイムコードシミュレーション
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Simulate TC:", GUILayout.Width(80));
            
            string newTC = EditorGUILayout.TextField(simulatedTimecodeProp.stringValue);
            if (newTC != simulatedTimecodeProp.stringValue)
            {
                simulatedTimecodeProp.stringValue = newTC;
            }
            
            if (GUILayout.Button("Fire", GUILayout.Width(60)))
            {
                debugger.SimulateTimecode(newTC);
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawEventStatus()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            var stats = debugger.EventStats;
            
            // LTC Started
            DrawEventStatusRow("LTC Started", 
                LTCEventDebugger.EventType.LTCStarted, 
                stats.ContainsKey(LTCEventDebugger.EventType.LTCStarted) ? 
                stats[LTCEventDebugger.EventType.LTCStarted] : null);
            
            // LTC Stopped
            DrawEventStatusRow("LTC Stopped", 
                LTCEventDebugger.EventType.LTCStopped,
                stats.ContainsKey(LTCEventDebugger.EventType.LTCStopped) ? 
                stats[LTCEventDebugger.EventType.LTCStopped] : null);
            
            // LTC Receiving
            DrawEventStatusRow("LTC Receiving", 
                LTCEventDebugger.EventType.LTCReceiving,
                stats.ContainsKey(LTCEventDebugger.EventType.LTCReceiving) ? 
                stats[LTCEventDebugger.EventType.LTCReceiving] : null,
                debugger.IsReceivingLTC);
            
            // LTC No Signal
            DrawEventStatusRow("LTC No Signal", 
                LTCEventDebugger.EventType.LTCNoSignal,
                stats.ContainsKey(LTCEventDebugger.EventType.LTCNoSignal) ? 
                stats[LTCEventDebugger.EventType.LTCNoSignal] : null,
                !debugger.IsReceivingLTC);
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawEventStatusRow(string eventName, LTCEventDebugger.EventType eventType, 
            LTCEventDebugger.EventStatistics stat, bool isActive = false)
        {
            EditorGUILayout.BeginHorizontal();
            
            // ステータスインジケーター
            Color originalColor = GUI.color;
            if (stat != null)
            {
                bool recentlyFired = (DateTime.Now - stat.lastFired).TotalSeconds < 1;
                GUI.color = isActive || recentlyFired ? Color.green : Color.gray;
            }
            else
            {
                GUI.color = Color.gray;
            }
            
            EditorGUILayout.LabelField("●", GUILayout.Width(20));
            GUI.color = originalColor;
            
            // イベント名
            EditorGUILayout.LabelField(eventName, GUILayout.Width(100));
            
            // カウント
            if (stat != null)
            {
                EditorGUILayout.LabelField($"Count: {stat.totalCount}", GUILayout.Width(80));
                
                // 最終発火時刻
                if (stat.lastFired != DateTime.MinValue)
                {
                    EditorGUILayout.LabelField($"Last: {stat.lastFired:HH:mm:ss}", GUILayout.Width(100));
                    EditorGUILayout.LabelField($"TC: {stat.lastTimecode}");
                }
                else
                {
                    EditorGUILayout.LabelField("Never fired");
                }
            }
            else
            {
                EditorGUILayout.LabelField("No data");
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawTimecodeEvents()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            var tcStats = debugger.TimecodeEventStats;
            
            if (tcStats.Count == 0)
            {
                EditorGUILayout.LabelField("No timecode events configured", EditorStyles.miniLabel);
            }
            else
            {
                foreach (var kvp in tcStats)
                {
                    DrawTimecodeEventRow(kvp.Key, kvp.Value);
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawTimecodeEventRow(string eventName, LTCEventDebugger.EventStatistics stat)
        {
            EditorGUILayout.BeginHorizontal();
            
            // ステータスインジケーター
            Color originalColor = GUI.color;
            bool recentlyFired = stat != null && (DateTime.Now - stat.lastFired).TotalSeconds < 2;
            GUI.color = recentlyFired ? Color.cyan : Color.gray;
            EditorGUILayout.LabelField("●", GUILayout.Width(20));
            GUI.color = originalColor;
            
            // イベント名
            EditorGUILayout.LabelField(eventName, GUILayout.Width(150));
            
            // 統計情報
            if (stat != null)
            {
                EditorGUILayout.LabelField($"Count: {stat.totalCount}", GUILayout.Width(80));
                
                if (stat.lastFired != DateTime.MinValue)
                {
                    EditorGUILayout.LabelField($"Last TC: {stat.lastTimecode}");
                }
            }
            
            // テストボタン
            if (GUILayout.Button("Test", GUILayout.Width(50)))
            {
                debugger.TriggerEvent(LTCEventDebugger.EventType.TimecodeEvent, eventName);
            }
            
            EditorGUILayout.EndHorizontal();
        }
        
        private void DrawEventHistory()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            var history = debugger.EventHistory;
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Event History ({history.Count} entries)", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                debugger.ClearHistory();
            }
            
            if (GUILayout.Button("Export CSV", GUILayout.Width(80)))
            {
                string path = EditorUtility.SaveFilePanel("Save Debug Log", "", "LTCDebugLog", "csv");
                if (!string.IsNullOrEmpty(path))
                {
                    debugger.SaveLogsToFile(path);
                }
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // 最新10件を表示
            int displayCount = Mathf.Min(10, history.Count);
            for (int i = history.Count - displayCount; i < history.Count; i++)
            {
                var entry = history[i];
                
                EditorGUILayout.BeginHorizontal();
                
                // タイムスタンプ
                EditorGUILayout.LabelField(entry.timestamp.ToString("HH:mm:ss.fff"), 
                    GUILayout.Width(100));
                
                // イベントタイプ
                GUIStyle style = GetEventStyle(entry.eventType);
                EditorGUILayout.LabelField(entry.eventType.ToString(), style, 
                    GUILayout.Width(100));
                
                // タイムコード
                EditorGUILayout.LabelField(entry.timecode, GUILayout.Width(100));
                
                // 追加情報
                if (!string.IsNullOrEmpty(entry.additionalInfo))
                {
                    EditorGUILayout.LabelField(entry.additionalInfo);
                }
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUILayout.EndVertical();
        }
        
        private void DrawStatistics()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Reset", GUILayout.Width(60)))
            {
                debugger.ResetStatistics();
            }
            
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space(5);
            
            // 合計イベント数
            int totalEvents = debugger.EventStats.Sum(kvp => kvp.Value.totalCount) +
                             debugger.TimecodeEventStats.Sum(kvp => kvp.Value.totalCount);
            EditorGUILayout.LabelField($"Total Events Fired: {totalEvents}");
            
            // 平均信号レベル
            if (debugger.EventStats.ContainsKey(LTCEventDebugger.EventType.LTCReceiving))
            {
                var stat = debugger.EventStats[LTCEventDebugger.EventType.LTCReceiving];
                EditorGUILayout.LabelField($"Average Signal Level: {stat.averageSignalLevel:P0}");
            }
            
            // イベント履歴サイズ
            EditorGUILayout.LabelField($"History Buffer: {debugger.EventHistory.Count}/{maxHistorySizeProp.intValue}");
            
            // シミュレーションモード状態
            EditorGUILayout.LabelField($"Mode: {(debugger.SimulationMode ? "SIMULATION" : "LIVE")}");
            
            EditorGUILayout.EndVertical();
        }
        
        private GUIStyle GetEventStyle(LTCEventDebugger.EventType eventType)
        {
            switch (eventType)
            {
                case LTCEventDebugger.EventType.LTCStarted:
                    return successStyle;
                case LTCEventDebugger.EventType.LTCStopped:
                    return warningStyle;
                case LTCEventDebugger.EventType.TimecodeEvent:
                    return new GUIStyle(statusStyle) { normal = { textColor = Color.cyan } };
                default:
                    return statusStyle;
            }
        }
    }
}