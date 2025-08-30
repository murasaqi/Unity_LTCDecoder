using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using jp.iridescent.ltcdecoder;

namespace jp.iridescent.ltcdecoder.Editor
{
    /// <summary>
    /// LTCデバッグUI付きセットアップユーティリティ
    /// 固定位置レイアウトバージョン
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
            
            // UI要素作成（固定位置レイアウト）
            GameObject mainPanel = CreateCompleteUI(canvas, ltcObject);
            
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
                // enableDebuggerプロパティを確実に有効化
                var enableDebuggerProp = debuggerSO.FindProperty("enableDebugger");
                if (enableDebuggerProp != null) enableDebuggerProp.boolValue = true;
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
        /// 完全なUI作成（固定位置レイアウト）
        /// </summary>
        private static GameObject CreateCompleteUI(GameObject canvas, GameObject ltcObject)
        {
            // メインパネル作成（380x600）
            GameObject mainPanel = CreateMainPanel(canvas);
            
            // ヘッダー（位置: 中央上部）
            CreateTextAtPosition(mainPanel, "HeaderText", "LTC Decoder Debug UI", 
                new Vector2(190, -30), new Vector2(360, 40), 24, TextAnchor.MiddleCenter, Color.cyan, true);
            
            // Current Timecode
            CreateTextAtPosition(mainPanel, "CurrentTCLabel", "Current TC:", 
                new Vector2(70, -80), new Vector2(100, 30), 14, TextAnchor.MiddleLeft, Color.white, false);
            CreateTextAtPosition(mainPanel, "CurrentTimecodeText", "00:00:00:00", 
                new Vector2(180, -80), new Vector2(180, 30), 18, TextAnchor.MiddleLeft, Color.white, true);
            
            // Decoded Timecode
            CreateTextAtPosition(mainPanel, "DecodedTCLabel", "Decoded TC:", 
                new Vector2(70, -120), new Vector2(100, 30), 14, TextAnchor.MiddleLeft, Color.white, false);
            CreateTextAtPosition(mainPanel, "DecodedTimecodeText", "00:00:00:00", 
                new Vector2(180, -120), new Vector2(180, 30), 16, TextAnchor.MiddleLeft, Color.white, false);
            
            // Status
            CreateTextAtPosition(mainPanel, "StatusLabel", "Status:", 
                new Vector2(70, -160), new Vector2(80, 30), 14, TextAnchor.MiddleLeft, Color.white, false);
            CreateTextAtPosition(mainPanel, "StatusText", "NO SIGNAL", 
                new Vector2(160, -160), new Vector2(150, 30), 16, TextAnchor.MiddleLeft, Color.yellow, false);
            
            // Signal Level
            CreateTextAtPosition(mainPanel, "SignalLabel", "Signal Level:", 
                new Vector2(70, -200), new Vector2(100, 30), 14, TextAnchor.MiddleLeft, Color.white, false);
            CreateSignalBar(mainPanel, new Vector2(180, -200));
            CreateTextAtPosition(mainPanel, "SignalLevelText", "0%", 
                new Vector2(340, -200), new Vector2(40, 30), 14, TextAnchor.MiddleLeft, Color.white, false);
            
            // Control Buttons
            CreateButtonAtPosition(mainPanel, "ClearButton", "Clear", new Vector2(70, -250), new Vector2(80, 35));
            CreateButtonAtPosition(mainPanel, "ExportButton", "Export", new Vector2(160, -250), new Vector2(80, 35));
            CreateButtonAtPosition(mainPanel, "CopyButton", "Copy", new Vector2(250, -250), new Vector2(80, 35));
            
            // Debug Message Area
            CreateDebugScrollView(mainPanel, new Vector2(10, -300), new Vector2(360, 280));
            
            // LTCUIControllerを追加して参照を設定
            SetupUIController(mainPanel, ltcObject);
            
            return mainPanel;
        }
        
        /// <summary>
        /// メインパネル作成
        /// </summary>
        private static GameObject CreateMainPanel(GameObject canvas)
        {
            GameObject mainPanel = new GameObject("Main Panel");
            mainPanel.transform.SetParent(canvas.transform, false);
            
            RectTransform rect = mainPanel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(10, -10);
            rect.sizeDelta = new Vector2(380, 600);
            
            Image panelImage = mainPanel.AddComponent<Image>();
            panelImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            
            return mainPanel;
        }
        
