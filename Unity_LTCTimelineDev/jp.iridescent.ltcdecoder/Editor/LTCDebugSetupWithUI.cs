using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using jp.iridescent.ltcdecoder;
using System.Reflection;
using System.Linq;
using System;

namespace jp.iridescent.ltcdecoder.Editor
{
    /// <summary>
    /// LTCデバッグUI付きセットアップユーティリティ
    /// </summary>
    public static class LTCDebugSetupWithUI
    {
        [MenuItem("GameObject/LTC Decoder/Create LTC Decoder with UI", false, 12)]
        public static void CreateLTCDecoderWithUI()
        {
            // LTCDecoder + Debugger作成
            GameObject ltcObject = CreateLTCDecoderObject();
            
            // Debug UI Canvas作成
            GameObject canvas = CreateDebugCanvas();
            
            // UI要素作成
            GameObject mainPanel = CreateCompleteUI(canvas, ltcObject);
            
            // UIマネジメントコンポーネント追加
            AddUIManagementComponent(mainPanel, ltcObject);
            
            // 選択
            Selection.activeGameObject = ltcObject;
            
            UnityEngine.Debug.Log("[LTC Decoder Setup] LTC Decoder with UI created successfully!");
        }
        
        /// <summary>
        /// LTCDecoder + Debuggerオブジェクト作成
        /// </summary>
        private static GameObject CreateLTCDecoderObject()
        {
            GameObject ltcObject = new GameObject("LTC Decoder");
            
            // LTCDecoder追加
            LTCDecoder decoder = ltcObject.AddComponent<LTCDecoder>();
            
            // LTCEventDebugger追加
            LTCEventDebugger debugger = ltcObject.AddComponent<LTCEventDebugger>();
            
            // デフォルト設定
            SetupDefaultSettings(decoder, debugger);
            
            return ltcObject;
        }
        
        /// <summary>
        /// デフォルト設定
        /// </summary>
        private static void SetupDefaultSettings(LTCDecoder decoder, LTCEventDebugger debugger)
        {
            if (decoder != null)
            {
                SerializedObject decoderSO = new SerializedObject(decoder);
                var enableDebugProp = decoderSO.FindProperty("enableDebugMode");
                if (enableDebugProp != null) enableDebugProp.boolValue = true;
                var logDebugProp = decoderSO.FindProperty("logDebugInfo");
                if (logDebugProp != null) logDebugProp.boolValue = true;
                var logConsoleProp = decoderSO.FindProperty("logToConsole");
                if (logConsoleProp != null) logConsoleProp.boolValue = false;
                decoderSO.ApplyModifiedProperties();
            }
            
            if (debugger != null)
            {
                SerializedObject debuggerSO = new SerializedObject(debugger);
                var enableLogProp = debuggerSO.FindProperty("enableLogging");
                if (enableLogProp != null) enableLogProp.boolValue = true;
                var logConsoleProp = debuggerSO.FindProperty("logToConsole");
                if (logConsoleProp != null) logConsoleProp.boolValue = false;
                var maxHistoryProp = debuggerSO.FindProperty("maxHistorySize");
                if (maxHistoryProp != null) maxHistoryProp.intValue = 100;
                debuggerSO.ApplyModifiedProperties();
            }
        }
        
        /// <summary>
        /// Debug UI Canvas作成
        /// </summary>
        private static GameObject CreateDebugCanvas()
        {
            // EventSystem確認
            if (!GameObject.FindObjectOfType<EventSystem>())
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<EventSystem>();
                eventSystem.AddComponent<StandaloneInputModule>();
            }
            
            // Canvas作成
            GameObject canvasObject = new GameObject("LTC Debug Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            canvasObject.AddComponent<GraphicRaycaster>();
            
            return canvasObject;
        }
        
