using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using LTC.Timeline;

namespace LTC.Debug
{
    /// <summary>
    /// LTCイベントデバッグUI - リアルタイムでイベント状態を表示
    /// </summary>
    public class LTCEventDebugUI : MonoBehaviour
    {
        #region UI要素の参照
        
        [Header("UI References")]
        [SerializeField] private Text statusText;
        [SerializeField] private Text currentTimecodeText;
        [SerializeField] private Text signalLevelText;
        [SerializeField] private Image signalLevelBar;
        
        [Header("Event Status Indicators")]
        [SerializeField] private Image ltcStartedIndicator;
        [SerializeField] private Text ltcStartedCountText;
        [SerializeField] private Text ltcStartedLastTimeText;
        
        [SerializeField] private Image ltcStoppedIndicator;
        [SerializeField] private Text ltcStoppedCountText;
        [SerializeField] private Text ltcStoppedLastTimeText;
        
        [SerializeField] private Image ltcReceivingIndicator;
        [SerializeField] private Text ltcReceivingFPSText;
        
        [SerializeField] private Image ltcNoSignalIndicator;
        [SerializeField] private Text ltcNoSignalDurationText;
        
        [Header("Event History")]
        [SerializeField] private Transform historyContent;
        [SerializeField] private GameObject historyEntryPrefab;
        [SerializeField] private ScrollRect historyScrollRect;
        [SerializeField] private int maxHistoryDisplay = 20;
        
        [Header("Timecode Events")]
        [SerializeField] private Transform timecodeEventsContent;
        [SerializeField] private GameObject timecodeEventPrefab;
        
        [Header("Statistics Panel")]
        [SerializeField] private Text totalEventsText;
        [SerializeField] private Text sessionDurationText;
        [SerializeField] private Text averageSignalText;
        
        #endregion
        
        #region フィールド
        
        [Header("Settings")]
        [SerializeField] private LTCEventDebugger debugger;
        [SerializeField] private bool autoScrollHistory = true;
        [SerializeField] private float updateInterval = 0.1f;
        
        // 色設定
        [Header("Colors")]
        [SerializeField] private Color activeColor = Color.green;
        [SerializeField] private Color inactiveColor = Color.gray;
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color errorColor = Color.red;
        
        // 内部状態
        private float lastUpdateTime;
        private DateTime sessionStartTime;
        private List<GameObject> historyEntries = new List<GameObject>();
        private Dictionary<string, TimecodeEventUI> timecodeEventUIs = new Dictionary<string, TimecodeEventUI>();
        
        // FPS計算用
        private int receivingEventCount = 0;
        private float receivingFPSTimer = 0f;
        private float currentReceivingFPS = 0f;
        
        // NoSignal計測用
        private float noSignalStartTime = 0f;
        private bool isNoSignal = false;
        
        #endregion
        
        #region Unity Lifecycle
        
        void Start()
        {
            sessionStartTime = DateTime.Now;
            
            if (debugger == null)
            {
                debugger = FindObjectOfType<LTCEventDebugger>();
            }
            
            if (debugger == null)
            {
                UnityEngine.Debug.LogError("LTCEventDebugUI requires LTCEventDebugger!");
                enabled = false;
                return;
            }
            
            // イベント購読
            debugger.OnEventOccurred += OnEventOccurred;
            debugger.OnHistoryCleared += OnHistoryCleared;
            
            // UI初期化
            InitializeUI();
            CreateTimecodeEventUIs();
        }
        
        void OnDestroy()
        {
            if (debugger != null)
            {
                debugger.OnEventOccurred -= OnEventOccurred;
                debugger.OnHistoryCleared -= OnHistoryCleared;
            }
        }
        
        void Update()
        {
            if (Time.time - lastUpdateTime < updateInterval) return;
            lastUpdateTime = Time.time;
            
            UpdateUI();
            UpdateFPS();
            UpdateStatistics();
        }
        
