using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using jp.iridescent.ltcdecoder;

namespace jp.iridescent.ltcdecoder.Samples
{
    /// <summary>
    /// LTCDecoderの状態を可視化するUIコンポーネント
    /// </summary>
    public class LTCDecoderUI : MonoBehaviour
    {
        #region UI要素の参照
        
        [Header("Core Display")]
        [SerializeField] private Text currentTimecodeText;
        [SerializeField] private Text decodedTimecodeText;
        [SerializeField] private Image signalLevelBar;
        [SerializeField] private Text signalLevelText;
        [SerializeField] private Text statusText;
        
        [Header("Event Indicators")]
        [SerializeField] private Image receivingIndicator;
        [SerializeField] private Image stoppedIndicator;
        [SerializeField] private Text eventCountText;
        
        [Header("Debug Messages")]
        [SerializeField] private ScrollRect debugScrollView;
        [SerializeField] private Transform debugMessageContainer;
        [SerializeField] private GameObject messagePrefab;
        [SerializeField] private int maxDisplayMessages = 50;
        
        [Header("Controls")]
        [SerializeField] private Button clearButton;
        [SerializeField] private Button exportButton;
        [SerializeField] private Button copyButton;
        [SerializeField] private Dropdown filterDropdown;
        
        [Header("Statistics Display")]
        [SerializeField] private Text sessionInfoText;
        [SerializeField] private Text signalQualityText;
        [SerializeField] private Text anomalyCountText;
        
        [Header("Event Log Messages")]
        [Tooltip("LTC開始時のメッセージ ({timecode}=TC, {signal}=信号レベル)")]
        [SerializeField] private string ltcStartedMessage = "LTC信号受信開始";
        [Tooltip("LTC停止時のメッセージ")]
        [SerializeField] private string ltcStoppedMessage = "LTC信号停止 at {timecode}";
        [Tooltip("LTC受信中のメッセージ")]
        [SerializeField] private string ltcReceivingMessage = "受信中: {timecode}";
        [Tooltip("信号なし時のメッセージ")]
        [SerializeField] private string ltcNoSignalMessage = "⚠ 信号喪失";
        [Tooltip("タイムコードイベント時のメッセージ ({eventName}=イベント名)")]
        [SerializeField] private string timecodeEventMessageFormat = "イベント '{eventName}' 発火: {timecode}";
        
        #endregion
        
        #region フィールド
        
        [Header("Settings")]
        [SerializeField] private float updateInterval = 0.1f;
        [SerializeField] private bool autoScroll = true;
        [SerializeField] private bool showStatistics = true;
        
        // 色設定
        [Header("Colors")]
        [SerializeField] private Color activeColor = Color.green;
        [SerializeField] private Color inactiveColor = Color.gray;
        [SerializeField] private Color warningColor = Color.yellow;
        [SerializeField] private Color errorColor = Color.red;
        
        // コンポーネント参照
        [Header("Target Components")]
        [SerializeField] private LTCDecoder targetDecoder;
        [SerializeField] private LTCEventDebugger targetDebugger;
        
        // 実際に使用する参照
        private LTCDecoder decoder;
        private LTCEventDebugger debugger;
        
        // 内部状態
        private float lastUpdateTime;
        private List<GameObject> messageObjects = new List<GameObject>();
        private string currentFilter = "All";
        private bool isReceiving = false;
        private DateTime lastReceivedTime;
        
        #endregion
        
        #region Unity Lifecycle
        
        void Start()
        {
            // コンポーネント参照の設定（Inspectorで設定されていれば優先、なければ自動検索）
            decoder = targetDecoder ? targetDecoder : FindObjectOfType<LTCDecoder>();
            debugger = targetDebugger ? targetDebugger : decoder?.GetComponent<LTCEventDebugger>();
            
            // デバッグログ出力
            UnityEngine.Debug.Log($"[LTCDecoderUI] Start - targetDecoder: {targetDecoder}, targetDebugger: {targetDebugger}");
            UnityEngine.Debug.Log($"[LTCDecoderUI] decoder: {decoder}, debugger: {debugger}");
            
            if (decoder == null)
            {
                UnityEngine.Debug.LogError("LTCDecoderUI: LTCDecoder not found! Please assign in Inspector or ensure one exists in the scene.");
                enabled = false;
                return;
            }
            
            // UI初期化
            InitializeUI();
            
            // UI要素の確認ログ
            UnityEngine.Debug.Log($"[LTCDecoderUI] UI References - statusText: {statusText}, currentTimecodeText: {currentTimecodeText}");
            UnityEngine.Debug.Log($"[LTCDecoderUI] signalLevelBar: {signalLevelBar}, receivingIndicator: {receivingIndicator}");
            
            // イベント購読
            if (debugger != null)
            {
                debugger.OnMessageAdded += OnDebugMessageAdded;
                debugger.OnHistoryCleared += OnHistoryCleared;
                UnityEngine.Debug.Log("[LTCDecoderUI] Debugger events subscribed");
            }
            
            // LTCイベント購読
            decoder.OnLTCStarted.AddListener(OnLTCStarted);
            decoder.OnLTCStopped.AddListener(OnLTCStopped);
            decoder.OnLTCReceiving.AddListener(OnLTCReceiving);
            UnityEngine.Debug.Log("[LTCDecoderUI] LTC events subscribed");
        }
        
