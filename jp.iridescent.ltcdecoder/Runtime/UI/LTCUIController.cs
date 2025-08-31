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
        public Text currentTimecodeText;  // メイン表示用（タイムコード、ステータス、シグナルレベルを統合）
        public Text statusText;  // 互換性のため残すが使用しない
        public Text signalLevelText;  // 互換性のため残すが使用しない
        public Image signalLevelBar;  // 互換性のため残すが使用しない
        public Transform debugMessageContainer;
        public ScrollRect debugScrollRect;
        
        [Header("Audio Settings UI")]
        public Dropdown deviceDropdown;
        public Dropdown frameRateDropdown;
        public Dropdown sampleRateDropdown;
        
        [Header("Target Components")]
        [SerializeField] private LTCDecoder ltcDecoder;
        [SerializeField] private LTCEventDebugger ltcEventDebugger;
        
        [Header("Debug Message Settings")]
        [SerializeField] private int maxDebugMessages = 100;
        
        private Queue<GameObject> debugMessagePool = new Queue<GameObject>();
        public List<GameObject> activeDebugMessages = new List<GameObject>();
        
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
                // デバッガーが有効であることを確認
                if (!ltcEventDebugger.IsEnabled)
                {
                    ltcEventDebugger.IsEnabled = true;
                }
                
                // OnDebugMessageWithColorイベントのリスナーを設定（色情報付き）
                ltcEventDebugger.OnDebugMessageWithColor += AddDebugMessage;
                
                // 初期化メッセージを追加（すぐに表示されるようにStartとして呼び出し）
                StartCoroutine(ShowInitialMessages());
            }
            
            // Audio Settings UIの初期化
            InitializeAudioSettingsUI();
        }
        
        /// <summary>
        /// Audio Settings UIの初期化
        /// </summary>
        private void InitializeAudioSettingsUI()
        {
            if (ltcDecoder == null) return;
            
            // デバイスドロップダウンの初期化
            if (deviceDropdown != null)
            {
                RefreshDeviceList();
                deviceDropdown.onValueChanged.AddListener(OnDeviceChanged);
            }
            
            // フレームレートドロップダウンの初期化
            if (frameRateDropdown != null)
            {
                SetupFrameRateDropdown();
                frameRateDropdown.onValueChanged.AddListener(OnFrameRateChanged);
            }
            
            // サンプルレートドロップダウンの初期化
            if (sampleRateDropdown != null)
            {
                SetupSampleRateDropdown();
                sampleRateDropdown.onValueChanged.AddListener(OnSampleRateChanged);
            }
        }
        
        private void RefreshDeviceList()
        {
            if (deviceDropdown == null || ltcDecoder == null) return;
            
            deviceDropdown.ClearOptions();
            
            string[] devices = ltcDecoder.AvailableDevices;
            if (devices.Length == 0)
            {
                deviceDropdown.AddOptions(new List<string> { "No devices found" });
                return;
            }
            
            deviceDropdown.AddOptions(new List<string>(devices));
            
            // 現在選択されているデバイスを選択
            string currentDevice = ltcDecoder.SelectedDevice;
            int index = System.Array.IndexOf(devices, currentDevice);
            if (index >= 0)
            {
                deviceDropdown.value = index;
            }
        }
        
        private void SetupFrameRateDropdown()
        {
            if (frameRateDropdown == null) return;
            
            frameRateDropdown.ClearOptions();
            
            List<string> options = new List<string>
            {
                "24 fps",
                "25 fps",
                "29.97 fps (Drop Frame)",
                "29.97 fps (Non-Drop)",
                "30 fps"
            };
            
            frameRateDropdown.AddOptions(options);
            
            // 現在の値を選択
            LTCDecoder.LTCFrameRate currentRate = ltcDecoder.FrameRate;
            int index = GetFrameRateIndex(currentRate);
            frameRateDropdown.value = index;
        }
        
        private void SetupSampleRateDropdown()
        {
            if (sampleRateDropdown == null) return;
            
            sampleRateDropdown.ClearOptions();
            
            List<string> options = new List<string>
            {
                "44100 Hz",
                "48000 Hz",
                "96000 Hz"
            };
            
            sampleRateDropdown.AddOptions(options);
            
            // 現在の値を選択
            int currentRate = ltcDecoder.SampleRate;
            int index = GetSampleRateIndex(currentRate);
            sampleRateDropdown.value = index;
        }
        
        private int GetFrameRateIndex(LTCDecoder.LTCFrameRate frameRate)
        {
            switch (frameRate)
            {
                case LTCDecoder.LTCFrameRate.FPS_24: return 0;
                case LTCDecoder.LTCFrameRate.FPS_25: return 1;
                case LTCDecoder.LTCFrameRate.FPS_29_97_DF: return 2;
                case LTCDecoder.LTCFrameRate.FPS_29_97_NDF: return 3;
                case LTCDecoder.LTCFrameRate.FPS_30: return 4;
                default: return 4;
            }
        }
        
        private int GetSampleRateIndex(int sampleRate)
        {
            switch (sampleRate)
            {
                case 44100: return 0;
                case 48000: return 1;
                case 96000: return 2;
                default: return 1;
            }
        }
        
        private void OnDeviceChanged(int index)
        {
            if (deviceDropdown == null || ltcDecoder == null) return;
            
            string[] devices = ltcDecoder.AvailableDevices;
            if (index >= 0 && index < devices.Length)
            {
                ltcDecoder.SetDevice(devices[index]);
                
                if (ltcEventDebugger != null)
                {
                    ltcEventDebugger.AddDebugMessage($"Audio device changed to: {devices[index]}", "SETTINGS", Color.cyan);
                }
            }
        }
        
        private void OnFrameRateChanged(int index)
        {
            if (frameRateDropdown == null || ltcDecoder == null) return;
            
            LTCDecoder.LTCFrameRate newRate = LTCDecoder.LTCFrameRate.FPS_30;
            
            switch (index)
            {
                case 0: newRate = LTCDecoder.LTCFrameRate.FPS_24; break;
                case 1: newRate = LTCDecoder.LTCFrameRate.FPS_25; break;
                case 2: newRate = LTCDecoder.LTCFrameRate.FPS_29_97_DF; break;
                case 3: newRate = LTCDecoder.LTCFrameRate.FPS_29_97_NDF; break;
                case 4: newRate = LTCDecoder.LTCFrameRate.FPS_30; break;
            }
            
            ltcDecoder.SetLTCFrameRate(newRate);
            
            if (ltcEventDebugger != null)
            {
                ltcEventDebugger.AddDebugMessage($"Frame rate changed to: {frameRateDropdown.options[index].text}", "SETTINGS", Color.cyan);
            }
        }
        
        private void OnSampleRateChanged(int index)
        {
            if (sampleRateDropdown == null || ltcDecoder == null) return;
            
            int newRate = 48000;
            
            switch (index)
            {
                case 0: newRate = 44100; break;
                case 1: newRate = 48000; break;
                case 2: newRate = 96000; break;
            }
            
            ltcDecoder.SetSampleRate(newRate);
            
            if (ltcEventDebugger != null)
            {
                ltcEventDebugger.AddDebugMessage($"Sample rate changed to: {newRate} Hz", "SETTINGS", Color.cyan);
            }
        }
        
        /// <summary>
        /// 初期メッセージを表示（少し遅延させて確実に表示）
        /// </summary>
        private System.Collections.IEnumerator ShowInitialMessages()
        {
            yield return null; // 1フレーム待機
            
            if (ltcEventDebugger != null)
            {
                // システムメッセージをシアン色で
                ltcEventDebugger.AddDebugMessage("LTC UI Controller initialized", "SYSTEM", new Color(0.5f, 1f, 1f));
                ltcEventDebugger.AddDebugMessage($"Connected to LTC Decoder", "SYSTEM", new Color(0.5f, 1f, 1f));
                
                // デバッガーの状態を確認
                if (ltcEventDebugger.IsEnabled)
                {
                    ltcEventDebugger.AddDebugMessage("Debug logging is enabled", "INFO", Color.white);
                }
                
                // デコーダーの状態を表示
                if (ltcDecoder != null)
                {
                    ltcEventDebugger.AddDebugMessage($"LTC Decoder found and connected", "SYSTEM", new Color(0.5f, 1f, 1f));
                }
                else
                {
                    ltcEventDebugger.AddDebugMessage("Warning: LTC Decoder not connected", "WARNING", new Color(1f, 0.7f, 0f));
                }
            }
        }
        
        void Start()
        {
            // Startで自動的にSetupを試みる（コンポーネントが設定されている場合）
            if (ltcDecoder != null && ltcEventDebugger != null)
            {
                Setup(ltcDecoder, ltcEventDebugger);
            }
        }
        
        void OnDestroy()
        {
            // イベントリスナーの解除
            if (ltcEventDebugger != null)
            {
                ltcEventDebugger.OnDebugMessageWithColor -= AddDebugMessage;
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
        /// タイムコード表示を更新（固定幅対応・1行統合表示）
        /// </summary>
        private void UpdateTimecodeDisplay()
        {
            if (currentTimecodeText == null || ltcDecoder == null) return;
            
            // タイムコード取得
            string timecode = ltcDecoder.CurrentTimecode;
            if (string.IsNullOrEmpty(timecode))
            {
                timecode = "00:00:00:00";
            }
            
            // ステータス取得（短縮形）
            bool hasSignal = ltcDecoder.HasSignal;
            string status = hasSignal ? "REC" : "---";
            Color statusColor = hasSignal ? Color.green : Color.gray;
            string statusHex = ColorUtility.ToHtmlStringRGB(statusColor);
            
            // シグナルレベル取得
            float signalLevel = ltcDecoder.SignalLevel;
            int signalPercent = Mathf.RoundToInt(signalLevel * 100);
            
            // シグナルバー生成（テキストベース）
            int barLength = 10;
            int filledBars = Mathf.RoundToInt(signalLevel * barLength);
            string signalBar = new string('█', filledBars) + new string('░', barLength - filledBars);
            
            // シグナルバーの色
            Color barColor = signalLevel > 0.5f ? Color.green : 
                            signalLevel > 0.2f ? Color.yellow : Color.red;
            string barHex = ColorUtility.ToHtmlStringRGB(barColor);
            
            // 1行統合表示（等幅フォント推奨）
            // フォーマット: [REC] 01:23:45:12 | ████████░░ 85%
            currentTimecodeText.text = $"<color=#{statusHex}>[{status}]</color> <b>{timecode}</b> | <color=#{barHex}>{signalBar}</color> {signalPercent:D3}%";
            
            // 等幅フォント設定（初回のみ）
            if (currentTimecodeText.font == null || !currentTimecodeText.font.name.Contains("Consola"))
            {
                // Consolasまたは等幅フォントを設定
                Font monoFont = Font.CreateDynamicFontFromOSFont("Consolas", currentTimecodeText.fontSize);
                if (monoFont != null)
                {
                    currentTimecodeText.font = monoFont;
                }
            }
        }
        
        /// <summary>
        /// ステータス表示を更新（互換性のため残すが、実際の表示はUpdateTimecodeDisplay()で行う）
        /// </summary>
        private void UpdateStatusDisplay()
        {
            // 互換性のため残すが、実際の処理はUpdateTimecodeDisplay()に統合
            // 個別のstatusTextは使用しない
        }
        
        /// <summary>
        /// シグナルレベル表示を更新（互換性のため残すが、実際の表示はUpdateTimecodeDisplay()で行う）
        /// </summary>
        private void UpdateSignalLevel()
        {
            // 互換性のため残すが、実際の処理はUpdateTimecodeDisplay()に統合
            // 個別のsignalLevelTextとsignalLevelBarは使用しない
        }
        
        /// <summary>
        /// デバッグメッセージを追加
        /// </summary>
        public void AddDebugMessage(string message)
        {
            AddDebugMessage(message, Color.white);
        }
        
        /// <summary>
        /// デバッグメッセージを追加（色指定付き）
        /// </summary>
        public void AddDebugMessage(string message, Color color)
        {
            if (debugMessageContainer == null) return;
            
            GameObject msgObj = GetOrCreateDebugMessageObject();
            Text msgText = msgObj.GetComponent<Text>();
            if (msgText != null)
            {
                msgText.text = $"[{System.DateTime.Now:HH:mm:ss.fff}] {message}";
                msgText.color = color;  // 色を適用
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
                
                // プールから取得したオブジェクトの設定をリセット
                Text text = msgObj.GetComponent<Text>();
                if (text != null)
                {
                    text.text = "";  // テキストをクリア
                }
            }
            else
            {
                // 常に新規作成
                msgObj = new GameObject("DebugMessage");
                
                // RectTransformの設定
                RectTransform rectTransform = msgObj.AddComponent<RectTransform>();
                rectTransform.anchorMin = new Vector2(0, 1);
                rectTransform.anchorMax = new Vector2(1, 1);
                rectTransform.pivot = new Vector2(0.5f, 1);
                
                Text text = msgObj.AddComponent<Text>();
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 11;  // フォントサイズを少し小さく
                text.color = Color.white;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;  // テキストを折り返し
                text.verticalOverflow = VerticalWrapMode.Overflow;  // 垂直方向はオーバーフロー許可
                
                // ContentSizeFitterでテキストに合わせて高さを自動調整
                ContentSizeFitter sizeFitter = msgObj.AddComponent<ContentSizeFitter>();
                sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
                sizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                
                // LayoutElementで最小高さのみ設定（最大高さは設定しない）
                LayoutElement layout = msgObj.AddComponent<LayoutElement>();
                layout.minHeight = 12;  // 最小高さのみ設定
                layout.preferredWidth = 0;  // 幅は親に合わせる
                layout.flexibleWidth = 1;  // 幅を柔軟に
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