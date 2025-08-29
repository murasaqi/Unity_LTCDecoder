using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using UnityEngine;
using LTC.Timeline;

namespace LTC.Debug
{
    /// <summary>
    /// LTCイベントデバッガー - イベントの監視とデバッグ機能を提供
    /// </summary>
    [RequireComponent(typeof(LTCDecoder))]
    public class LTCEventDebugger : MonoBehaviour
    {
        #region 定数とEnum
        
        /// <summary>
        /// イベントタイプ
        /// </summary>
        public enum EventType
        {
            LTCStarted,
            LTCStopped,
            LTCReceiving,
            LTCNoSignal,
            TimecodeEvent
        }
        
        /// <summary>
        /// イベント履歴エントリ
        /// </summary>
        [Serializable]
        public class EventHistoryEntry
        {
            public EventType eventType;
            public string eventName;
            public string timecode;
            public DateTime timestamp;
            public float signalLevel;
            public string additionalInfo;
            
            public EventHistoryEntry(EventType type, string name, string tc, float signal, string info = "")
            {
                eventType = type;
                eventName = name;
                timecode = tc;
                timestamp = DateTime.Now;
                signalLevel = signal;
                additionalInfo = info;
            }
            
            public string ToCSV()
            {
                return $"{timestamp:yyyy-MM-dd HH:mm:ss.fff},{eventType},{eventName},{timecode},{signalLevel:F2},{additionalInfo}";
            }
        }
        
        /// <summary>
        /// イベント統計
        /// </summary>
        [Serializable]
        public class EventStatistics
        {
            public int totalCount;
            public DateTime lastFired;
            public string lastTimecode;
            public float averageSignalLevel;
            private List<float> signalLevels = new List<float>();
            
            public void AddOccurrence(string tc, float signal)
            {
                totalCount++;
                lastFired = DateTime.Now;
                lastTimecode = tc;
                signalLevels.Add(signal);
                if (signalLevels.Count > 100) signalLevels.RemoveAt(0);
                averageSignalLevel = signalLevels.Count > 0 ? signalLevels.Average() : 0f;
            }
            
            public void Reset()
            {
                totalCount = 0;
                lastFired = DateTime.MinValue;
                lastTimecode = "00:00:00:00";
                averageSignalLevel = 0f;
                signalLevels.Clear();
            }
        }
        
        #endregion
        
        #region フィールド
        
        [Header("デバッグ設定")]
        [SerializeField] private bool enableDebugger = true;
        [SerializeField] private int maxHistorySize = 50;
        [SerializeField] private bool logToConsole = false;
        
        [Header("シミュレーション")]
        [SerializeField] private bool simulationMode = false;
        [SerializeField] private string simulatedTimecode = "00:00:00:00";
        [SerializeField] private float simulatedSignalLevel = 1.0f;
        
        // コンポーネント参照
        private LTCDecoder ltcDecoder;
        
        // イベント履歴
        private List<EventHistoryEntry> eventHistory = new List<EventHistoryEntry>();
        
        // イベント統計
        private Dictionary<EventType, EventStatistics> eventStats = new Dictionary<EventType, EventStatistics>();
        private Dictionary<string, EventStatistics> timecodeEventStats = new Dictionary<string, EventStatistics>();
        
        // 状態
        private bool isReceivingLTC = false;
        private float lastUpdateTime = 0f;
        private const float UPDATE_INTERVAL = 0.1f; // 100ms間隔で更新
        
        // イベントコールバック（UIや他のコンポーネントから購読可能）
        public Action<EventHistoryEntry> OnEventOccurred;
        public Action OnHistoryCleared;
        
        #endregion
        
        #region プロパティ
        
        public List<EventHistoryEntry> EventHistory => new List<EventHistoryEntry>(eventHistory);
        public Dictionary<EventType, EventStatistics> EventStats => new Dictionary<EventType, EventStatistics>(eventStats);
        public Dictionary<string, EventStatistics> TimecodeEventStats => new Dictionary<string, EventStatistics>(timecodeEventStats);
        public bool IsReceivingLTC => isReceivingLTC;
        public bool SimulationMode => simulationMode;
        
        #endregion
        
        #region Unity Lifecycle
        
        void Awake()
        {
            ltcDecoder = GetComponent<LTCDecoder>();
            if (ltcDecoder == null)
            {
                UnityEngine.Debug.LogError("LTCEventDebugger requires LTCDecoder component!");
                enabled = false;
                return;
            }
            
            InitializeStatistics();
        }
        
        void OnEnable()
        {
            if (!enableDebugger) return;
            
            // イベントを購読
            ltcDecoder.OnLTCStarted.AddListener(HandleLTCStarted);
            ltcDecoder.OnLTCStopped.AddListener(HandleLTCStopped);
            ltcDecoder.OnLTCReceiving.AddListener(HandleLTCReceiving);
            ltcDecoder.OnLTCNoSignal.AddListener(HandleLTCNoSignal);
            
            // タイムコードイベントを購読
            SubscribeToTimecodeEvents();
        }
        