        void OnDestroy()
        {
            // イベント購読解除
            if (debugger != null)
            {
                debugger.OnMessageAdded -= OnDebugMessageAdded;
                debugger.OnHistoryCleared -= OnHistoryCleared;
            }
            
            if (decoder != null)
            {
                decoder.OnLTCStarted.RemoveListener(OnLTCStarted);
                decoder.OnLTCStopped.RemoveListener(OnLTCStopped);
                decoder.OnLTCReceiving.RemoveListener(OnLTCReceiving);
            }
        }
        
        void Update()
        {
            if (Time.time - lastUpdateTime < updateInterval) return;
            lastUpdateTime = Time.time;
            
            UpdateDisplay();
        }
        
        #endregion
        
        #region UI初期化
        
        private void InitializeUI()
        {
            // ボタン設定
            if (clearButton != null)
            {
                clearButton.onClick.AddListener(OnClearButtonClicked);
            }
            
            if (exportButton != null)
            {
                exportButton.onClick.AddListener(OnExportButtonClicked);
            }
            
            if (copyButton != null)
            {
                copyButton.onClick.AddListener(OnCopyButtonClicked);
            }
            
            // フィルタードロップダウン設定
            if (filterDropdown != null)
            {
                filterDropdown.ClearOptions();
                var options = new List<string> { "All", "Info", "Event", "Warning", "Error", "Debug" };
                filterDropdown.AddOptions(options);
                filterDropdown.onValueChanged.AddListener(OnFilterChanged);
            }
            
            // 初期表示
            if (statusText) statusText.text = "Waiting...";
            if (currentTimecodeText) currentTimecodeText.text = "00:00:00:00";
            if (decodedTimecodeText) decodedTimecodeText.text = "00:00:00:00";
            if (signalLevelText) signalLevelText.text = "0%";
            
            // インジケーター初期化
            SetIndicatorColor(receivingIndicator, inactiveColor);
            SetIndicatorColor(stoppedIndicator, inactiveColor);
        }
        
        #endregion
        
        #region UI更新
        
        private void UpdateDisplay()
        {
            if (decoder == null) return;
            
            // タイムコード更新
            if (currentTimecodeText)
            {
                currentTimecodeText.text = decoder.CurrentTimecode;
            }
            
            if (decodedTimecodeText)
            {
                decodedTimecodeText.text = decoder.DecodedTimecode;
            }
            
            // 信号レベル更新
            if (signalLevelBar)
            {
                signalLevelBar.fillAmount = decoder.SignalLevel;
                signalLevelBar.color = decoder.SignalLevel > 0.5f ? activeColor : warningColor;
            }
            
            if (signalLevelText)
            {
                signalLevelText.text = $"{decoder.SignalLevel * 100:F0}%";
            }
            
            // ステータス更新
            if (statusText)
            {
                statusText.text = decoder.HasSignal ? "RECEIVING" : "NO SIGNAL";
                statusText.color = decoder.HasSignal ? activeColor : inactiveColor;
            }
            
            // インジケーター更新
            UpdateIndicators();
            
            // イベントカウント更新
            if (eventCountText && debugger != null)
            {
                var stats = debugger.GetEventStatistics();
                int totalEvents = 0;
                foreach (var count in stats.Values)
                {
                    totalEvents += count;
                }
                eventCountText.text = $"Events: {totalEvents}";
            }
            
            // 統計表示更新
            if (showStatistics && debugger != null)
            {
                UpdateStatisticsDisplay();
            }
        }
        
        private void UpdateIndicators()
        {
            // Receiving indicator
            if (isReceiving && (DateTime.Now - lastReceivedTime).TotalSeconds < 1)
            {
                SetIndicatorColor(receivingIndicator, activeColor);
            }
            else
            {
                SetIndicatorColor(receivingIndicator, inactiveColor);
            }
            
            // Stopped indicator - 最近停止した場合は点灯
            bool recentlyStopped = !decoder.HasSignal && (DateTime.Now - lastReceivedTime).TotalSeconds < 2;
            SetIndicatorColor(stoppedIndicator, recentlyStopped ? warningColor : inactiveColor);
        }
        