        /// <summary>
        /// 固定位置にテキスト作成
        /// </summary>
        private static GameObject CreateTextAtPosition(GameObject parent, string name, string text, 
            Vector2 position, Vector2 size, int fontSize, TextAnchor alignment, Color color, bool bold)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent.transform, false);
            
            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            
            Text textComp = textObj.AddComponent<Text>();
            textComp.text = text;
            textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComp.fontSize = fontSize;
            textComp.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            textComp.alignment = alignment;
            textComp.color = color;
            
            return textObj;
        }
        
        /// <summary>
        /// 固定位置にボタン作成
        /// </summary>
        private static GameObject CreateButtonAtPosition(GameObject parent, string name, string text, 
            Vector2 position, Vector2 size)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent.transform, false);
            
            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            
            Image image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.3f, 0.3f, 0.3f, 1);
            
            Button button = buttonObj.AddComponent<Button>();
            
            // ボタンテキスト
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;
            
            Text buttonText = textObj.AddComponent<Text>();
            buttonText.text = text;
            buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonText.fontSize = 14;
            buttonText.color = Color.white;
            buttonText.alignment = TextAnchor.MiddleCenter;
            
            return buttonObj;
        }
        
        /// <summary>
        /// シグナルレベルバー作成
        /// </summary>
        private static void CreateSignalBar(GameObject parent, Vector2 position)
        {
            // バー背景
            GameObject barBg = new GameObject("SignalLevelBar");
            barBg.transform.SetParent(parent.transform, false);
            
            RectTransform bgRect = barBg.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 1);
            bgRect.anchorMax = new Vector2(0, 1);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = position;
            bgRect.sizeDelta = new Vector2(150, 20);
            
            Image bgImage = barBg.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1);
            
            // バーフィル
            GameObject barFill = new GameObject("Fill");
            barFill.transform.SetParent(barBg.transform, false);
            
            RectTransform fillRect = barFill.AddComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0, 0.5f);
            fillRect.anchorMax = new Vector2(0, 0.5f);
            fillRect.pivot = new Vector2(0, 0.5f);
            fillRect.anchoredPosition = new Vector2(-75, 0); // 左端から開始
            fillRect.sizeDelta = new Vector2(75, 20);
            
            Image fillImage = barFill.AddComponent<Image>();
            fillImage.color = Color.green;
        }
        
        /// <summary>
        /// デバッグスクロールビュー作成
        /// </summary>
        private static void CreateDebugScrollView(GameObject parent, Vector2 position, Vector2 size)
        {
            // ScrollView
            GameObject scrollView = new GameObject("DebugScrollView");
            scrollView.transform.SetParent(parent.transform, false);
            
            RectTransform scrollRect = scrollView.AddComponent<RectTransform>();
            scrollRect.anchorMin = new Vector2(0, 1);
            scrollRect.anchorMax = new Vector2(0, 1);
            scrollRect.pivot = new Vector2(0, 1);
            scrollRect.anchoredPosition = position;
            scrollRect.sizeDelta = size;
            
            ScrollRect scroll = scrollView.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            
            // Viewport
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
            
            // Content
            GameObject content = new GameObject("DebugMessageContainer");
            content.transform.SetParent(viewport.transform, false);
            
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 0);
            
            // VerticalLayoutGroupは必要最小限に
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
        /// UIコントローラーのセットアップ
        /// </summary>
        private static void SetupUIController(GameObject mainPanel, GameObject ltcObject)
        {
            // LTCUIControllerを追加
            LTCUIController controller = mainPanel.AddComponent<LTCUIController>();
            
            // LTCDecoderとLTCEventDebuggerを設定
            LTCDecoder decoder = ltcObject.GetComponent<LTCDecoder>();
            LTCEventDebugger debugger = ltcObject.GetComponent<LTCEventDebugger>();
            controller.Setup(decoder, debugger);
            
            // UI要素の参照を設定
            controller.currentTimecodeText = mainPanel.transform.Find("CurrentTimecodeText")?.GetComponent<Text>();
            controller.decodedTimecodeText = mainPanel.transform.Find("DecodedTimecodeText")?.GetComponent<Text>();
            controller.statusText = mainPanel.transform.Find("StatusText")?.GetComponent<Text>();
            controller.signalLevelText = mainPanel.transform.Find("SignalLevelText")?.GetComponent<Text>();
            controller.signalLevelBar = mainPanel.transform.Find("SignalLevelBar/Fill")?.GetComponent<Image>();
            controller.debugMessageContainer = mainPanel.transform.Find("DebugScrollView/Viewport/DebugMessageContainer");
            controller.debugScrollRect = mainPanel.transform.Find("DebugScrollView")?.GetComponent<ScrollRect>();
            
            // ボタンのイベント設定
            Button clearButton = mainPanel.transform.Find("ClearButton")?.GetComponent<Button>();
            Button exportButton = mainPanel.transform.Find("ExportButton")?.GetComponent<Button>();
            Button copyButton = mainPanel.transform.Find("CopyButton")?.GetComponent<Button>();
            
            if (clearButton) clearButton.onClick.AddListener(() => 
            {
                controller.ClearDebugMessages();
                debugger?.AddDebugMessage("Debug messages cleared", "SYSTEM");
            });
            
            if (exportButton) exportButton.onClick.AddListener(() => 
            {
                string messages = controller.ExportDebugMessages();
                Debug.Log(messages);
                debugger?.AddDebugMessage($"Exported {controller.activeDebugMessages.Count} messages to console", "EXPORT");
            });
            
            if (copyButton) copyButton.onClick.AddListener(() => 
            {
                controller.CopyDebugMessagesToClipboard();
                debugger?.AddDebugMessage("Debug messages copied to clipboard", "SYSTEM");
            });
            
            UnityEngine.Debug.Log("[LTC Decoder Setup] LTCUIController configured successfully");
        }
    }
}