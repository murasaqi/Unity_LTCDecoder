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
    /// LTCイベントデバッグユーティリティ
    /// イベントのログ記録、フィルタリング、エクスポート機能を提供
    /// </summary>
    [RequireComponent(typeof(LTCDecoder))]
    public class LTCEventDebugger : MonoBehaviour
    {
        #region フィールド
        
        [Header("デバッグ設定")]
        [SerializeField] private bool enableDebugger = true;
        [SerializeField] private int maxHistorySize = 100;
        [SerializeField] private bool logToConsole = false;
        
        // コンポーネント参照
        private LTCDecoder ltcDecoder;
        
        // メッセージ履歴
        private Queue<DebugMessage> messageHistory = new Queue<DebugMessage>();
        
        // イベント統計
        private Dictionary<string, int> eventStatistics = new Dictionary<string, int>();
        
        // パフォーマンス計測
        private Dictionary<string, float> performanceTimers = new Dictionary<string, float>();
        
        // イベント
        public event Action<DebugMessage> OnMessageAdded;
        public event Action OnHistoryCleared;
        
        #endregion
        
        #region プロパティ
        
        public bool IsEnabled => enableDebugger;
        public int MessageCount => messageHistory.Count;
        public IReadOnlyList<DebugMessage> Messages => messageHistory.ToList();
        
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
        }
        
        void OnEnable()
        {
            if (!enableDebugger) return;
            
            // LTCイベントを購読
            ltcDecoder.OnLTCStarted.AddListener(HandleLTCStarted);
            ltcDecoder.OnLTCStopped.AddListener(HandleLTCStopped);
            ltcDecoder.OnLTCReceiving.AddListener(HandleLTCReceiving);
            ltcDecoder.OnLTCNoSignal.AddListener(HandleLTCNoSignal);
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
        
        #endregion
        
        #region パブリックメソッド - メッセージ管理
        
        /// <summary>
        /// デバッグメッセージを追加
        /// </summary>
        public void AddDebugMessage(string message, string category = null, Color? color = null)
        {
            if (!enableDebugger) return;
            
            category = category ?? DebugMessage.INFO;
            
            var debugMsg = new DebugMessage(
                message,
                category,
                ltcDecoder.CurrentTimecode,
                ltcDecoder.SignalLevel,
                color
            );
            
            AddMessageInternal(debugMsg);
        }
        
        /// <summary>
        /// フォーマット付きデバッグメッセージを追加
        /// </summary>
        public void AddDebugMessageFormat(string format, string category, params object[] args)
        {
            AddDebugMessage(string.Format(format, args), category);
        }

        /// <summary>
        /// UnityEvent用シンプルメソッド - 文字列のみ
        /// </summary>
        public void AddDebugMessageSimple(string message)
        {
            AddDebugMessage(message, DebugMessage.INFO);
        }
        
        /// <summary>
        /// UnityEvent用 - イベントカテゴリ
        /// </summary>
        public void AddEventMessage(string message)
        {
            AddDebugMessage(message, DebugMessage.EVENT, Color.green);
        }
        
        /// <summary>
        /// UnityEvent用 - 警告カテゴリ
        /// </summary>
        public void AddWarningMessage(string message)
        {
            AddDebugMessage(message, DebugMessage.WARNING, Color.yellow);
        }
        
        /// <summary>
        /// UnityEvent用 - エラーカテゴリ
        /// </summary>
        public void AddErrorMessage(string message)
        {
            AddDebugMessage(message, DebugMessage.ERROR, Color.red);
        }
        
        /// <summary>
        /// UnityEvent用 - LTCEventDataを使用
        /// </summary>
        public void AddDebugMessageFromEvent(LTCEventData eventData)
        {
            string message = $"Event at {eventData.currentTimecode} (Signal: {eventData.signalLevel:P0})";
            AddDebugMessage(message, DebugMessage.EVENT);
        }
        
        /// <summary>
        /// イベント履歴をクリア
        /// </summary>
        public void ClearHistory()
        {
            messageHistory.Clear();
            OnHistoryCleared?.Invoke();
            
            if (logToConsole)
                UnityEngine.Debug.Log("[LTC Debug] History cleared");
        }
        
        /// <summary>
        /// 統計をリセット
        /// </summary>
        public void ResetStatistics()
        {
            eventStatistics.Clear();
            
            if (logToConsole)
                UnityEngine.Debug.Log("[LTC Debug] Statistics reset");
        }
        
        #endregion
        
        #region パブリックメソッド - フィルタリング
        
        /// <summary>
        /// カテゴリでフィルタリング
        /// </summary>
        public List<DebugMessage> GetFilteredMessages(string category)
        {
            return messageHistory.Where(m => m.category == category).ToList();
        }
        
        /// <summary>
        /// 複数カテゴリでフィルタリング
        /// </summary>
        public List<DebugMessage> GetFilteredMessages(params string[] categories)
        {
            var categorySet = new HashSet<string>(categories);
            return messageHistory.Where(m => categorySet.Contains(m.category)).ToList();
        }
        
        /// <summary>
        /// 時間範囲でフィルタリング
        /// </summary>
        public List<DebugMessage> GetMessagesInTimeRange(string startTC, string endTC)
        {
            var startTime = TimecodeToSeconds(startTC);
            var endTime = TimecodeToSeconds(endTC);
            
            return messageHistory.Where(m =>
            {
                var msgTime = TimecodeToSeconds(m.timecode);
                return msgTime >= startTime && msgTime <= endTime;
            }).ToList();
        }
        
        /// <summary>
        /// キーワード検索
        /// </summary>
        public List<DebugMessage> SearchMessages(string keyword)
        {
            var lowerKeyword = keyword.ToLower();
            return messageHistory.Where(m => 
                m.message.ToLower().Contains(lowerKeyword) ||
                m.category.ToLower().Contains(lowerKeyword)
            ).ToList();
        }
        
        #endregion
        
        #region パブリックメソッド - 統計
        
        /// <summary>
        /// イベント統計を取得
        /// </summary>
        public Dictionary<string, int> GetEventStatistics()
        {
            return new Dictionary<string, int>(eventStatistics);
        }
        
        /// <summary>
        /// カテゴリ別統計を取得
        /// </summary>
        public Dictionary<string, int> GetCategoryStatistics()
        {
            return messageHistory
                .GroupBy(m => m.category)
                .ToDictionary(g => g.Key, g => g.Count());
        }
        
        #endregion
        
        #region パブリックメソッド - パフォーマンス計測
        
        /// <summary>
        /// パフォーマンス計測開始
        /// </summary>
        public void StartPerformanceMeasure(string label)
        {
            performanceTimers[label] = Time.realtimeSinceStartup;
        }
        
        /// <summary>
        /// パフォーマンス計測終了
        /// </summary>
        public void EndPerformanceMeasure(string label)
        {
            if (performanceTimers.ContainsKey(label))
            {
                float duration = Time.realtimeSinceStartup - performanceTimers[label];
                AddDebugMessage(
                    $"Performance [{label}]: {duration * 1000:F2}ms",
                    DebugMessage.PERFORMANCE,
                    Color.magenta
                );
                performanceTimers.Remove(label);
            }
        }
        
        #endregion
        
        #region パブリックメソッド - エクスポート
        
        /// <summary>
        /// CSV形式でエクスポート
        /// </summary>
        public string ExportAsCSV()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,Category,Message,Timecode,SignalLevel");
            
            foreach (var msg in messageHistory)
            {
                sb.AppendLine(msg.ToCSV());
            }
            
            return sb.ToString();
        }
        
        /// <summary>
        /// JSON形式でエクスポート
        /// </summary>
        public string ExportAsJSON()
        {
            var messages = messageHistory.Select(m => m.ToJSON()).ToList();
            return "[\n" + string.Join(",\n", messages) + "\n]";
        }
        
        /// <summary>
        /// ファイルに保存
        /// </summary>
        public void SaveDebugLog(string filepath = null)
        {
            if (string.IsNullOrEmpty(filepath))
            {
                filepath = Path.Combine(Application.dataPath, $"LTCDebugLog_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
            }
            
            try
            {
                File.WriteAllText(filepath, ExportAsCSV());
                UnityEngine.Debug.Log($"[LTC Debug] Log saved to: {filepath}");
            }
            catch (Exception e)
            {
                UnityEngine.Debug.LogError($"[LTC Debug] Failed to save log: {e.Message}");
            }
        }
        
        /// <summary>
        /// クリップボードにコピー
        /// </summary>
        public void CopyToClipboard()
        {
            GUIUtility.systemCopyBuffer = ExportAsCSV();
            AddDebugMessage("Debug log copied to clipboard", DebugMessage.INFO);
        }
        
        #endregion
        
        #region イベントハンドラー
        
        private void HandleLTCStarted(LTCEventData data)
        {
            AddDebugMessage(
                $"LTC Started at {data.currentTimecode}",
                DebugMessage.EVENT,
                Color.green
            );
            UpdateStatistics("LTC Started");
        }
        
        private void HandleLTCStopped(LTCEventData data)
        {
            AddDebugMessage(
                $"LTC Stopped at {data.currentTimecode}",
                DebugMessage.EVENT,
                Color.yellow
            );
            UpdateStatistics("LTC Stopped");
        }
        
        private void HandleLTCReceiving(LTCEventData data)
        {
            // 頻度が高いので通常はログしない
            // 必要に応じて有効化
            if (UnityEngine.Random.value < 0.001f) // 0.1%の確率でサンプリング
            {
                AddDebugMessage(
                    $"Receiving: {data.currentTimecode} (Signal: {data.signalLevel:P0})",
                    DebugMessage.DEBUG,
                    Color.gray
                );
            }
        }
        
        private void HandleLTCNoSignal(LTCEventData data)
        {
            // 連続したNoSignalは最初の一回だけログ
            var lastMsg = messageHistory.LastOrDefault();
            if (lastMsg == null || !lastMsg.message.StartsWith("No Signal"))
            {
                AddDebugMessage(
                    "No Signal detected",
                    DebugMessage.WARNING,
                    Color.yellow
                );
                UpdateStatistics("No Signal");
            }
        }
        
        #endregion
        
        #region プライベートメソッド
        
        private void AddMessageInternal(DebugMessage message)
        {
            // 履歴に追加
            messageHistory.Enqueue(message);
            
            // 最大サイズを超えたら古いものを削除
            while (messageHistory.Count > maxHistorySize)
            {
                messageHistory.Dequeue();
            }
            
            // コンソールログ
            if (logToConsole)
            {
                UnityEngine.Debug.Log(message.GetFormattedMessage());
            }
            
            // イベント発火
            OnMessageAdded?.Invoke(message);
        }
        
        private void UpdateStatistics(string eventName)
        {
            if (!eventStatistics.ContainsKey(eventName))
            {
                eventStatistics[eventName] = 0;
            }
            eventStatistics[eventName]++;
        }
        
        private float TimecodeToSeconds(string timecode)
        {
            try
            {
                var parts = timecode.Split(':');
                if (parts.Length == 4)
                {
                    int hours = int.Parse(parts[0]);
                    int minutes = int.Parse(parts[1]);
                    int seconds = int.Parse(parts[2]);
                    int frames = int.Parse(parts[3]);
                    
                    float frameRate = 30f; // デフォルト値
                    return hours * 3600f + minutes * 60f + seconds + frames / frameRate;
                }
            }
            catch { }
            
            return 0f;
        }
        
        #endregion
    }
}