        /// <summary>
        /// 完全なUI作成
        /// </summary>
        private static GameObject CreateCompleteUI(GameObject canvas, GameObject ltcObject)
        {
            // メインパネル - 位置を調整して画面内に収める
            GameObject mainPanel = CreatePanel(canvas, "Main Panel", 
                new Vector2(380, 600),  // 幅を400から380に縮小
                new Vector2(210, -310), // 画面左上から適切な余白を持つ位置に調整
                new Vector2(0, 1), new Vector2(0, 1));
            
            // Pivotを調整
            RectTransform panelRect = mainPanel.GetComponent<RectTransform>();
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(210, -310);
            
            // 背景色設定
            Image panelImage = mainPanel.GetComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            
            // ヘッダー
            CreateHeader(mainPanel);
            
            // タイムコード表示セクション
            CreateTimecodeSection(mainPanel);
            
            // ステータス表示セクション
            CreateStatusSection(mainPanel);
            
            // コントロールボタン
            CreateControlButtons(mainPanel);
            
            // デバッグメッセージエリア
            CreateDebugMessageArea(mainPanel);
            
            // LTCDecoderの参照を設定
            SetupUIReferences(mainPanel, ltcObject);
            
            return mainPanel;
        }
        
        private static void CreateHeader(GameObject parent)
        {
            GameObject header = CreateTextElement(parent, "Header", "LTC Decoder Debug UI", 
                24, TextAnchor.MiddleCenter,
                new Vector2(0, 280), new Vector2(360, 40),  // 幅を調整
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            
            Text headerText = header.GetComponent<Text>();
            headerText.fontStyle = FontStyle.Bold;
            headerText.color = Color.cyan;
        }
        
        private static void CreateTimecodeSection(GameObject parent)
        {
            // タイムコードラベル
            CreateTextElement(parent, "TC Label", "Current Timecode:", 
                14, TextAnchor.MiddleLeft,
                new Vector2(-180, 230), new Vector2(150, 30),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            
            // タイムコード値
            GameObject tcValue = CreateTextElement(parent, "CurrentTimecodeText", "00:00:00:00", 
                20, TextAnchor.MiddleLeft,
                new Vector2(-30, 230), new Vector2(180, 30),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            tcValue.GetComponent<Text>().fontStyle = FontStyle.Bold;
            
            // デコードタイムコードラベル
            CreateTextElement(parent, "Decoded Label", "Decoded TC:", 
                14, TextAnchor.MiddleLeft,
                new Vector2(-180, 195), new Vector2(150, 30),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            
            // デコードタイムコード値
            CreateTextElement(parent, "DecodedTimecodeText", "00:00:00:00", 
                16, TextAnchor.MiddleLeft,
                new Vector2(-30, 195), new Vector2(180, 30),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        }
        
        private static void CreateStatusSection(GameObject parent)
        {
            // ステータスラベル
            CreateTextElement(parent, "Status Label", "Status:", 
                14, TextAnchor.MiddleLeft,
                new Vector2(-180, 160), new Vector2(80, 30),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            
            // ステータス値
            GameObject statusText = CreateTextElement(parent, "StatusText", "NO SIGNAL", 
                16, TextAnchor.MiddleLeft,
                new Vector2(-100, 160), new Vector2(150, 30),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            statusText.GetComponent<Text>().color = Color.yellow;
            
            // シグナルレベル
            CreateSignalLevelBar(parent);
        }
        
        private static void CreateSignalLevelBar(GameObject parent)
        {
            // ラベル
            CreateTextElement(parent, "Signal Label", "Signal Level:", 
                14, TextAnchor.MiddleLeft,
                new Vector2(-180, 125), new Vector2(100, 30),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            
            // バー背景
            GameObject barBg = new GameObject("SignalLevelBar");
            barBg.transform.SetParent(parent.transform, false);
            RectTransform bgRect = barBg.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0.5f, 0.5f);
            bgRect.anchorMax = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = new Vector2(-10, 125);
            bgRect.sizeDelta = new Vector2(150, 20);  // 幅を200から150に縮小
            
            Image bgImage = barBg.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1);
            
            // バーフィル
            GameObject barFill = new GameObject("Fill");
            barFill.transform.SetParent(barBg.transform, false);
            RectTransform fillRect = barFill.AddComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0, 0);
            fillRect.anchorMax = new Vector2(0, 1);
            fillRect.anchoredPosition = new Vector2(0, 0);
            fillRect.sizeDelta = new Vector2(75, 0);
            fillRect.pivot = new Vector2(0, 0.5f);
            
            Image fillImage = barFill.AddComponent<Image>();
            fillImage.color = Color.green;
            
            // パーセンテージテキスト
            CreateTextElement(parent, "SignalLevelText", "0%", 
                14, TextAnchor.MiddleLeft,
                new Vector2(110, 125), new Vector2(50, 30),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        }
        
        private static void CreateControlButtons(GameObject parent)
        {
            // Clearボタン
            CreateButton(parent, "ClearButton", "Clear", 
                new Vector2(-140, 80), new Vector2(80, 35));
            
            // Exportボタン
            CreateButton(parent, "ExportButton", "Export", 
                new Vector2(-50, 80), new Vector2(80, 35));
            
            // Copyボタン
            CreateButton(parent, "CopyButton", "Copy", 
                new Vector2(40, 80), new Vector2(80, 35));
        }
        
        private static void CreateDebugMessageArea(GameObject parent)
        {
            // スクロールビュー作成
            GameObject scrollView = new GameObject("DebugScrollView");
            scrollView.transform.SetParent(parent.transform, false);
            
            RectTransform scrollRect = scrollView.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0.5f, 0.5f);
            scrollRect.anchorMax = new Vector2(0.5f, 0.5f);
            scrollRect.anchoredPosition = new Vector2(0, -100);
            scrollRect.sizeDelta = new Vector2(360, 300);
            
            ScrollRect scroll = scrollView.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            
            // ビューポート
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollView.transform, false);
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.anchoredPosition = Vector2.zero;
            
            viewport.AddComponent<Mask>();
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0.05f, 0.05f, 0.05f, 1);
            
            // コンテンツ
            GameObject content = new GameObject("DebugMessageContainer");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 0);
            
            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperLeft;
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 2;
            layout.padding = new RectOffset(5, 5, 5, 5);
            
            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            scroll.content = contentRect;
            scroll.viewport = viewportRect;
        }
        
        /// <summary>
        /// UIマネジメントコンポーネントの追加
        /// </summary>
        private static void AddUIManagementComponent(GameObject mainPanel, GameObject ltcObject)
        {
            // まず、Samplesの LTCDecoderUI が利用可能か確認
            System.Type ltcDecoderUIType = GetTypeFromAssemblies("jp.iridescent.ltcdecoder.Samples.LTCDecoderUI");
            
            if (ltcDecoderUIType != null)
            {
                // LTCDecoderUIが存在する場合は追加
                Component uiComponent = mainPanel.AddComponent(ltcDecoderUIType);
                
                // リフレクションで必要なフィールドを設定
                SetFieldValue(uiComponent, "targetDecoder", ltcObject.GetComponent<LTCDecoder>());
                SetFieldValue(uiComponent, "targetDebugger", ltcObject.GetComponent<LTCEventDebugger>());
                
                UnityEngine.Debug.Log("[LTC Decoder Setup] LTCDecoderUI component added successfully");
            }
            else
            {
                // 存在しない場合は簡易UIコントローラーを追加
                SimpleLTCUIController simpleUI = mainPanel.AddComponent<SimpleLTCUIController>();
                simpleUI.Setup(ltcObject.GetComponent<LTCDecoder>());
                
                UnityEngine.Debug.LogWarning("[LTC Decoder Setup] Using simplified UI controller. Import Samples for full functionality.");
            }
        }
        
        /// <summary>
        /// UI要素の参照を設定
        /// </summary>
        private static void SetupUIReferences(GameObject mainPanel, GameObject ltcObject)
        {
            // UI要素の参照を取得
            Transform currentTCText = mainPanel.transform.Find("CurrentTimecodeText");
            Transform decodedTCText = mainPanel.transform.Find("DecodedTimecodeText");
            Transform statusText = mainPanel.transform.Find("StatusText");
            Transform signalLevelText = mainPanel.transform.Find("SignalLevelText");
            Transform signalLevelBar = mainPanel.transform.Find("SignalLevelBar/Fill");
            
            // SimpleLTCUIControllerがあれば参照を設定
            SimpleLTCUIController controller = mainPanel.GetComponent<SimpleLTCUIController>();
            if (controller != null)
            {
                controller.currentTimecodeText = currentTCText?.GetComponent<Text>();
                controller.decodedTimecodeText = decodedTCText?.GetComponent<Text>();
                controller.statusText = statusText?.GetComponent<Text>();
                controller.signalLevelText = signalLevelText?.GetComponent<Text>();
                controller.signalLevelBar = signalLevelBar?.GetComponent<Image>();
            }
        }
        
        // ヘルパーメソッド
        private static System.Type GetTypeFromAssemblies(string typeName)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null) return type;
            }
            return null;
        }
        