        void OnDisable()
        {
            // イベントの購読解除
            if (ltcDecoder != null)
            {
                ltcDecoder.OnLTCStarted.RemoveListener(HandleLTCStarted);
                ltcDecoder.OnLTCStopped.RemoveListener(HandleLTCStopped);
                ltcDecoder.OnLTCReceiving.RemoveListener(HandleLTCReceiving);
                ltcDecoder.OnLTCNoSignal.RemoveListener(HandleLTCNoSignal);
            }
        }
        
        void Update()
        {
            if (!enableDebugger) return;
            
            // シミュレーションモードの更新
            if (simulationMode && Time.time - lastUpdateTime > UPDATE_INTERVAL)
            {
                lastUpdateTime = Time.time;
                UpdateSimulation();
            }
        }
        
        #endregion
        
        #region イベントハンドラー
        
        private void HandleLTCStarted(LTCEventData data)
        {
            isReceivingLTC = true;
            RecordEvent(EventType.LTCStarted, "LTC Started", data);
            
            if (logToConsole)
                UnityEngine.Debug.Log($"[LTC Debug] Started - TC: {data.currentTimecode}, Signal: {data.signalLevel:F2}");
        }
        
        private void HandleLTCStopped(LTCEventData data)
        {
            isReceivingLTC = false;
            RecordEvent(EventType.LTCStopped, "LTC Stopped", data);
            
            if (logToConsole)
                UnityEngine.Debug.Log($"[LTC Debug] Stopped - Last TC: {data.currentTimecode}");
        }
        
        private void HandleLTCReceiving(LTCEventData data)
        {
            // 頻度が高いので間引く
            if (Time.time - lastUpdateTime < UPDATE_INTERVAL) return;
            lastUpdateTime = Time.time;
            
            RecordEvent(EventType.LTCReceiving, "LTC Receiving", data, false); // 履歴に記録しない
            
            if (logToConsole && UnityEngine.Random.value < 0.01f) // 1%の確率でログ
                UnityEngine.Debug.Log($"[LTC Debug] Receiving - TC: {data.currentTimecode}");
        }
        
        private void HandleLTCNoSignal(LTCEventData data)
        {
            // 頻度が高いので間引く
            if (Time.time - lastUpdateTime < UPDATE_INTERVAL) return;
            lastUpdateTime = Time.time;
            
            RecordEvent(EventType.LTCNoSignal, "No Signal", data, false); // 履歴に記録しない
        }
        
        private void HandleTimecodeEvent(string eventName, LTCEventData data)
        {
            var entry = RecordEvent(EventType.TimecodeEvent, eventName, data);
            entry.additionalInfo = $"Target TC Event: {eventName}";
            
            if (logToConsole)
                UnityEngine.Debug.Log($"[LTC Debug] Timecode Event '{eventName}' triggered at {data.currentTimecode}");
        }
        
        #endregion
        
        #region パブリックメソッド
        
        /// <summary>
        /// イベントを手動でトリガー
        /// </summary>
        public void TriggerEvent(EventType eventType, string eventName = null)
        {
            var data = new LTCEventData(
                simulationMode ? simulatedTimecode : ltcDecoder.CurrentTimecode,
                0f,
                simulationMode ? true : ltcDecoder.HasSignal,
                simulationMode ? simulatedSignalLevel : ltcDecoder.SignalLevel
            );
            
            switch (eventType)
            {
                case EventType.LTCStarted:
                    HandleLTCStarted(data);
                    break;
                case EventType.LTCStopped:
                    HandleLTCStopped(data);
                    break;
                case EventType.LTCReceiving:
                    HandleLTCReceiving(data);
                    break;
                case EventType.LTCNoSignal:
                    HandleLTCNoSignal(data);
                    break;
                case EventType.TimecodeEvent:
                    HandleTimecodeEvent(eventName ?? "Manual Trigger", data);
                    break;
            }
        }
        
        /// <summary>
        /// 特定のタイムコードをシミュレート
        /// </summary>
        public void SimulateTimecode(string timecode)
        {
            simulatedTimecode = timecode;
            
            if (simulationMode)
            {
                var data = new LTCEventData(timecode, 0f, true, simulatedSignalLevel);
                
                // タイムコードイベントをチェック
                var tcEvents = ltcDecoder.GetTimecodeEvents();
                foreach (var tcEvent in tcEvents)
                {
                    if (tcEvent.IsMatch(timecode, ltcDecoder.FrameRate()))
                    {
                        HandleTimecodeEvent(tcEvent.eventName, data);
                    }
                }
            }
        }
        
        /// <summary>
        /// イベント履歴をクリア
        /// </summary>
        public void ClearHistory()
        {
            eventHistory.Clear();
            OnHistoryCleared?.Invoke();
            
            if (logToConsole)
                UnityEngine.Debug.Log("[LTC Debug] History cleared");
        }
        
