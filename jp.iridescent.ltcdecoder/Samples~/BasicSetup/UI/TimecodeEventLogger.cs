using UnityEngine;
using jp.iridescent.ltcdecoder;

namespace jp.iridescent.ltcdecoder.Samples
{
    /// <summary>
    /// TimecodeEvent用のログヘルパー
    /// TimecodeEventのonTimecodeReachedに登録して使用
    /// </summary>
    public class TimecodeEventLogger : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("LTCDecoderUI to output logs / ログを出力するLTCDecoderUI")]
        [SerializeField] private LTCDecoderUI targetUI;
        
        [Tooltip("Event name (displayed in logs) / イベント名（ログに表示される）")]
        [SerializeField] private string eventName = "TimecodeEvent";
        
        [Header("Options")]
        [Tooltip("Automatically search for LTCDecoderUI / 自動的にLTCDecoderUIを検索する")]
        [SerializeField] private bool autoFindUI = true;
        
        void Start()
        {
            if (autoFindUI && targetUI == null)
            {
                targetUI = FindObjectOfType<LTCDecoderUI>();
                if (targetUI == null)
                {
                    UnityEngine.Debug.LogWarning($"[TimecodeEventLogger] LTCDecoderUI not found! Please assign manually or ensure one exists in the scene.");
                }
            }
        }
        
        /// <summary>
        /// TimecodeEventが発火した時に呼ばれるメソッド
        /// </summary>
        public void LogEvent(LTCEventData data)
        {
            if (targetUI != null)
            {
                targetUI.LogTimecodeEvent(data, eventName);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[TimecodeEventLogger] Cannot log event '{eventName}' - LTCDecoderUI is not assigned!");
            }
        }
        
        /// <summary>
        /// イベント名を設定
        /// </summary>
        public void SetEventName(string name)
        {
            eventName = name;
        }
        
        /// <summary>
        /// ターゲットUIを設定
        /// </summary>
        public void SetTargetUI(LTCDecoderUI ui)
        {
            targetUI = ui;
        }
    }
}