        private static void SetFieldValue(object obj, string fieldName, object value)
        {
            if (obj == null) return;
            var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            field?.SetValue(obj, value);
        }
        
        private static GameObject CreatePanel(GameObject parent, string name, Vector2 size, 
            Vector2 position, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent.transform, false);
            
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            
            panel.AddComponent<Image>();
            
            return panel;
        }
        
        private static GameObject CreateTextElement(GameObject parent, string name, string text, 
            int fontSize, TextAnchor alignment, Vector2 position, Vector2 size,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent.transform, false);
            
            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            
            Text textComp = textObj.AddComponent<Text>();
            textComp.text = text;
            textComp.fontSize = fontSize;
            textComp.alignment = alignment;
            textComp.color = Color.white;
            textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            
            return textObj;
        }
        
        private static GameObject CreateButton(GameObject parent, string name, string text, 
            Vector2 position, Vector2 size)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent.transform, false);
            
            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            
            Image image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.3f, 0.3f, 0.3f, 1);
            
            Button button = buttonObj.AddComponent<Button>();
            
            // ボタンテキスト
            GameObject textObj = CreateTextElement(buttonObj, "Text", text, 
                14, TextAnchor.MiddleCenter,
                Vector2.zero, size,
                Vector2.zero, Vector2.one);
            
            return buttonObj;
        }
    }
    
    /// <summary>
    /// 簡易LTC UIコントローラー（LTCDecoderUIが利用できない場合のフォールバック）
    /// </summary>
    public class SimpleLTCUIController : MonoBehaviour
    {
        public Text currentTimecodeText;
        public Text decodedTimecodeText;
        public Text statusText;
        public Text signalLevelText;
        public Image signalLevelBar;
        
        private LTCDecoder decoder;
        
        public void Setup(LTCDecoder ltcDecoder)
        {
            decoder = ltcDecoder;
        }
        
        void Update()
        {
            if (decoder == null) return;
            
            // UIを更新
            if (currentTimecodeText) currentTimecodeText.text = decoder.CurrentTimecode;
            if (decodedTimecodeText) decodedTimecodeText.text = decoder.DecodedTimecode;
            
            if (statusText)
            {
                statusText.text = decoder.HasSignal ? "RECEIVING" : "NO SIGNAL";
                statusText.color = decoder.HasSignal ? Color.green : Color.yellow;
            }
            
            float signalLevel = decoder.SignalLevel;
            if (signalLevelText) signalLevelText.text = $"{(int)(signalLevel * 100)}%";
            
            if (signalLevelBar)
            {
                RectTransform barRect = signalLevelBar.rectTransform;
                barRect.sizeDelta = new Vector2(150 * signalLevel, barRect.sizeDelta.y);
                signalLevelBar.color = signalLevel > 0.5f ? Color.green : Color.yellow;
            }
        }
    }
}