        /// <summary>
        /// 統計をリセット
        /// </summary>
        public void ResetStatistics()
        {
            foreach (var stat in eventStats.Values)
            {
                stat.Reset();
            }
            
            foreach (var stat in timecodeEventStats.Values)
            {
                stat.Reset();
            }
            
            if (logToConsole)
                UnityEngine.Debug.Log("[LTC Debug] Statistics reset");
        }
        
        /// <summary>
        /// ログをCSV形式でエクスポート
        /// </summary>
        public string ExportLogsAsCSV()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,EventType,EventName,Timecode,SignalLevel,AdditionalInfo");
            
            foreach (var entry in eventHistory)
            {
                sb.AppendLine(entry.ToCSV());
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// ログをファイルに保存
        /// </summary>
        public void SaveLogsToFile(string filepath = null)
        {
            if (string.IsNullOrEmpty(filepath))
            {
                filepath = Path.Combine(Application.dataPath, $"LTCDebugLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }
            
            try
            {
                File.WriteAllText(filepath, ExportLogsAsCSV());
                UnityEngine.Debug.Log($"[LTC Debug] Logs saved to: {filepath}");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[LTC Debug] Failed to save logs: {e.Message}");
            }
        }
        
        /// <summary>
        /// 特定イベントの統計を取得
        /// </summary>
        public EventStatistics GetEventStatistics(EventType eventType)
        {
            return eventStats.ContainsKey(eventType) ? eventStats[eventType] : null;
        }
        
        /// <summary>
        /// タイムコードイベントの統計を取得
        /// </summary>
        public EventStatistics GetTimecodeEventStatistics(string eventName)
        {
            return timecodeEventStats.ContainsKey(eventName) ? timecodeEventStats[eventName] : null;
        }
        
        #endregion
        
        #region プライベートメソッド
        
        private void InitializeStatistics()
        {
            // 基本イベントの統計を初期化
            foreach (EventType eventType in Enum.GetValues(typeof(EventType)))
            {
                if (eventType != EventType.TimecodeEvent)
                {
                    eventStats[eventType] = new EventStatistics();
                }
            }
        }
        
        private void SubscribeToTimecodeEvents()
        {
            var tcEvents = ltcDecoder.GetTimecodeEvents();
            if (tcEvents == null) return;
            
            foreach (var tcEvent in tcEvents)
            {
                if (!timecodeEventStats.ContainsKey(tcEvent.eventName))
                {
                    timecodeEventStats[tcEvent.eventName] = new EventStatistics();
                }
                
                // イベントに購読
                tcEvent.onTimecodeReached.AddListener((data) => HandleTimecodeEvent(tcEvent.eventName, data));
            }
        }
        
        private EventHistoryEntry RecordEvent(EventType type, string name, LTCEventData data, bool addToHistory = true)
        {
            var entry = new EventHistoryEntry(type, name, data.currentTimecode, data.signalLevel);
            
            // 履歴に追加
            if (addToHistory)
            {
                eventHistory.Add(entry);
                
                // 最大サイズを超えたら古いものを削除
                while (eventHistory.Count > maxHistorySize)
                {
                    eventHistory.RemoveAt(0);
                }
            }
            
            // 統計を更新
            if (type != EventType.TimecodeEvent)
            {
                if (eventStats.ContainsKey(type))
                {
                    eventStats[type].AddOccurrence(data.currentTimecode, data.signalLevel);
                }
            }
            else
            {
                if (timecodeEventStats.ContainsKey(name))
                {
                    timecodeEventStats[name].AddOccurrence(data.currentTimecode, data.signalLevel);
                }
            }
            
            // コールバック呼び出し
            OnEventOccurred?.Invoke(entry);
            
            return entry;
        }
        
        private void UpdateSimulation()
        {
            if (!simulationMode) return;
            
            // シミュレーションモードでタイムコードを進める
            var parts = simulatedTimecode.Split(':');
            if (parts.Length == 4)
            {
                if (int.TryParse(parts[3], out int frames))
                {
                    frames++;
                    if (frames >= ltcDecoder.FrameRate())
                    {
                        frames = 0;
                        if (int.TryParse(parts[2], out int seconds))
                        {
                            seconds++;
                            if (seconds >= 60)
                            {
                                seconds = 0;
                                // 分も更新...（省略）
                            }
                            parts[2] = seconds.ToString("D2");
                        }
                    }
                    parts[3] = frames.ToString("D2");
                    simulatedTimecode = string.Join(":", parts);
                }
            }
        }
        
        #endregion
    }
    
    // LTCDecoderに追加するための拡張メソッド
    public static class LTCDecoderExtensions
    {
        public static List<TimecodeEvent> GetTimecodeEvents(this LTCDecoder decoder)
        {
            // リフレクションを使用してprivateフィールドにアクセス
            var field = typeof(LTCDecoder).GetField("timecodeEvents", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(decoder) as List<TimecodeEvent>;
        }
        
        public static float FrameRate(this LTCDecoder decoder)
        {
            var field = typeof(LTCDecoder).GetField("frameRate",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            object value = field?.GetValue(decoder);
            return value != null ? (float)value : 30f;
        }
    }
}