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
        /// 完全なUI作成（新レイアウト構造）
        /// </summary>
        private static GameObject CreateCompleteUI(GameObject canvas, GameObject ltcObject)
        {
            // メインパネル作成（自動レイアウト）
            GameObject mainPanel = CreateMainPanel(canvas);
            
            // ヘッダー（ツールバー）作成
            GameObject header = CreateHeader(mainPanel);
            
            // セパレータライン
            CreateSeparator(mainPanel);
            
            // ScrollRect/Viewport/Content構造を作成
            GameObject content = CreateScrollableContent(mainPanel);
            
            // --- Content内に各要素を追加 ---
            
            // タイムコードセクション
            CreateSectionTitle(content, "LTC Timecode");
            GameObject timecodeText = CreateContentText(content, "CurrentTimecodeText", "00:00:00:00", 24, true, true);
            GameObject statusLine = CreateContentText(content, "StatusLineText", "Output TC: Stopped ░░░░░░░░░░ 000%", 14, false, false);
            
            // セパレータ
            CreateContentSeparator(content);
            
            // Audio Input Settings セクション
            CreateSectionTitle(content, "Audio Input Settings");
            
            // Device Dropdown行
            GameObject deviceRow = CreateHorizontalGroup(content, "DeviceRow");
            CreateLabelInRow(deviceRow, "Device:", 80);
            CreateDropdownInRow(deviceRow, "DeviceDropdown", 200);
            
            // Frame Rate Dropdown行
            GameObject frameRateRow = CreateHorizontalGroup(content, "FrameRateRow");
            CreateLabelInRow(frameRateRow, "Frame Rate:", 80);
            CreateDropdownInRow(frameRateRow, "FrameRateDropdown", 200);
            
            // Sample Rate Dropdown行
            GameObject sampleRateRow = CreateHorizontalGroup(content, "SampleRateRow");
            CreateLabelInRow(sampleRateRow, "Sample Rate:", 80);
            CreateDropdownInRow(sampleRateRow, "SampleRateDropdown", 200);
            
            // セパレータ
            CreateContentSeparator(content);
            
            // Timeline Sync Section（初期非表示、実行時に自動表示）
            GameObject timelineSyncSection = CreateTimelineSyncSectionNew(content);
            timelineSyncSection.SetActive(false);
            
            // セパレータ
            CreateContentSeparator(content);
            
            // Debug Messages セクション
            CreateSectionTitle(content, "Debug Messages");
            GameObject debugContainer = CreateDebugMessageContainer(content);
            
            // セパレータ
            CreateContentSeparator(content);
            
            // ボタンセクション（Debug Messageの下に配置）
            GameObject buttonSection = CreateButtonSection(content);
            
            // 互換性のために非表示要素を作成（LTCUIControllerが参照する可能性があるため）
            CreateHiddenCompatibilityElements(mainPanel);
            
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
            // 画面右側に固定、縦方向は上下にストレッチ
            rect.anchorMin = new Vector2(1, 0);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-10, -10);
            
            // 横幅をビュー幅の30%で計算（最小320px、最大560px）
            float viewWidth = Screen.width;
            #if UNITY_EDITOR
            if (UnityEditor.SceneView.lastActiveSceneView != null)
            {
                viewWidth = UnityEditor.SceneView.lastActiveSceneView.position.width;
            }
            #endif
            float panelWidth = Mathf.Clamp(viewWidth * 0.3f, 320f, 560f);
            rect.sizeDelta = new Vector2(panelWidth, 0);  // 縦はアンカーで追従
            
            Image panelImage = mainPanel.AddComponent<Image>();
            panelImage.color = new Color(25f/255f, 25f/255f, 25f/255f, 0.92f);  // セミトーン背景
            
            // VerticalLayoutGroupを追加
            VerticalLayoutGroup vlg = mainPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.spacing = 6;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childScaleWidth = false;
            vlg.childScaleHeight = false;
            
            return mainPanel;
        }
        
        /// <summary>
        /// シンプルなヘッダー作成（タイトルのみ）
        /// </summary>
        private static GameObject CreateHeader(GameObject parent)
        {
            GameObject header = new GameObject("Header");
            header.transform.SetParent(parent.transform, false);
            
            // LayoutElementで高さを固定（コンパクトに）
            LayoutElement headerLayout = header.AddComponent<LayoutElement>();
            headerLayout.preferredHeight = 25;
            headerLayout.minHeight = 25;
            
            // タイトルテキスト（センター配置）
            Text titleText = header.AddComponent<Text>();
            titleText.text = "LTC Debug UI";
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 16;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(0.5f, 0.8f, 1f);
            titleText.alignment = TextAnchor.MiddleCenter;
            
            return header;
        }
        
        /// <summary>
        /// ボタンセクション作成（Debug Messageの下に配置）
        /// </summary>
        private static GameObject CreateButtonSection(GameObject parent)
        {
            GameObject buttonSection = new GameObject("ButtonSection");
            buttonSection.transform.SetParent(parent.transform, false);
            
            // HorizontalLayoutGroupで横並び配置
            HorizontalLayoutGroup hlg = buttonSection.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 5;
            hlg.padding = new RectOffset(10, 10, 5, 5);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            
            // LayoutElementで高さを固定
            LayoutElement sectionLayout = buttonSection.AddComponent<LayoutElement>();
            sectionLayout.preferredHeight = 35;
            sectionLayout.minHeight = 35;
            
            // Flexible Space（左側）
            GameObject leftSpacer = new GameObject("LeftSpacer");
            leftSpacer.transform.SetParent(buttonSection.transform, false);
            LayoutElement leftSpacerLayout = leftSpacer.AddComponent<LayoutElement>();
            leftSpacerLayout.flexibleWidth = 1;
            
            // Clear Button
            CreateActionButton(buttonSection, "ClearButton", "Clear");
            
            // Export Button
            CreateActionButton(buttonSection, "ExportButton", "Export");
            
            // Copy Button
            CreateActionButton(buttonSection, "CopyButton", "Copy");
            
            // Flexible Space（右側）
            GameObject rightSpacer = new GameObject("RightSpacer");
            rightSpacer.transform.SetParent(buttonSection.transform, false);
            LayoutElement rightSpacerLayout = rightSpacer.AddComponent<LayoutElement>();
            rightSpacerLayout.flexibleWidth = 1;
            
            return buttonSection;
        }
        
        /// <summary>
        /// アクションボタン作成
        /// </summary>
        private static GameObject CreateActionButton(GameObject parent, string name, string text)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent.transform, false);
            
            // LayoutElementで幅と高さを設定
            LayoutElement layout = buttonObj.AddComponent<LayoutElement>();
            layout.preferredWidth = 80;
            layout.preferredHeight = 28;
            layout.minWidth = 70;
            layout.minHeight = 28;
            
            // ボタン背景
            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            // ボタンコンポーネント
            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = buttonImage;
            
            // ボタンテキスト
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.offsetMin = new Vector2(8, 0);  // パディング
            textRect.offsetMax = new Vector2(-8, 0);
            
            Text buttonText = textObj.AddComponent<Text>();
            buttonText.text = text;
            buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            buttonText.fontSize = 14;
            buttonText.color = Color.white;
            buttonText.alignment = TextAnchor.MiddleCenter;
            
            return buttonObj;
        }
        
        /// <summary>
        /// セパレータライン作成
        /// </summary>
        private static GameObject CreateSeparator(GameObject parent)
        {
            GameObject separator = new GameObject("Separator");
            separator.transform.SetParent(parent.transform, false);
            
            LayoutElement layout = separator.AddComponent<LayoutElement>();
            layout.preferredHeight = 2;
            layout.minHeight = 2;
            
            Image image = separator.AddComponent<Image>();
            image.color = new Color(128f/255f, 128f/255f, 128f/255f, 0.5f);
            
            return separator;
        }
        
        /// <summary>
        /// ScrollRect/Viewport/Content構造を作成
        /// </summary>
        private static GameObject CreateScrollableContent(GameObject parent)
        {
            // ScrollRect作成
            GameObject scrollRectObj = new GameObject("ScrollRect");
            scrollRectObj.transform.SetParent(parent.transform, false);
            
            // LayoutElementで残りの高さを占有
            LayoutElement scrollLayout = scrollRectObj.AddComponent<LayoutElement>();
            scrollLayout.flexibleHeight = 1;
            scrollLayout.flexibleWidth = 1;
            
            // ScrollRectコンポーネント
            ScrollRect scrollRect = scrollRectObj.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scrollRect.scrollSensitivity = 20;
            
            // Viewport作成
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollRectObj.transform, false);
            
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            
            // ViewportにMaskとImage（透明）を追加
            viewport.AddComponent<RectMask2D>();
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(0, 0, 0, 0);  // 透明
            
            // Content作成
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 0);
            
            // ContentにVerticalLayoutGroupを追加
            VerticalLayoutGroup contentVLG = content.AddComponent<VerticalLayoutGroup>();
            contentVLG.padding = new RectOffset(10, 10, 10, 10);
            contentVLG.spacing = 5;
            contentVLG.childForceExpandWidth = true;
            contentVLG.childForceExpandHeight = false;
            contentVLG.childControlWidth = true;
            contentVLG.childControlHeight = true;
            
            // ContentSizeFitterを追加
            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            
            // ScrollRectに参照を設定
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;
            
            // 垂直スクロールバー作成（オプション）
            GameObject scrollbarObj = new GameObject("Scrollbar Vertical");
            scrollbarObj.transform.SetParent(scrollRectObj.transform, false);
            
            RectTransform scrollbarRect = scrollbarObj.AddComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1, 0);
            scrollbarRect.anchorMax = new Vector2(1, 1);
            scrollbarRect.pivot = new Vector2(1, 0.5f);
            scrollbarRect.anchoredPosition = new Vector2(0, 0);
            scrollbarRect.sizeDelta = new Vector2(10, 0);
            
            Image scrollbarBg = scrollbarObj.AddComponent<Image>();
            scrollbarBg.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            
            Scrollbar scrollbar = scrollbarObj.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            
            // スクロールバーのハンドル
            GameObject handleObj = new GameObject("Handle");
            handleObj.transform.SetParent(scrollbarObj.transform, false);
            
            RectTransform handleRect = handleObj.AddComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.sizeDelta = new Vector2(-4, -4);
            handleRect.anchoredPosition = Vector2.zero;
            
            Image handleImage = handleObj.AddComponent<Image>();
            handleImage.color = new Color(0.4f, 0.4f, 0.4f, 0.8f);
            
            scrollbar.handleRect = handleRect;
            scrollbar.targetGraphic = handleImage;
            
            // ScrollRectにスクロールバーを設定
            scrollRect.verticalScrollbar = scrollbar;
            
            return content;  // Contentを返す（ここに要素を追加する）
        }
        
        /// <summary>
        /// Contentにセクションタイトルを作成
        /// </summary>
        private static GameObject CreateSectionTitle(GameObject parent, string title)
        {
            GameObject titleObj = new GameObject("SectionTitle_" + title);
            titleObj.transform.SetParent(parent.transform, false);
            
            Text text = titleObj.AddComponent<Text>();
            text.text = title;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 16;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(0.5f, 0.8f, 1f);
            text.alignment = TextAnchor.MiddleLeft;
            
            LayoutElement layout = titleObj.AddComponent<LayoutElement>();
            layout.preferredHeight = 24;
            layout.minHeight = 24;
            
            return titleObj;
        }
        
        /// <summary>
        /// Contentに通常テキストを作成
        /// </summary>
        private static GameObject CreateContentText(GameObject parent, string name, string text, int fontSize, bool bold, bool monospace)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent.transform, false);
            
            Text textComp = textObj.AddComponent<Text>();
            textComp.text = text;
            
            if (monospace)
            {
                textComp.font = Font.CreateDynamicFontFromOSFont("Consolas", fontSize);
                if (textComp.font == null)
                {
                    textComp.font = Font.CreateDynamicFontFromOSFont("Courier New", fontSize);
                }
                if (textComp.font == null)
                {
                    textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
            }
            else
            {
                textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            
            textComp.fontSize = fontSize;
            textComp.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            textComp.color = Color.white;
            textComp.alignment = TextAnchor.MiddleCenter;
            textComp.horizontalOverflow = HorizontalWrapMode.Wrap;
            textComp.verticalOverflow = VerticalWrapMode.Truncate;
            
            LayoutElement layout = textObj.AddComponent<LayoutElement>();
            layout.minHeight = fontSize + 8;
            layout.preferredHeight = fontSize + 8;
            
            return textObj;
        }
        
        /// <summary>
        /// Content内にセパレータを作成
        /// </summary>
        private static GameObject CreateContentSeparator(GameObject parent)
        {
            GameObject separator = new GameObject("ContentSeparator");
            separator.transform.SetParent(parent.transform, false);
            
            LayoutElement layout = separator.AddComponent<LayoutElement>();
            layout.preferredHeight = 1;
            layout.minHeight = 1;
            
            Image image = separator.AddComponent<Image>();
            image.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
            
            return separator;
        }
        
        /// <summary>
        /// 水平レイアウトグループを作成
        /// </summary>
        private static GameObject CreateHorizontalGroup(GameObject parent, string name)
        {
            GameObject group = new GameObject(name);
            group.transform.SetParent(parent.transform, false);
            
            HorizontalLayoutGroup hlg = group.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 5;
            hlg.padding = new RectOffset(0, 0, 2, 2);
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            
            LayoutElement layout = group.AddComponent<LayoutElement>();
            layout.preferredHeight = 28;
            layout.minHeight = 28;
            
            return group;
        }
        
        /// <summary>
        /// 行内にラベルを作成
        /// </summary>
        private static GameObject CreateLabelInRow(GameObject parent, string text, float width)
        {
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(parent.transform, false);
            
            Text label = labelObj.AddComponent<Text>();
            label.text = text;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 14;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleLeft;
            
            LayoutElement layout = labelObj.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.minWidth = width;
            
            return labelObj;
        }
        
        /// <summary>
        /// 行内にドロップダウンを作成
        /// </summary>
        private static GameObject CreateDropdownInRow(GameObject parent, string name, float width)
        {
            GameObject dropdownObj = new GameObject(name);
            dropdownObj.transform.SetParent(parent.transform, false);
            
            Image bgImage = dropdownObj.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            Dropdown dropdown = dropdownObj.AddComponent<Dropdown>();
            
            // テンプレートを作成
            GameObject template = new GameObject("Template");
            template.transform.SetParent(dropdownObj.transform, false);
            
            RectTransform templateRect = template.AddComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(0.5f, 1);
            templateRect.anchoredPosition = new Vector2(0, 2);
            templateRect.sizeDelta = new Vector2(0, 150);
            
            template.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            template.AddComponent<ScrollRect>();
            
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(template.transform, false);
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            viewport.AddComponent<Image>().color = Color.white;
            
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 28);
            
            GameObject item = new GameObject("Item");
            item.transform.SetParent(content.transform, false);
            
            RectTransform itemRect = item.AddComponent<RectTransform>();
            itemRect.anchorMin = new Vector2(0, 0.5f);
            itemRect.anchorMax = new Vector2(1, 0.5f);
            itemRect.sizeDelta = new Vector2(0, 20);
            
            Toggle itemToggle = item.AddComponent<Toggle>();
            itemToggle.targetGraphic = item.AddComponent<Image>();
            itemToggle.targetGraphic.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            GameObject itemLabel = new GameObject("Item Label");
            itemLabel.transform.SetParent(item.transform, false);
            
            RectTransform itemLabelRect = itemLabel.AddComponent<RectTransform>();
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.sizeDelta = new Vector2(-20, -2);
            itemLabelRect.anchoredPosition = new Vector2(10, 0);
            
            Text itemText = itemLabel.AddComponent<Text>();
            itemText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            itemText.fontSize = 12;
            itemText.color = Color.white;
            itemText.alignment = TextAnchor.MiddleLeft;
            
            dropdown.template = templateRect;
            dropdown.itemText = itemText;
            
            // Label (selected value display)
            GameObject label = new GameObject("Label");
            label.transform.SetParent(dropdownObj.transform, false);
            
            RectTransform labelRect = label.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.sizeDelta = new Vector2(-30, 0);
            labelRect.anchoredPosition = new Vector2(-5, 0);
            
            Text labelText = label.AddComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 12;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleLeft;
            
            dropdown.captionText = labelText;
            
            // Arrow
            GameObject arrow = new GameObject("Arrow");
            arrow.transform.SetParent(dropdownObj.transform, false);
            
            RectTransform arrowRect = arrow.AddComponent<RectTransform>();
            arrowRect.anchorMin = new Vector2(1, 0.5f);
            arrowRect.anchorMax = new Vector2(1, 0.5f);
            arrowRect.sizeDelta = new Vector2(20, 20);
            arrowRect.anchoredPosition = new Vector2(-10, 0);
            
            Text arrowText = arrow.AddComponent<Text>();
            arrowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            arrowText.text = "▼";
            arrowText.fontSize = 10;
            arrowText.color = Color.white;
            arrowText.alignment = TextAnchor.MiddleCenter;
            
            template.SetActive(false);
            
            LayoutElement layout = dropdownObj.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.minWidth = width;
            layout.preferredHeight = 24;
            
            return dropdownObj;
        }
        
        /// <summary>
        /// Timeline Syncセクション作成（新レイアウト版）
        /// </summary>
        private static GameObject CreateTimelineSyncSectionNew(GameObject parent)
        {
            GameObject section = new GameObject("TimelineSyncSection");
            section.transform.SetParent(parent.transform, false);
            
            VerticalLayoutGroup vlg = section.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 3;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            
            // セクションタイトル
            CreateSectionTitle(section, "Timeline Sync");
            
            // LTC TC行
            GameObject ltcRow = CreateContentText(section, "TimelineSyncLTC", "LTC:      --:--:--:--", 14, false, true);
            
            // Timeline TC行
            GameObject timelineRow = CreateContentText(section, "TimelineSyncTimeline", "Timeline: --:--:--:--", 14, false, true);
            
            // Diff行
            GameObject diffRow = CreateContentText(section, "TimelineSyncDiff", "Diff: 0.000s [Waiting]", 14, false, false);
            
            // Status行
            GameObject statusRow = CreateContentText(section, "TimelineSyncStatus", "Status: Stopped", 14, false, false);
            
            // Threshold行
            GameObject thresholdRow = CreateContentText(section, "TimelineSyncThreshold", "Threshold: 0.10s | Offset: 0.00s", 14, false, false);
            
            // Sync Settings サブセクション
            CreateSectionTitle(section, "Sync Settings");
            
            // Sync Threshold行
            GameObject syncThresholdRow = CreateHorizontalGroup(section, "SyncThresholdRow");
            CreateLabelInRow(syncThresholdRow, "Sync Threshold:", 120);
            CreateInputFieldInRow(syncThresholdRow, "SyncThresholdInput", "0.033", 80);
            
            // Observation Time行
            GameObject observationRow = CreateHorizontalGroup(section, "ObservationTimeRow");
            CreateLabelInRow(observationRow, "Observation Time:", 120);
            CreateInputFieldInRow(observationRow, "ObservationTimeInput", "0.1", 80);
            
            // Timeline Offset行
            GameObject offsetRow = CreateHorizontalGroup(section, "TimelineOffsetRow");
            CreateLabelInRow(offsetRow, "Timeline Offset:", 120);
            CreateInputFieldInRow(offsetRow, "TimelineOffsetInput", "0.0", 80);
            
            // Enable Syncトグル行
            GameObject enableSyncRow = CreateHorizontalGroup(section, "EnableSyncRow");
            CreateToggleInRow(enableSyncRow, "EnableSyncToggle", "Enable Sync", true);
            
            // Snap to FPSトグル行
            GameObject snapToFpsRow = CreateHorizontalGroup(section, "SnapToFpsRow");
            CreateToggleInRow(snapToFpsRow, "SnapToFpsToggle", "Snap to Timeline FPS", true);
            
            return section;
        }
        
        /// <summary>
        /// 行内に入力フィールドを作成
        /// </summary>
        private static GameObject CreateInputFieldInRow(GameObject parent, string name, string defaultText, float width)
        {
            GameObject inputObj = new GameObject(name);
            inputObj.transform.SetParent(parent.transform, false);
            
            Image bgImage = inputObj.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            
            InputField inputField = inputObj.AddComponent<InputField>();
            
            // Text Component
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(inputObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = new Vector2(-10, 0);
            textRect.anchoredPosition = new Vector2(5, 0);
            
            Text text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 12;
            text.color = Color.white;
            text.supportRichText = false;
            text.alignment = TextAnchor.MiddleLeft;
            
            inputField.textComponent = text;
            inputField.text = defaultText;
            
            // Placeholder
            GameObject placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(inputObj.transform, false);
            
            RectTransform placeholderRect = placeholderObj.AddComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.sizeDelta = new Vector2(-10, 0);
            placeholderRect.anchoredPosition = new Vector2(5, 0);
            
            Text placeholderText = placeholderObj.AddComponent<Text>();
            placeholderText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            placeholderText.fontSize = 12;
            placeholderText.fontStyle = FontStyle.Italic;
            placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            placeholderText.text = "Enter value...";
            placeholderText.alignment = TextAnchor.MiddleLeft;
            
            inputField.placeholder = placeholderText;
            
            LayoutElement layout = inputObj.AddComponent<LayoutElement>();
            layout.preferredWidth = width;
            layout.minWidth = width;
            layout.preferredHeight = 20;
            
            return inputObj;
        }
        
        /// <summary>
        /// 行内にトグルを作成
        /// </summary>
        private static GameObject CreateToggleInRow(GameObject parent, string name, string label, bool defaultValue)
        {
            GameObject toggleObj = new GameObject(name);
            toggleObj.transform.SetParent(parent.transform, false);
            
            Toggle toggle = toggleObj.AddComponent<Toggle>();
            toggle.isOn = defaultValue;
            
            // Background
            GameObject background = new GameObject("Background");
            background.transform.SetParent(toggleObj.transform, false);
            
            RectTransform bgRect = background.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.5f);
            bgRect.anchorMax = new Vector2(0, 0.5f);
            bgRect.sizeDelta = new Vector2(20, 20);
            bgRect.anchoredPosition = new Vector2(10, 0);
            
            Image bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            // Checkmark
            GameObject checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(background.transform, false);
            
            RectTransform checkRect = checkmark.AddComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.sizeDelta = new Vector2(-4, -4);
            checkRect.anchoredPosition = Vector2.zero;
            
            Image checkImage = checkmark.AddComponent<Image>();
            checkImage.color = new Color(0.4f, 0.8f, 0.4f, 1f);
            
            toggle.targetGraphic = bgImage;
            toggle.graphic = checkImage;
            
            // Label
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(toggleObj.transform, false);
            
            RectTransform labelRect = labelObj.AddComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0, 0.5f);
            labelRect.anchorMax = new Vector2(1, 0.5f);
            labelRect.sizeDelta = new Vector2(-35, 20);
            labelRect.anchoredPosition = new Vector2(35, 0);
            
            Text labelText = labelObj.AddComponent<Text>();
            labelText.text = label;
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 14;
            labelText.color = Color.white;
            labelText.alignment = TextAnchor.MiddleLeft;
            
            LayoutElement layout = toggleObj.AddComponent<LayoutElement>();
            layout.preferredHeight = 24;
            layout.flexibleWidth = 1;
            
            return toggleObj;
        }
        
        /// <summary>
        /// デバッグメッセージコンテナを作成
        /// </summary>
        private static GameObject CreateDebugMessageContainer(GameObject parent)
        {
            GameObject container = new GameObject("DebugMessageContainer");
            container.transform.SetParent(parent.transform, false);
            
            VerticalLayoutGroup vlg = container.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 2;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            
            LayoutElement layout = container.AddComponent<LayoutElement>();
            layout.minHeight = 100;
            layout.flexibleHeight = 1;
            
            return container;
        }
        
        /// <summary>
        /// 互換性のための非表示要素を作成
        /// </summary>
        private static void CreateHiddenCompatibilityElements(GameObject parent)
        {
            // StatusText（非表示）
            GameObject statusText = new GameObject("StatusText");
            statusText.transform.SetParent(parent.transform, false);
            statusText.AddComponent<Text>();
            statusText.SetActive(false);
            
            // SignalLevelText（非表示）
            GameObject signalLevelText = new GameObject("SignalLevelText");
            signalLevelText.transform.SetParent(parent.transform, false);
            signalLevelText.AddComponent<Text>();
            signalLevelText.SetActive(false);
            
            // SignalLevelBar（非表示）
            GameObject signalBar = new GameObject("SignalLevelBar");
            signalBar.transform.SetParent(parent.transform, false);
            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(signalBar.transform, false);
            fill.AddComponent<Image>();
            signalBar.SetActive(false);
            
            // HeaderText（非表示）- 新レイアウトではHeader内のTitleに移動
            GameObject headerText = new GameObject("HeaderText");
            headerText.transform.SetParent(parent.transform, false);
            headerText.AddComponent<Text>();
            headerText.SetActive(false);
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
        /// Timeline Syncセクション作成
        /// </summary>
        private static GameObject CreateTimelineSyncSection(GameObject parent)
        {
            // セクションコンテナ
            GameObject section = new GameObject("TimelineSyncSection");
            section.transform.SetParent(parent.transform, false);
            
            RectTransform sectionRect = section.AddComponent<RectTransform>();
            sectionRect.anchorMin = new Vector2(0, 1);
            sectionRect.anchorMax = new Vector2(0, 1);
            sectionRect.pivot = new Vector2(0, 1);
            sectionRect.anchoredPosition = new Vector2(10, -260);
            sectionRect.sizeDelta = new Vector2(360, 200);  // 高さを65から200に拡張
            
            // セパレータライン
            CreateTextAtPosition(section, "Separator", "─────── Timeline Sync ───────", 
                new Vector2(180, -5), new Vector2(340, 15), 12, TextAnchor.MiddleCenter, new Color(0.5f, 0.8f, 1f), false);
            
            // LTC TC表示
            CreateMonospaceTextAtPosition(section, "TimelineSyncLTC", "LTC:      --:--:--:--", 
                new Vector2(180, -20), new Vector2(340, 15), 11, TextAnchor.MiddleLeft, Color.white, false);
            
            // Timeline TC表示
            CreateMonospaceTextAtPosition(section, "TimelineSyncTimeline", "Timeline: --:--:--:--", 
                new Vector2(180, -35), new Vector2(340, 15), 11, TextAnchor.MiddleLeft, Color.white, false);
            
            // 時間差とステータス
            CreateTextAtPosition(section, "TimelineSyncDiff", "Diff: 0.000s [Stopped]", 
                new Vector2(90, -50), new Vector2(160, 15), 11, TextAnchor.MiddleLeft, Color.white, false);
            
            // Play状態
            CreateTextAtPosition(section, "TimelineSyncStatus", "Status: Stopped", 
                new Vector2(270, -50), new Vector2(160, 15), 11, TextAnchor.MiddleLeft, Color.gray, false);
            
            // 閾値とオフセット
            CreateTextAtPosition(section, "TimelineSyncThreshold", "Threshold: 0.50s | Offset: 0.00s", 
                new Vector2(180, -65), new Vector2(340, 15), 10, TextAnchor.MiddleCenter, new Color(0.7f, 0.7f, 0.7f), false);
            
            
            // Sync Settingsセクション追加
            CreateTextAtPosition(section, "SyncSettingsLabel", "─────── Sync Settings ───────", 
                new Vector2(180, -85), new Vector2(340, 15), 12, TextAnchor.MiddleCenter, new Color(0.5f, 0.8f, 1f), false);
            
            // Sync Threshold入力
            CreateTextAtPosition(section, "SyncThresholdLabel", "Sync Threshold:", 
                new Vector2(80, -105), new Vector2(120, 20), 11, TextAnchor.MiddleRight, Color.white, false);
            CreateInputFieldAtPosition(section, "SyncThresholdInput", "0.033", 
                new Vector2(230, -105), new Vector2(80, 20));
            CreateTextAtPosition(section, "SyncThresholdUnit", "s", 
                new Vector2(280, -105), new Vector2(20, 20), 11, TextAnchor.MiddleLeft, Color.white, false);
            
            // Observation Time入力
            CreateTextAtPosition(section, "ObservationTimeLabel", "Observation Time:", 
                new Vector2(80, -130), new Vector2(120, 20), 11, TextAnchor.MiddleRight, Color.white, false);
            CreateInputFieldAtPosition(section, "ObservationTimeInput", "0.1", 
                new Vector2(230, -130), new Vector2(80, 20));
            CreateTextAtPosition(section, "ObservationTimeUnit", "s", 
                new Vector2(280, -130), new Vector2(20, 20), 11, TextAnchor.MiddleLeft, Color.white, false);
            
            // Timeline Offset入力
            CreateTextAtPosition(section, "TimelineOffsetLabel", "Timeline Offset:", 
                new Vector2(80, -155), new Vector2(120, 20), 11, TextAnchor.MiddleRight, Color.white, false);
            CreateInputFieldAtPosition(section, "TimelineOffsetInput", "0.0", 
                new Vector2(230, -155), new Vector2(80, 20));
            CreateTextAtPosition(section, "TimelineOffsetUnit", "s", 
                new Vector2(280, -155), new Vector2(20, 20), 11, TextAnchor.MiddleLeft, Color.white, false);
            
            // Enable Syncトグル
            CreateTextAtPosition(section, "EnableSyncLabel", "Enable Sync:", 
                new Vector2(80, -180), new Vector2(120, 20), 11, TextAnchor.MiddleRight, Color.white, false);
            CreateToggleAtPosition(section, "EnableSyncToggle", 
                new Vector2(160, -180), new Vector2(20, 20));
            
            return section;
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
        /// 固定位置にInputField作成
        /// </summary>
        private static GameObject CreateInputFieldAtPosition(GameObject parent, string name, string defaultValue, 
            Vector2 position, Vector2 size)
        {
            GameObject inputObj = new GameObject(name);
            inputObj.transform.SetParent(parent.transform, false);
            
            RectTransform rect = inputObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            
            Image image = inputObj.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 1);
            
            InputField inputField = inputObj.AddComponent<InputField>();
            
            // Textコンポーネント
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(inputObj.transform, false);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(5, 2);
            textRect.offsetMax = new Vector2(-5, -2);
            
            Text text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 12;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = false;
            
            // Placeholder
            GameObject placeholderObj = new GameObject("Placeholder");
            placeholderObj.transform.SetParent(inputObj.transform, false);
            
            RectTransform placeholderRect = placeholderObj.AddComponent<RectTransform>();
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = new Vector2(5, 2);
            placeholderRect.offsetMax = new Vector2(-5, -2);
            
            Text placeholderText = placeholderObj.AddComponent<Text>();
            placeholderText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            placeholderText.fontSize = 12;
            placeholderText.fontStyle = FontStyle.Italic;
            placeholderText.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            placeholderText.alignment = TextAnchor.MiddleLeft;
            placeholderText.text = "";
            
            inputField.textComponent = text;
            inputField.placeholder = placeholderText;
            inputField.text = defaultValue;
            inputField.characterLimit = 10;
            
            return inputObj;
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
            
            // ScrollRectのContent内を検索するヘルパー
            Transform scrollContent = mainPanel.transform.Find("ScrollRect/Viewport/Content");
            
            // UI要素の参照を先に設定（Setupより前に）
            controller.currentTimecodeText = scrollContent?.Find("CurrentTimecodeText")?.GetComponent<Text>();
            controller.statusLineText = scrollContent?.Find("StatusLineText")?.GetComponent<Text>();
            
            // 互換性のための非表示要素
            controller.statusText = mainPanel.transform.Find("StatusText")?.GetComponent<Text>();
            controller.signalLevelText = mainPanel.transform.Find("SignalLevelText")?.GetComponent<Text>();
            controller.signalLevelBar = mainPanel.transform.Find("SignalLevelBar/Fill")?.GetComponent<Image>();
            
            // デバッグメッセージコンテナ
            controller.debugMessageContainer = scrollContent?.Find("DebugMessageContainer");
            controller.debugScrollRect = mainPanel.transform.Find("ScrollRect")?.GetComponent<ScrollRect>();
            
            // Audio Settings UIの参照を設定（新レイアウトではContent内の各行から取得）
            controller.deviceDropdown = scrollContent?.Find("DeviceRow/DeviceDropdown")?.GetComponent<Dropdown>();
            controller.frameRateDropdown = scrollContent?.Find("FrameRateRow/FrameRateDropdown")?.GetComponent<Dropdown>();
            controller.sampleRateDropdown = scrollContent?.Find("SampleRateRow/SampleRateDropdown")?.GetComponent<Dropdown>();
            
            // Timeline Sync UIの参照を設定
            controller.timelineSyncSection = scrollContent?.Find("TimelineSyncSection")?.gameObject;
            controller.timelineSyncLTCText = scrollContent?.Find("TimelineSyncSection/TimelineSyncLTC")?.GetComponent<Text>();
            controller.timelineSyncTimelineText = scrollContent?.Find("TimelineSyncSection/TimelineSyncTimeline")?.GetComponent<Text>();
            controller.timelineSyncDiffText = scrollContent?.Find("TimelineSyncSection/TimelineSyncDiff")?.GetComponent<Text>();
            controller.timelineSyncStatusText = scrollContent?.Find("TimelineSyncSection/TimelineSyncStatus")?.GetComponent<Text>();
            controller.timelineSyncThresholdText = scrollContent?.Find("TimelineSyncSection/TimelineSyncThreshold")?.GetComponent<Text>();
            
            // Sync Settings UIの参照を設定
            controller.syncThresholdInput = scrollContent?.Find("TimelineSyncSection/SyncThresholdRow/SyncThresholdInput")?.GetComponent<InputField>();
            controller.observationTimeInput = scrollContent?.Find("TimelineSyncSection/ObservationTimeRow/ObservationTimeInput")?.GetComponent<InputField>();
            controller.timelineOffsetInput = scrollContent?.Find("TimelineSyncSection/TimelineOffsetRow/TimelineOffsetInput")?.GetComponent<InputField>();
            controller.enableSyncToggle = scrollContent?.Find("TimelineSyncSection/EnableSyncRow/EnableSyncToggle")?.GetComponent<Toggle>();
            controller.snapToFpsToggle = scrollContent?.Find("TimelineSyncSection/SnapToFpsRow/SnapToFpsToggle")?.GetComponent<Toggle>();
            
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
            
            // ボタンのイベント設定（新レイアウトではButtonSection内のボタンを参照）
            Button clearButton = scrollContent?.Find("ButtonSection/ClearButton")?.GetComponent<Button>();
            Button exportButton = scrollContent?.Find("ButtonSection/ExportButton")?.GetComponent<Button>();
            Button copyButton = scrollContent?.Find("ButtonSection/CopyButton")?.GetComponent<Button>();
            
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