        #endregion
        
        #region UI更新
        
        private void InitializeUI()
        {
            // インジケーターを初期化
            SetIndicatorColor(ltcStartedIndicator, inactiveColor);
            SetIndicatorColor(ltcStoppedIndicator, inactiveColor);
            SetIndicatorColor(ltcReceivingIndicator, inactiveColor);
            SetIndicatorColor(ltcNoSignalIndicator, inactiveColor);
            
            // テキストをクリア
            if (ltcStartedCountText) ltcStartedCountText.text = "0";
            if (ltcStoppedCountText) ltcStoppedCountText.text = "0";
            if (ltcReceivingFPSText) ltcReceivingFPSText.text = "0 fps";
            if (ltcNoSignalDurationText) ltcNoSignalDurationText.text = "0.0s";
            
            // ステータス表示
            if (statusText) statusText.text = "Waiting...";
            if (currentTimecodeText) currentTimecodeText.text = "00:00:00:00";
            if (signalLevelText) signalLevelText.text = "0%";
        }
        
        private void UpdateUI()
        {
            if (debugger == null) return;
            
            // ステータス更新
            if (statusText)
            {
                statusText.text = debugger.SimulationMode ? "SIMULATION" : 
                                 debugger.IsReceivingLTC ? "RECEIVING" : "NO SIGNAL";
                statusText.color = debugger.SimulationMode ? warningColor :
                                  debugger.IsReceivingLTC ? activeColor : inactiveColor;
            }
            
            // インジケーター更新
            UpdateIndicators();
            
            // NoSignal時間更新
            if (isNoSignal && ltcNoSignalDurationText)
            {
                float duration = Time.time - noSignalStartTime;
                ltcNoSignalDurationText.text = $"{duration:F1}s";
            }
        }
        
        private void UpdateIndicators()
        {
            // 統計データから更新
            var stats = debugger.EventStats;
            
            // LTC Started
            if (stats.ContainsKey(LTCEventDebugger.EventType.LTCStarted))
            {
                var stat = stats[LTCEventDebugger.EventType.LTCStarted];
                if (ltcStartedCountText) ltcStartedCountText.text = stat.totalCount.ToString();
                if (ltcStartedLastTimeText && stat.lastFired != DateTime.MinValue)
                    ltcStartedLastTimeText.text = stat.lastFired.ToString("HH:mm:ss");
                
                // 最近発火した場合は点灯
                bool recentlyFired = (DateTime.Now - stat.lastFired).TotalSeconds < 1;
                SetIndicatorColor(ltcStartedIndicator, recentlyFired ? activeColor : inactiveColor);
            }
            
            // LTC Stopped
            if (stats.ContainsKey(LTCEventDebugger.EventType.LTCStopped))
            {
                var stat = stats[LTCEventDebugger.EventType.LTCStopped];
                if (ltcStoppedCountText) ltcStoppedCountText.text = stat.totalCount.ToString();
                if (ltcStoppedLastTimeText && stat.lastFired != DateTime.MinValue)
                    ltcStoppedLastTimeText.text = stat.lastFired.ToString("HH:mm:ss");
                
                bool recentlyFired = (DateTime.Now - stat.lastFired).TotalSeconds < 1;
                SetIndicatorColor(ltcStoppedIndicator, recentlyFired ? warningColor : inactiveColor);
            }
            
            // LTC Receiving
            SetIndicatorColor(ltcReceivingIndicator, debugger.IsReceivingLTC ? activeColor : inactiveColor);
            if (ltcReceivingFPSText) ltcReceivingFPSText.text = $"{currentReceivingFPS:F0} fps";
            
            // LTC No Signal
            SetIndicatorColor(ltcNoSignalIndicator, isNoSignal ? warningColor : inactiveColor);
        }
        
