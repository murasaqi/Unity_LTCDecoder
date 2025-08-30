using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace jp.iridescent.ltcdecoder
{
    /// <summary>
    /// LTC UIコントローラー
    /// LTCDecoderとLTCEventDebuggerの情報をUIに表示
    /// </summary>
    public class LTCUIController : MonoBehaviour
    {
        [Header("UI References")]
        public Text currentTimecodeText;
        public Text decodedTimecodeText;
        public Text statusText;
        public Text signalLevelText;
        public Image signalLevelBar;
        public Transform debugMessageContainer;
        public ScrollRect debugScrollRect;
        
        [Header("Target Components")]
        [SerializeField] private LTCDecoder ltcDecoder;
        [SerializeField] private LTCEventDebugger ltcEventDebugger;
        
        [Header("Debug Message Settings")]
        [SerializeField] private int maxDebugMessages = 100;
        
        private Queue<GameObject> debugMessagePool = new Queue<GameObject>();
        private List<GameObject> activeDebugMessages = new List<GameObject>();
        
        /// <summary>
        /// LTCDecoderとLTCEventDebuggerを設定
        /// </summary>
        public void Setup(LTCDecoder decoder, LTCEventDebugger debugger = null)
        {
            ltcDecoder = decoder;
            ltcEventDebugger = debugger;
            
            // イベントリスナーの設定
            if (ltcEventDebugger != null)
            {
                ltcEventDebugger.OnDebugMessage += AddDebugMessage;
            }
        }
        
        void OnDestroy()
        {
            // イベントリスナーの解除
            if (ltcEventDebugger != null)
            {
                ltcEventDebugger.OnDebugMessage -= AddDebugMessage;
            }
        }
        
        void Update()
        {
            if (ltcDecoder == null) return;
            
            // タイムコード更新
            UpdateTimecodeDisplay();
            
            // ステータス更新
            UpdateStatusDisplay();
            
            // シグナルレベル更新
            UpdateSignalLevel();
        }
        
        /// <summary>
        /// タイムコード表示を更新
        /// </summary>
        private void UpdateTimecodeDisplay()
        {
            if (currentTimecodeText != null)
            {
                currentTimecodeText.text = ltcDecoder.CurrentTimecode;
            }
            
            if (decodedTimecodeText != null)
            {
                decodedTimecodeText.text = ltcDecoder.DecodedTimecode;
            }
        }
        
        /// <summary>
        /// ステータス表示を更新
        /// </summary>
        private void UpdateStatusDisplay()
        {
            if (statusText != null)
            {
                if (ltcDecoder.HasSignal)
                {
                    statusText.text = "RECEIVING";
                    statusText.color = Color.green;
                }
                else
                {
                    statusText.text = "NO SIGNAL";
                    statusText.color = Color.yellow;
                }
            }
        }
        
        /// <summary>
        /// シグナルレベル表示を更新
        /// </summary>
        private void UpdateSignalLevel()
        {
            float signalLevel = ltcDecoder.SignalLevel;
            
            if (signalLevelText != null)
            {
                signalLevelText.text = $"{(int)(signalLevel * 100)}%";
            }
            
            if (signalLevelBar != null)
            {
                // バーの幅を更新（最大幅150pxに対する割合）
                RectTransform barRect = signalLevelBar.rectTransform;
                float maxWidth = 150f; // 固定幅
                barRect.sizeDelta = new Vector2(maxWidth * signalLevel, barRect.sizeDelta.y);
                
                // 色を更新
                signalLevelBar.color = signalLevel > 0.5f ? Color.green : 
                                       signalLevel > 0.2f ? Color.yellow : Color.red;
            }
        }
        
        /// <summary>
        /// デバッグメッセージを追加
        /// </summary>
        public void AddDebugMessage(string message)
        {
            if (debugMessageContainer == null) return;
            
            GameObject msgObj = GetOrCreateDebugMessageObject();
            Text msgText = msgObj.GetComponent<Text>();
            if (msgText != null)
            {
                msgText.text = $"[{System.DateTime.Now:HH:mm:ss.fff}] {message}";
            }
            
            msgObj.transform.SetParent(debugMessageContainer, false);
            msgObj.SetActive(true);
            activeDebugMessages.Add(msgObj);
            
            // 最大数を超えたら古いメッセージを削除
            while (activeDebugMessages.Count > maxDebugMessages)
            {
                GameObject oldMsg = activeDebugMessages[0];
                activeDebugMessages.RemoveAt(0);
                ReturnToPool(oldMsg);
            }
            
            // スクロールを最下部に
            if (debugScrollRect != null)
            {
                Canvas.ForceUpdateCanvases();
                debugScrollRect.verticalNormalizedPosition = 0f;
            }
        }
        
        /// <summary>
        /// デバッグメッセージをクリア
        /// </summary>
        public void ClearDebugMessages()
        {
            foreach (var msg in activeDebugMessages)
            {
                ReturnToPool(msg);
            }
            activeDebugMessages.Clear();
        }
        
        /// <summary>
        /// デバッグメッセージオブジェクトを取得または作成
        /// </summary>
        private GameObject GetOrCreateDebugMessageObject()
        {
            GameObject msgObj;
            
            if (debugMessagePool.Count > 0)
            {
                msgObj = debugMessagePool.Dequeue();
            }
            else
            {
                // 常に新規作成
                msgObj = new GameObject("DebugMessage");
                Text text = msgObj.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 12;
                text.color = Color.white;
                
                // LayoutElementを追加して高さを固定
                LayoutElement layout = msgObj.AddComponent<LayoutElement>();
                layout.minHeight = 20;
                layout.preferredHeight = 20;
            }
            
            return msgObj;
        }
        
        /// <summary>
        /// オブジェクトをプールに返却
        /// </summary>
        private void ReturnToPool(GameObject obj)
        {
            obj.SetActive(false);
            debugMessagePool.Enqueue(obj);
        }
        
        /// <summary>
        /// デバッグメッセージをエクスポート
        /// </summary>
        public string ExportDebugMessages()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            foreach (var msgObj in activeDebugMessages)
            {
                Text text = msgObj.GetComponent<Text>();
                if (text != null)
                {
                    sb.AppendLine(text.text);
                }
            }
            return sb.ToString();
        }
        
        /// <summary>
        /// デバッグメッセージをクリップボードにコピー
        /// </summary>
        public void CopyDebugMessagesToClipboard()
        {
            string messages = ExportDebugMessages();
            GUIUtility.systemCopyBuffer = messages;
            Debug.Log("[LTCUIController] Debug messages copied to clipboard");
        }
    }
}