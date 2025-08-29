using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using LTC.Timeline;
using LTC.Debug;

namespace LTC.UI
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
        
        #endregion
        
        #region フィールド
        
        [Header("Settings")]
        [SerializeField] private float updateInterval = 0.1f;
        [SerializeField] private bool autoScroll = true;
        
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
            
            if (decoder == null)
            {
                UnityEngine.Debug.LogError("LTCDecoderUI: LTCDecoder not found! Please assign in Inspector or ensure one exists in the scene.");
                enabled = false;
                return;
            }
            
            // UI初期化
            InitializeUI();
            
            // イベント購読
            if (debugger != null)
            {
                debugger.OnMessageAdded += OnDebugMessageAdded;
                debugger.OnHistoryCleared += OnHistoryCleared;
            }
            
            // LTCイベント購読
            decoder.OnLTCStarted.AddListener(OnLTCStarted);
            decoder.OnLTCStopped.AddListener(OnLTCStopped);
            decoder.OnLTCReceiving.AddListener(OnLTCReceiving);
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
        
        #endregion
    }
}