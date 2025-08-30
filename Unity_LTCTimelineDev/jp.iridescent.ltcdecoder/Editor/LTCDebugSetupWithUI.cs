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
            
            // UI要素作成（LayoutGroupベース）
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
        /// 完全なUI作成（LayoutGroupベース）
        /// </summary>
        private static GameObject CreateCompleteUI(GameObject canvas, GameObject ltcObject)
        {
            // メインパネル作成
            GameObject mainPanel = CreateMainPanel(canvas);
            
            // VerticalLayoutGroupを追加
            VerticalLayoutGroup mainLayout = mainPanel.AddComponent<VerticalLayoutGroup>();
            mainLayout.childAlignment = TextAnchor.UpperCenter;
            mainLayout.childControlHeight = false;
            mainLayout.childControlWidth = true;
            mainLayout.childForceExpandHeight = false;
            mainLayout.childForceExpandWidth = true;
            mainLayout.spacing = 10;
            mainLayout.padding = new RectOffset(10, 10, 10, 10);
            
            // 各セクションを作成
            CreateHeaderSection(mainPanel);
            CreateTimecodeSection(mainPanel);
            CreateStatusSection(mainPanel);
            CreateControlSection(mainPanel);
            CreateDebugMessageSection(mainPanel);
            
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
        /// ヘッダーセクション作成
        /// </summary>
        private static void CreateHeaderSection(GameObject parent)
        {
            GameObject headerSection = new GameObject("HeaderSection");
            headerSection.transform.SetParent(parent.transform, false);
            
            // LayoutElementで高さ設定
            LayoutElement layoutElement = headerSection.AddComponent<LayoutElement>();
            layoutElement.minHeight = 40;
            layoutElement.preferredHeight = 40;
            
            // テキスト
            Text headerText = headerSection.AddComponent<Text>();
            headerText.text = "LTC Decoder Debug UI";
            headerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            headerText.fontSize = 24;
            headerText.fontStyle = FontStyle.Bold;
            headerText.color = Color.cyan;
            headerText.alignment = TextAnchor.MiddleCenter;
        }
        
        /// <summary>
        /// タイムコードセクション作成
        /// </summary>
        private static void CreateTimecodeSection(GameObject parent)
        {
            GameObject section = CreateHorizontalSection(parent, "TimecodeSection", 30);
            
            // Current Timecode
            CreateLabelValuePair(section, "Current TC:", "CurrentTimecodeText", "00:00:00:00", 120, true);
            
            // セパレータ用のスペース
            CreateSpacer(parent, 5);
            
            // Decoded Timecode
            GameObject decodedSection = CreateHorizontalSection(parent, "DecodedSection", 30);
            CreateLabelValuePair(decodedSection, "Decoded TC:", "DecodedTimecodeText", "00:00:00:00", 120, false);
        }
        
        /// <summary>
        /// ステータスセクション作成
        /// </summary>
        private static void CreateStatusSection(GameObject parent)
        {
            // ステータス行
            GameObject statusSection = CreateHorizontalSection(parent, "StatusSection", 30);
            CreateLabelValuePair(statusSection, "Status:", "StatusText", "NO SIGNAL", 80, false);
            
            // シグナルレベル行
            GameObject signalSection = CreateHorizontalSection(parent, "SignalSection", 30);
            
            // ラベル
            GameObject label = CreateLabel(signalSection, "Signal Level:", 100);
            
            // シグナルレベルバー背景
            GameObject barBg = new GameObject("SignalLevelBar");
            barBg.transform.SetParent(signalSection.transform, false);
            
            LayoutElement barBgLayout = barBg.AddComponent<LayoutElement>();
            barBgLayout.preferredWidth = 150;
            barBgLayout.minHeight = 20;
            
            Image bgImage = barBg.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1);
            
            // バーフィル
            GameObject barFill = new GameObject("Fill");
            barFill.transform.SetParent(barBg.transform, false);
            
            RectTransform fillRect = barFill.AddComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0, 0);
            fillRect.anchorMax = new Vector2(0, 1);
            fillRect.pivot = new Vector2(0, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(75, 20); // 初期サイズを設定
            
            Image fillImage = barFill.AddComponent<Image>();
            fillImage.color = Color.green;
            
            // パーセンテージテキスト
            GameObject percentText = CreateValue(signalSection, "SignalLevelText", "0%", 50, false);
        }
        
        /// <summary>
        /// コントロールセクション作成
        /// </summary>
        private static void CreateControlSection(GameObject parent)
        {
            GameObject section = CreateHorizontalSection(parent, "ControlSection", 40);
            
            // ボタン間のスペースを均等に配分
            HorizontalLayoutGroup layout = section.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleCenter;
            
            CreateButton(section, "ClearButton", "Clear", 80);
            CreateButton(section, "ExportButton", "Export", 80);
            CreateButton(section, "CopyButton", "Copy", 80);
        }
        
        /// <summary>
        /// デバッグメッセージセクション作成
        /// </summary>
        private static void CreateDebugMessageSection(GameObject parent)
        {
            // ScrollView作成
            GameObject scrollView = new GameObject("DebugScrollView");
            scrollView.transform.SetParent(parent.transform, false);
            
            // RectTransformを明示的に設定
            RectTransform scrollRectTransform = scrollView.GetComponent<RectTransform>();
            if (scrollRectTransform == null)
                scrollRectTransform = scrollView.AddComponent<RectTransform>();
            scrollRectTransform.anchorMin = Vector2.zero;
            scrollRectTransform.anchorMax = Vector2.one;
            scrollRectTransform.sizeDelta = Vector2.zero;
            scrollRectTransform.anchoredPosition = Vector2.zero;
            
            LayoutElement scrollLayout = scrollView.AddComponent<LayoutElement>();
            scrollLayout.flexibleHeight = 1; // 残りの高さを全て使う
            scrollLayout.minHeight = 200;
            
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
            
            VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlHeight = false;
            contentLayout.childControlWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.childForceExpandWidth = true;
            contentLayout.spacing = 2;
            contentLayout.padding = new RectOffset(5, 5, 5, 5);
            
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
            Transform currentTCText = mainPanel.transform.Find("TimecodeSection/CurrentTimecodeText");
            Transform decodedTCText = mainPanel.transform.Find("DecodedSection/DecodedTimecodeText");
            Transform statusText = mainPanel.transform.Find("StatusSection/StatusText");
            Transform signalLevelText = mainPanel.transform.Find("SignalSection/SignalLevelText");
            Transform signalLevelBar = mainPanel.transform.Find("SignalSection/SignalLevelBar/Fill");
            Transform debugContainer = mainPanel.transform.Find("DebugScrollView/Viewport/DebugMessageContainer");
            Transform debugScrollView = mainPanel.transform.Find("DebugScrollView");
            
            controller.currentTimecodeText = currentTCText?.GetComponent<Text>();
            controller.decodedTimecodeText = decodedTCText?.GetComponent<Text>();
            controller.statusText = statusText?.GetComponent<Text>();
            controller.signalLevelText = signalLevelText?.GetComponent<Text>();
            controller.signalLevelBar = signalLevelBar?.GetComponent<Image>();
            controller.debugMessageContainer = debugContainer;
            controller.debugScrollRect = debugScrollView?.GetComponent<ScrollRect>();
            
            // ボタンのイベント設定
            Button clearButton = mainPanel.transform.Find("ControlSection/ClearButton")?.GetComponent<Button>();
            Button exportButton = mainPanel.transform.Find("ControlSection/ExportButton")?.GetComponent<Button>();
            Button copyButton = mainPanel.transform.Find("ControlSection/CopyButton")?.GetComponent<Button>();
            
            if (clearButton) clearButton.onClick.AddListener(() => controller.ClearDebugMessages());
            if (exportButton) exportButton.onClick.AddListener(() => Debug.Log(controller.ExportDebugMessages()));
            if (copyButton) copyButton.onClick.AddListener(() => controller.CopyDebugMessagesToClipboard());
            
            UnityEngine.Debug.Log("[LTC Decoder Setup] LTCUIController configured successfully");
        }
        
        // ヘルパーメソッド
        
        /// <summary>
        /// 水平セクション作成
        /// </summary>
        private static GameObject CreateHorizontalSection(GameObject parent, string name, float height)
        {
            GameObject section = new GameObject(name);
            section.transform.SetParent(parent.transform, false);
            
            LayoutElement layout = section.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
            
            HorizontalLayoutGroup horizontalLayout = section.AddComponent<HorizontalLayoutGroup>();
            horizontalLayout.childAlignment = TextAnchor.MiddleLeft;
            horizontalLayout.childControlHeight = false;
            horizontalLayout.childControlWidth = false;
            horizontalLayout.childForceExpandHeight = false;
            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.spacing = 10;
            
            return section;
        }
        
        /// <summary>
        /// ラベルと値のペア作成
        /// </summary>
        private static void CreateLabelValuePair(GameObject parent, string labelText, string valueName, 
            string defaultValue, float labelWidth, bool bold)
        {
            CreateLabel(parent, labelText, labelWidth);
            CreateValue(parent, valueName, defaultValue, 0, bold);
        }
        
        /// <summary>
        /// ラベル作成
        /// </summary>
        private static GameObject CreateLabel(GameObject parent, string text, float width)
        {
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(parent.transform, false);
            
            LayoutElement layout = labelObj.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            
            Text labelText = labelObj.AddComponent<Text>();
            labelText.text = text;
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 14;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleLeft;
            
            return labelObj;
        }
        
        /// <summary>
        /// 値テキスト作成
        /// </summary>
        private static GameObject CreateValue(GameObject parent, string name, string defaultText, 
            float width, bool bold)
        {
            GameObject valueObj = new GameObject(name);
            valueObj.transform.SetParent(parent.transform, false);
            
            LayoutElement layout = valueObj.AddComponent<LayoutElement>();
            if (width > 0)
            {
                layout.minWidth = width;
                layout.preferredWidth = width;
            }
            else
            {
                layout.flexibleWidth = 1;
            }
            
            Text valueText = valueObj.AddComponent<Text>();
            valueText.text = defaultText;
            valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            valueText.fontSize = bold ? 18 : 14;
            valueText.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            valueText.color = Color.white;
            valueText.alignment = TextAnchor.MiddleLeft;
            
            return valueObj;
        }
        
        /// <summary>
        /// ボタン作成
        /// </summary>
        private static GameObject CreateButton(GameObject parent, string name, string text, float width)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent.transform, false);
            
            LayoutElement layout = buttonObj.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.minHeight = 35;
            layout.preferredHeight = 35;
            
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
        /// スペーサー作成
        /// </summary>
        private static void CreateSpacer(GameObject parent, float height)
        {
            GameObject spacer = new GameObject("Spacer");
            spacer.transform.SetParent(parent.transform, false);
            
            LayoutElement layout = spacer.AddComponent<LayoutElement>();
            layout.minHeight = height;
            layout.preferredHeight = height;
        }
    }
}