        #endregion
        
        #region デバッグメッセージ表示
        
        private void OnDebugMessageAdded(DebugMessage message)
        {
            // フィルタチェック
            if (currentFilter != "All" && message.category != currentFilter)
            {
                return;
            }
            
            // メッセージUI作成
            CreateMessageUI(message);
            
            // 自動スクロール
            if (autoScroll && debugScrollView != null)
            {
                Canvas.ForceUpdateCanvases();
                debugScrollView.verticalNormalizedPosition = 0f;
            }
        }
        
        private void CreateMessageUI(DebugMessage message)
        {
            if (messagePrefab == null || debugMessageContainer == null) return;
            
            // 新しいメッセージオブジェクトを作成
            GameObject msgObj = Instantiate(messagePrefab, debugMessageContainer);
            msgObj.SetActive(true);  // プレハブがDisableなので、明示的にEnableにする
            messageObjects.Add(msgObj);
            
            // テキスト設定
            Text[] texts = msgObj.GetComponentsInChildren<Text>();
            if (texts.Length > 0)
            {
                texts[0].text = $"[{message.timestamp:HH:mm:ss}] [{message.category}] {message.message}";
                texts[0].color = message.color;
            }
            
            // 最大表示数を超えたら古いものを削除
            while (messageObjects.Count > maxDisplayMessages)
            {
                Destroy(messageObjects[0]);
                messageObjects.RemoveAt(0);
            }
        }
        
        private void OnHistoryCleared()
        {
            // すべてのメッセージUIを削除
            foreach (var obj in messageObjects)
            {
                Destroy(obj);
            }
            messageObjects.Clear();
        }
        
        #endregion
        
        #region パブリックメソッド - 直接ログ
        
        /// <summary>
        /// メッセージを直接ログに追加（Unity Event用）
        /// </summary>
        public void AddLogMessage(string message)
        {
            var debugMsg = new DebugMessage(
                message,
                DebugMessage.INFO,
                decoder?.CurrentTimecode ?? "00:00:00:00",
                decoder?.SignalLevel ?? 0f,
                Color.white
            );
            
            // UIに直接追加
            OnDebugMessageAdded(debugMsg);
        }
        
        /// <summary>
        /// LTCイベントからメッセージを追加（Unity Event用）
        /// </summary>
        public void AddLogMessageFromEvent(LTCEventData eventData)
        {
            string message = $"TC: {eventData.currentTimecode} (Signal: {eventData.signalLevel:P0})";
            var debugMsg = new DebugMessage(
                message,
                DebugMessage.EVENT,
                eventData.currentTimecode,
                eventData.signalLevel,
                Color.green
            );
            
            OnDebugMessageAdded(debugMsg);
        }
        
        /// <summary>
        /// 情報メッセージを追加
        /// </summary>
        public void AddInfoMessage(string message)
        {
            var debugMsg = new DebugMessage(
                message,
                DebugMessage.INFO,
                decoder?.CurrentTimecode ?? "00:00:00:00",
                decoder?.SignalLevel ?? 0f,
                Color.white
            );
            OnDebugMessageAdded(debugMsg);
        }
        
        /// <summary>
        /// 警告メッセージを追加
        /// </summary>
        public void AddWarningMessage(string message)
        {
            var debugMsg = new DebugMessage(
                message,
                DebugMessage.WARNING,
                decoder?.CurrentTimecode ?? "00:00:00:00",
                decoder?.SignalLevel ?? 0f,
                Color.yellow
            );
            OnDebugMessageAdded(debugMsg);
        }
        
        /// <summary>
        /// エラーメッセージを追加
        /// </summary>
        public void AddErrorMessage(string message)
        {
            var debugMsg = new DebugMessage(
                message,
                DebugMessage.ERROR,
                decoder?.CurrentTimecode ?? "00:00:00:00",
                decoder?.SignalLevel ?? 0f,
                Color.red
            );
            OnDebugMessageAdded(debugMsg);
        }
        
        /// <summary>
        /// LTC開始イベント用（カスタマイズ可能なメッセージ）
        /// </summary>
        public void LogLTCStarted(LTCEventData data)
        {
            string message = FormatEventMessage(ltcStartedMessage, data);
            AddInfoMessage(message);
        }
        
        /// <summary>
        /// LTC停止イベント用（カスタマイズ可能なメッセージ）
        /// </summary>
        public void LogLTCStopped(LTCEventData data)
        {
            string message = FormatEventMessage(ltcStoppedMessage, data);
            AddWarningMessage(message);
        }
        