        private void UpdateFPS()
        {
            receivingFPSTimer += Time.deltaTime;
            if (receivingFPSTimer >= 1f)
            {
                currentReceivingFPS = receivingEventCount / receivingFPSTimer;
                receivingEventCount = 0;
                receivingFPSTimer = 0f;
            }
        }
        
        private void UpdateStatistics()
        {
            // 合計イベント数
            if (totalEventsText)
            {
                int total = debugger.EventStats.Sum(kvp => kvp.Value.totalCount);
                totalEventsText.text = $"Total Events: {total}";
            }
            
            // セッション時間
            if (sessionDurationText)
            {
                var duration = DateTime.Now - sessionStartTime;
                sessionDurationText.text = $"Session: {duration:hh\\:mm\\:ss}";
            }
            
            // 平均信号レベル
            if (averageSignalText && debugger.EventStats.ContainsKey(LTCEventDebugger.EventType.LTCReceiving))
            {
                var stat = debugger.EventStats[LTCEventDebugger.EventType.LTCReceiving];
                averageSignalText.text = $"Avg Signal: {stat.averageSignalLevel:P0}";
                
                // 信号レベルバー更新
                if (signalLevelBar)
                {
                    signalLevelBar.fillAmount = stat.averageSignalLevel;
                    signalLevelBar.color = stat.averageSignalLevel > 0.5f ? activeColor : warningColor;
                }
            }
        }
        
        #endregion
        
        #region イベントハンドリング
        
        private void OnEventOccurred(LTCEventDebugger.EventHistoryEntry entry)
        {
            // 現在のタイムコード更新
            if (currentTimecodeText)
            {
                currentTimecodeText.text = entry.timecode;
            }
            
            // 信号レベル更新
            if (signalLevelText)
            {
                signalLevelText.text = $"{entry.signalLevel * 100:F0}%";
            }
            
            // イベントタイプ別処理
            switch (entry.eventType)
            {
                case LTCEventDebugger.EventType.LTCReceiving:
                    receivingEventCount++;
                    isNoSignal = false;
                    break;
                    
                case LTCEventDebugger.EventType.LTCNoSignal:
                    if (!isNoSignal)
                    {
                        isNoSignal = true;
                        noSignalStartTime = Time.time;
                    }
                    break;
                    
                case LTCEventDebugger.EventType.LTCStopped:
                    isNoSignal = true;
                    noSignalStartTime = Time.time;
                    break;
                    
                case LTCEventDebugger.EventType.TimecodeEvent:
                    UpdateTimecodeEventUI(entry.eventName);
                    break;
            }
            
            // 履歴に追加（頻度の高いイベントは除外）
            if (entry.eventType != LTCEventDebugger.EventType.LTCReceiving &&
                entry.eventType != LTCEventDebugger.EventType.LTCNoSignal)
            {
                AddHistoryEntry(entry);
            }
        }
        
        private void OnHistoryCleared()
        {
            foreach (var entry in historyEntries)
            {
                Destroy(entry);
            }
            historyEntries.Clear();
        }
        
        #endregion
        
        #region 履歴管理
        
        private void AddHistoryEntry(LTCEventDebugger.EventHistoryEntry entry)
        {
            if (historyEntryPrefab == null || historyContent == null) return;
            
            // 新しいエントリを作成
            var entryGO = Instantiate(historyEntryPrefab, historyContent);
            historyEntries.Add(entryGO);
            
            // テキスト設定
            var texts = entryGO.GetComponentsInChildren<Text>();
            if (texts.Length >= 3)
            {
                texts[0].text = entry.timestamp.ToString("HH:mm:ss.fff");
                texts[1].text = entry.eventName;
                texts[2].text = entry.timecode;
            }
            
            // 色設定
            var image = entryGO.GetComponent<Image>();
            if (image)
            {
                image.color = GetEventColor(entry.eventType);
            }
            
            // 最大数を超えたら古いものを削除
            while (historyEntries.Count > maxHistoryDisplay)
            {
                Destroy(historyEntries[0]);
                historyEntries.RemoveAt(0);
            }
            
            // 自動スクロール
            if (autoScrollHistory && historyScrollRect)
            {
                Canvas.ForceUpdateCanvases();
                historyScrollRect.verticalNormalizedPosition = 0f;
            }
        }
        
