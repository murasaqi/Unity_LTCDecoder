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
                // LTCDecoderへの参照を設定
                var ltcDecoderProp = debuggerSO.FindProperty("ltcDecoder");
                if (ltcDecoderProp != null) ltcDecoderProp.objectReferenceValue = decoder;
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
            // メインパネル作成（380x700）
            GameObject mainPanel = CreateMainPanel(canvas);
            
            // ヘッダー削除（タイムコードを最上部に大きく表示するため）
            
            // タイムコード表示（最上部に大きく配置）
            // フォーマット: [REC] 01:23:45:12 | ████████░░ 85%
            CreateMonospaceTextAtPosition(mainPanel, "CurrentTimecodeText", "[---] 00:00:00:00 | ░░░░░░░░░░ 000%", 
                new Vector2(190, -50), new Vector2(360, 45), 24, TextAnchor.MiddleCenter, Color.white, false);
            
            // --- Audio Input Settings セクション（中段に配置） ---
            CreateTextAtPosition(mainPanel, "AudioSettingsLabel", "Audio Input Settings", 
                new Vector2(190, -110), new Vector2(360, 25), 16, TextAnchor.MiddleCenter, new Color(0.5f, 0.8f, 1f), true);
            
            // Device Dropdown
            CreateTextAtPosition(mainPanel, "DeviceLabel", "Device:", 
                new Vector2(50, -140), new Vector2(60, 25), 12, TextAnchor.MiddleLeft, Color.white, false);
            CreateDropdownAtPosition(mainPanel, "DeviceDropdown", new Vector2(220, -140), new Vector2(250, 25));
            
            // Frame Rate Dropdown
            CreateTextAtPosition(mainPanel, "FrameRateLabel", "Frame Rate:", 
                new Vector2(60, -170), new Vector2(80, 25), 12, TextAnchor.MiddleLeft, Color.white, false);
            CreateDropdownAtPosition(mainPanel, "FrameRateDropdown", new Vector2(220, -170), new Vector2(250, 25));
            
            // Sample Rate Dropdown
            CreateTextAtPosition(mainPanel, "SampleRateLabel", "Sample Rate:", 
                new Vector2(65, -200), new Vector2(90, 25), 12, TextAnchor.MiddleLeft, Color.white, false);
            CreateDropdownAtPosition(mainPanel, "SampleRateDropdown", new Vector2(220, -200), new Vector2(250, 25));
            
            // Timecode Displayラベル削除（タイムコードは最上部に移動済み）
            
            // 互換性のために残すが非表示（LTCUIControllerが参照する可能性があるため）
            GameObject statusText = CreateTextAtPosition(mainPanel, "StatusText", "", 
                new Vector2(-1000, -1000), new Vector2(1, 1), 1, TextAnchor.MiddleLeft, Color.clear, false);
            statusText.SetActive(false);
            
            GameObject signalLevelText = CreateTextAtPosition(mainPanel, "SignalLevelText", "", 
                new Vector2(-1000, -1000), new Vector2(1, 1), 1, TextAnchor.MiddleLeft, Color.clear, false);
            signalLevelText.SetActive(false);
            
            // シグナルバーも非表示で作成（互換性のため）
            GameObject signalBar = new GameObject("SignalLevelBar");
            signalBar.transform.SetParent(mainPanel.transform, false);
            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(signalBar.transform, false);
            fill.AddComponent<Image>().color = Color.clear;
            signalBar.SetActive(false);
            
            // Control Buttons
            CreateButtonAtPosition(mainPanel, "ClearButton", "Clear", new Vector2(70, -250), new Vector2(80, 35));
            CreateButtonAtPosition(mainPanel, "ExportButton", "Export", new Vector2(160, -250), new Vector2(80, 35));
            CreateButtonAtPosition(mainPanel, "CopyButton", "Copy", new Vector2(250, -250), new Vector2(80, 35));
            
            // Debug Message Area
            CreateDebugScrollView(mainPanel, new Vector2(10, -300), new Vector2(360, 390));
            
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
            rect.sizeDelta = new Vector2(380, 700);
            
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
        /// 固定位置に等幅フォントテキスト作成
        /// </summary>
        private static GameObject CreateMonospaceTextAtPosition(GameObject parent, string name, string text, 
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
            // 等幅フォントを使用（Arialは比較的等幅に近い）
            textComp.font = Font.CreateDynamicFontFromOSFont("Consolas", fontSize);
            if (textComp.font == null)
            {
                textComp.font = Font.CreateDynamicFontFromOSFont("Courier New", fontSize);
            }
            if (textComp.font == null)
            {
                textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            textComp.fontSize = fontSize;
            textComp.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            textComp.alignment = alignment;
            textComp.color = color;
            textComp.horizontalOverflow = HorizontalWrapMode.Overflow;
            
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
        /// 固定位置にドロップダウン作成
        /// </summary>
        private static GameObject CreateDropdownAtPosition(GameObject parent, string name, Vector2 position, Vector2 size)
        {
            GameObject dropdownObj = new GameObject(name);
            dropdownObj.transform.SetParent(parent.transform, false);
            
            RectTransform rect = dropdownObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            
            Image image = dropdownObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 1);
            
            Dropdown dropdown = dropdownObj.AddComponent<Dropdown>();
            
            // Label
            GameObject label = new GameObject("Label");
            label.transform.SetParent(dropdownObj.transform, false);
            
            RectTransform labelRect = label.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(10, 2);
            labelRect.offsetMax = new Vector2(-25, -2);
            
            Text labelText = label.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 12;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleLeft;
            
            // Arrow
            GameObject arrow = new GameObject("Arrow");
            arrow.transform.SetParent(dropdownObj.transform, false);
            
            RectTransform arrowRect = arrow.AddComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1, 0.5f);
            arrowRect.anchorMax = new Vector2(1, 0.5f);
            arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.anchoredPosition = new Vector2(-12, 0);
            arrowRect.sizeDelta = new Vector2(10, 10);
            
            Text arrowText = arrow.AddComponent<Text>();
            arrowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            arrowText.fontSize = 12;
            arrowText.text = "▼";
            arrowText.color = Color.white;
            arrowText.alignment = TextAnchor.MiddleCenter;
            
            // Template (dropdown list)
            GameObject template = new GameObject("Template");
            template.transform.SetParent(dropdownObj.transform, false);
            
            RectTransform templateRect = template.AddComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(0.5f, 1);
            templateRect.anchoredPosition = new Vector2(0, 2);
            templateRect.sizeDelta = new Vector2(0, 150);
            
            ScrollRect scrollRect = template.AddComponent<ScrollRect>();
            
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(template.transform, false);
            
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.anchoredPosition = Vector2.zero;
            
            viewport.AddComponent<Mask>();
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 28);  // 高さを設定して最初のアイテム用のスペースを確保
            
            // Item (template item)
            GameObject item = new GameObject("Item");
            item.transform.SetParent(content.transform, false);
            
            RectTransform itemRect = item.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, 0.5f);
            itemRect.anchorMax = new Vector2(1, 0.5f);
            itemRect.pivot = new Vector2(0.5f, 0.5f);
            itemRect.sizeDelta = new Vector2(0, 20);
            itemRect.anchoredPosition = new Vector2(0, -10);  // 最初のアイテムを下にオフセット
            
            Toggle toggle = item.AddComponent<Toggle>();
            
            // Item Background
            GameObject itemBg = new GameObject("Item Background");
            itemBg.transform.SetParent(item.transform, false);
            
            RectTransform bgRect = itemBg.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            
            Image bgImage = itemBg.AddComponent<Image>();
            bgImage.color = Color.white;
            
            // Item Checkmark
            GameObject checkmark = new GameObject("Item Checkmark");
            checkmark.transform.SetParent(item.transform, false);
            
            RectTransform checkRect = checkmark.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0, 0.5f);
            checkRect.anchorMax = new Vector2(0, 0.5f);
            checkRect.pivot = new Vector2(0.5f, 0.5f);
            checkRect.anchoredPosition = new Vector2(10, 0);
            checkRect.sizeDelta = new Vector2(10, 10);
            
            Image checkImage = checkmark.AddComponent<Image>();
            checkImage.color = Color.white;
            
            // Item Label
            GameObject itemLabel = new GameObject("Item Label");
            itemLabel.transform.SetParent(item.transform, false);
            
            RectTransform itemLabelRect = itemLabel.AddComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.offsetMin = new Vector2(20, 1);
            itemLabelRect.offsetMax = new Vector2(-10, -2);
            
            Text itemText = itemLabel.AddComponent<Text>();
            itemText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            itemText.fontSize = 12;
            itemText.color = Color.black;
            itemText.alignment = TextAnchor.MiddleLeft;
            
            // ドロップダウンの設定
            dropdown.captionText = labelText;
            dropdown.template = templateRect;
            dropdown.itemText = itemText;
            
            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;
            
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            
            template.SetActive(false);
            
            return dropdownObj;
        }
        
        /// <summary>
        /// 固定位置にトグル作成
        /// </summary>
        private static GameObject CreateToggleAtPosition(GameObject parent, string name, Vector2 position, Vector2 size)
        {
            GameObject toggleObj = new GameObject(name);
            toggleObj.transform.SetParent(parent.transform, false);
            
            RectTransform rect = toggleObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            
            Toggle toggle = toggleObj.AddComponent<Toggle>();
            
            // Background
            GameObject background = new GameObject("Background");
            background.transform.SetParent(toggleObj.transform, false);
            
            RectTransform bgRect = background.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0);
            bgRect.anchorMax = new Vector2(1, 1);
            bgRect.sizeDelta = Vector2.zero;
            bgRect.anchoredPosition = Vector2.zero;
            
            Image bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.3f, 0.3f, 0.3f, 1);
            
            // Checkmark
            GameObject checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(background.transform, false);
            
            RectTransform checkRect = checkmark.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.1f, 0.1f);
            checkRect.anchorMax = new Vector2(0.9f, 0.9f);
            checkRect.sizeDelta = Vector2.zero;
            checkRect.anchoredPosition = Vector2.zero;
            
            Image checkImage = checkmark.AddComponent<Image>();
            checkImage.color = Color.green;
            
            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;
            
            return toggleObj;
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
            layout.spacing = 1;  // スペーシングを最小に
            layout.padding = new RectOffset(5, 5, 2, 2);  // 上下パディングを削減
            
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
            
            // UI要素の参照を先に設定（Setupより前に）
            controller.currentTimecodeText = mainPanel.transform.Find("CurrentTimecodeText")?.GetComponent<Text>();
            controller.statusText = mainPanel.transform.Find("StatusText")?.GetComponent<Text>();
            controller.signalLevelText = mainPanel.transform.Find("SignalLevelText")?.GetComponent<Text>();
            controller.signalLevelBar = mainPanel.transform.Find("SignalLevelBar/Fill")?.GetComponent<Image>();
            controller.debugMessageContainer = mainPanel.transform.Find("DebugScrollView/Viewport/DebugMessageContainer");
            controller.debugScrollRect = mainPanel.transform.Find("DebugScrollView")?.GetComponent<ScrollRect>();
            
            // Audio Settings UIの参照を設定
            controller.deviceDropdown = mainPanel.transform.Find("DeviceDropdown")?.GetComponent<Dropdown>();
            controller.frameRateDropdown = mainPanel.transform.Find("FrameRateDropdown")?.GetComponent<Dropdown>();
            controller.sampleRateDropdown = mainPanel.transform.Find("SampleRateDropdown")?.GetComponent<Dropdown>();
            
            // LTCDecoderとLTCEventDebuggerを設定（参照設定後に）
            LTCDecoder decoder = ltcObject.GetComponent<LTCDecoder>();
            LTCEventDebugger debugger = ltcObject.GetComponent<LTCEventDebugger>();
            
            // SerializedObjectで参照を設定
            SerializedObject controllerSO = new SerializedObject(controller);
            controllerSO.FindProperty("ltcDecoder").objectReferenceValue = decoder;
            controllerSO.FindProperty("ltcEventDebugger").objectReferenceValue = debugger;
            controllerSO.ApplyModifiedProperties();
            
            // Setupを呼び出し
            controller.Setup(decoder, debugger);
            
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
            
            // セットアップ完了
        }
    }
}