        /// <summary>
        /// LTC受信中イベント用（カスタマイズ可能なメッセージ）
        /// </summary>
        public void LogLTCReceiving(LTCEventData data)
        {
            string message = FormatEventMessage(ltcReceivingMessage, data);
            AddInfoMessage(message);
        }
        
        /// <summary>
        /// LTC信号なしイベント用（カスタマイズ可能なメッセージ）
        /// </summary>
        public void LogLTCNoSignal(LTCEventData data)
        {
            string message = FormatEventMessage(ltcNoSignalMessage, data);
            AddWarningMessage(message);
        }
        
        /// <summary>
        /// タイムコードイベント用（イベント名付き）
        /// </summary>
        public void LogTimecodeEvent(LTCEventData data, string eventName)
        {
            string message = timecodeEventMessageFormat
                .Replace("{eventName}", eventName)
                .Replace("{timecode}", data.currentTimecode)
                .Replace("{signal}", $"{data.signalLevel * 100:F0}");
            
            var debugMsg = new DebugMessage(
                message,
                DebugMessage.TIMECODE_EVENT,
                data.currentTimecode,
                data.signalLevel,
                Color.cyan
            );
            OnDebugMessageAdded(debugMsg);
        }
        
        /// <summary>
        /// メッセージフォーマット処理
        /// </summary>
        private string FormatEventMessage(string format, LTCEventData data)
        {
            return format
                .Replace("{timecode}", data.currentTimecode)
                .Replace("{signal}", $"{data.signalLevel * 100:F0}");
        }
        
        #endregion
        
        #region イベントハンドラー
        
        private void OnLTCStarted(LTCEventData data)
        {
            isReceiving = true;
            lastReceivedTime = DateTime.Now;
        }
        
        private void OnLTCStopped(LTCEventData data)
        {
            isReceiving = false;
        }
        
        private void OnLTCReceiving(LTCEventData data)
        {
            lastReceivedTime = DateTime.Now;
        }
        
        private void OnClearButtonClicked()
        {
            debugger?.ClearHistory();
            debugger?.ResetStatistics();
            
            // 直接追加されたメッセージもクリア
            OnHistoryCleared();
        }
        
        private void OnExportButtonClicked()
        {
            debugger?.SaveDebugLog();
        }
        
        private void OnCopyButtonClicked()
        {
            debugger?.CopyToClipboard();
        }
        
        private void OnFilterChanged(int index)
        {
            var options = new[] { "All", "Info", "Event", "Warning", "Error", "Debug" };
            if (index >= 0 && index < options.Length)
            {
                currentFilter = options[index];
                RefreshMessageDisplay();
            }
        }
        
        #endregion
        
        #region ユーティリティ
        
        private void RefreshMessageDisplay()
        {
            // 既存のメッセージUIをクリア
            OnHistoryCleared();
            
            // フィルタに応じて再表示
            if (debugger != null)
            {
                var messages = currentFilter == "All" 
                    ? debugger.Messages 
                    : debugger.GetFilteredMessages(currentFilter);
                
                foreach (var msg in messages)
                {
                    CreateMessageUI(msg);
                }
            }
        }
        
        private void SetIndicatorColor(Image indicator, Color color)
        {
            if (indicator != null)
            {
                indicator.color = color;
            }
        }
        
        private void UpdateStatisticsDisplay()
        {
            if (debugger == null) return;
            
            // セッション情報
            if (sessionInfoText != null)
            {
                var sessionStats = debugger.GetSessionStatistics();
                if (sessionStats.isActive)
                {
                    sessionInfoText.text = $"Session: {sessionStats.duration:hh\\:mm\\:ss} | Events: {sessionStats.totalEvents}";
                }
                else
                {
                    sessionInfoText.text = "Session: Inactive";
                }
            }
            
            // 信号品質
            if (signalQualityText != null)
            {
                var qualityReport = debugger.GetSignalQualityReport();
                signalQualityText.text = $"Quality: {qualityReport.qualityScore:F0}% | Stability: {qualityReport.stability:P0}";
                
                // 品質に応じて色を変更
                if (qualityReport.qualityScore > 80)
                    signalQualityText.color = activeColor;
                else if (qualityReport.qualityScore > 50)
                    signalQualityText.color = warningColor;
                else
                    signalQualityText.color = errorColor;
            }
            
            // 異常カウント
            if (anomalyCountText != null)
            {
                var anomalies = debugger.GetAnomalyReport();
                var sessionStats = debugger.GetSessionStatistics();
                anomalyCountText.text = $"Anomalies: {anomalies.Count} | Dropouts: {sessionStats.dropoutCount}";
                
                if (anomalies.Count > 0)
                    anomalyCountText.color = warningColor;
                else
                    anomalyCountText.color = Color.white;
            }
        }
        
        #endregion
    }
}