        #endregion
        
        #region タイムコードイベントUI
        
        private class TimecodeEventUI
        {
            public GameObject gameObject;
            public Text nameText;
            public Text timecodeText;
            public Text countText;
            public Image statusIndicator;
            public Button resetButton;
        }
        
        private void CreateTimecodeEventUIs()
        {
            if (timecodeEventPrefab == null || timecodeEventsContent == null) return;
            
            // デバッガーからタイムコードイベント統計を取得
            var tcStats = debugger.TimecodeEventStats;
            
            foreach (var kvp in tcStats)
            {
                CreateTimecodeEventUI(kvp.Key);
            }
        }
        
        private void CreateTimecodeEventUI(string eventName)
        {
            if (timecodeEventUIs.ContainsKey(eventName)) return;
            
            var eventGO = Instantiate(timecodeEventPrefab, timecodeEventsContent);
            var ui = new TimecodeEventUI
            {
                gameObject = eventGO,
                nameText = eventGO.transform.Find("Name")?.GetComponent<Text>(),
                timecodeText = eventGO.transform.Find("Timecode")?.GetComponent<Text>(),
                countText = eventGO.transform.Find("Count")?.GetComponent<Text>(),
                statusIndicator = eventGO.transform.Find("Indicator")?.GetComponent<Image>(),
                resetButton = eventGO.transform.Find("ResetButton")?.GetComponent<Button>()
            };
            
            if (ui.nameText) ui.nameText.text = eventName;
            if (ui.countText) ui.countText.text = "0";
            if (ui.statusIndicator) ui.statusIndicator.color = inactiveColor;
            
            // リセットボタンの設定
            if (ui.resetButton)
            {
                ui.resetButton.onClick.AddListener(() => ResetTimecodeEvent(eventName));
            }
            
            timecodeEventUIs[eventName] = ui;
        }
        
        private void UpdateTimecodeEventUI(string eventName)
        {
            if (!timecodeEventUIs.ContainsKey(eventName)) return;
            
            var ui = timecodeEventUIs[eventName];
            var stat = debugger.GetTimecodeEventStatistics(eventName);
            
            if (stat != null)
            {
                if (ui.countText) ui.countText.text = stat.totalCount.ToString();
                if (ui.timecodeText) ui.timecodeText.text = stat.lastTimecode;
                
                // 発火アニメーション
                if (ui.statusIndicator)
                {
                    ui.statusIndicator.color = activeColor;
                    // フェードアウトは別途コルーチンで実装可能
                }
            }
        }
        
        private void ResetTimecodeEvent(string eventName)
        {
            // デバッガー側でリセット処理を呼び出す
            // ここでは UI のみリセット
            if (timecodeEventUIs.ContainsKey(eventName))
            {
                var ui = timecodeEventUIs[eventName];
                if (ui.countText) ui.countText.text = "0";
                if (ui.statusIndicator) ui.statusIndicator.color = inactiveColor;
            }
        }
        
        #endregion
        
        #region ユーティリティ
        
        private void SetIndicatorColor(Image indicator, Color color)
        {
            if (indicator) indicator.color = color;
        }
        
        private Color GetEventColor(LTCEventDebugger.EventType eventType)
        {
            switch (eventType)
            {
                case LTCEventDebugger.EventType.LTCStarted:
                    return activeColor;
                case LTCEventDebugger.EventType.LTCStopped:
                    return warningColor;
                case LTCEventDebugger.EventType.TimecodeEvent:
                    return Color.cyan;
                default:
                    return inactiveColor;
            }
        }
        
        #endregion
    }
}