using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using LTC.Timeline;
using LTC.Debug;
using LTC.UI;
using System.Reflection;

namespace LTC.Editor
{
    /// <summary>
    /// LTCデバッグセットアップユーティリティ
    /// </summary>
    public static class LTCDebugSetup
    {
        [MenuItem("GameObject/LTC Debug/Create Debug Setup", false, 10)]
        public static void CreateDebugSetup()
        {
            // LTCDecoder + Debugger作成
            GameObject ltcObject = CreateLTCDecoderWithDebugger();
            
            // Debug UI Canvas作成
            GameObject canvas = CreateDebugCanvas();
            
            // UI要素作成と参照接続
            CreateUIElements(canvas, ltcObject);
            
            // 選択
            Selection.activeGameObject = ltcObject;
            
            UnityEngine.Debug.Log("[LTC Debug Setup] Debug setup created successfully!");
            UnityEngine.Debug.Log("All UI references have been automatically connected.");
            UnityEngine.Debug.Log("Press Play to start debugging!");
        }
        
        /// <summary>
        /// LTCDecoder + Debugger GameObjectを作成
        /// </summary>
        private static GameObject CreateLTCDecoderWithDebugger()
        {
            GameObject ltcObject = new GameObject("LTC Decoder with Debugger");
            
            // LTCDecoder追加
            LTCDecoder decoder = ltcObject.AddComponent<LTCDecoder>();
            
            // LTCEventDebugger追加
            LTCEventDebugger debugger = ltcObject.AddComponent<LTCEventDebugger>();
            
            // サンプルタイムコードイベントを追加
            AddSampleTimecodeEvents(decoder);
            
            return ltcObject;
        }
        
        /// <summary>
        /// Debug UI Canvasを作成
        /// </summary>
        private static GameObject CreateDebugCanvas()
        {
            // 既存のCanvasをチェック
            Canvas existingCanvas = GameObject.FindObjectOfType<Canvas>();
            GameObject canvasObject;
            
            if (existingCanvas != null)
            {
                canvasObject = existingCanvas.gameObject;
            }
            else
            {
                canvasObject = new GameObject("Canvas");
                Canvas canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.AddComponent<CanvasScaler>();
                canvasObject.AddComponent<GraphicRaycaster>();
                
                // EventSystemが無ければ作成
                if (GameObject.FindObjectOfType<EventSystem>() == null)
                {
                    GameObject eventSystem = new GameObject("EventSystem");
                    eventSystem.AddComponent<EventSystem>();
                    eventSystem.AddComponent<StandaloneInputModule>();
                }
            }
            
            return canvasObject;
        }
        
        /// <summary>
        /// UI要素を作成
        /// </summary>
        private static void CreateUIElements(GameObject canvas, GameObject ltcObject)
        {
            // メインパネル作成
            GameObject mainPanel = CreateMainPanel(canvas);
            
            // 各UIセクション作成
            GameObject statusPanel = CreateStatusPanel(mainPanel.transform);
            GameObject indicatorsPanel = CreateIndicatorsPanel(mainPanel.transform);
            GameObject messagesPanel = CreateDebugMessagesPanel(mainPanel.transform);
            GameObject controlsPanel = CreateControlsPanel(mainPanel.transform);
            
            // LTCDecoderUIコンポーネント追加と参照設定
            LTCDecoderUI decoderUI = mainPanel.AddComponent<LTCDecoderUI>();
            ConnectUIReferences(decoderUI, statusPanel, indicatorsPanel, messagesPanel, controlsPanel);
            
            // メッセージプレハブ作成
            CreateMessagePrefab(mainPanel, decoderUI);
        }
        
        /// <summary>
        /// メインパネル作成
        /// </summary>
        private static GameObject CreateMainPanel(GameObject canvas)
        {
            GameObject panel = new GameObject("LTC Debug Panel");
            panel.transform.SetParent(canvas.transform);
            
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 0.5f);
            rect.anchoredPosition = new Vector2(10, 0);
            rect.sizeDelta = new Vector2(400, -20);
            
            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.1f, 0.1f, 0.1f, 0.9f);
            
            VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.spacing = 10;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            
            return panel;
        }
        
        /// <summary>
        /// ステータスパネル作成
        /// </summary>
        private static GameObject CreateStatusPanel(Transform parent)
        {
            GameObject panel = new GameObject("Status Panel");
            panel.transform.SetParent(parent);
            
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 80);
            
            VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(5, 5, 5, 5);
            vlg.spacing = 5;
            
            // Status Text
            CreateTextElement(panel.transform, "Status", "StatusText", "NO SIGNAL", 16, TextAnchor.MiddleLeft);
            
            // Current Timecode
            CreateTextElement(panel.transform, "CurrentTC", "CurrentTimecodeText", "Current: 00:00:00:00", 14, TextAnchor.MiddleLeft);
            
            // Decoded Timecode
            CreateTextElement(panel.transform, "DecodedTC", "DecodedTimecodeText", "Decoded: 00:00:00:00", 14, TextAnchor.MiddleLeft);
            
            // Signal Level
            GameObject signalContainer = new GameObject("SignalContainer");
            signalContainer.transform.SetParent(panel.transform);
            RectTransform signalRect = signalContainer.AddComponent<RectTransform>();
            signalRect.sizeDelta = new Vector2(0, 20);
            
            HorizontalLayoutGroup hlg = signalContainer.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 10;
            
            CreateTextElement(signalContainer.transform, "SignalLabel", "SignalLevelLabel", "Signal:", 12, TextAnchor.MiddleLeft);
            
            // Signal Level Bar
            GameObject barContainer = new GameObject("SignalLevelBar");
            barContainer.transform.SetParent(signalContainer.transform);
            RectTransform barRect = barContainer.AddComponent<RectTransform>();
            barRect.sizeDelta = new Vector2(200, 10);
            
            Image barBg = barContainer.AddComponent<Image>();
            barBg.color = new Color(0.2f, 0.2f, 0.2f, 1);
            
            GameObject barFill = new GameObject("Fill");
            barFill.transform.SetParent(barContainer.transform);
            RectTransform fillRect = barFill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(0, 1);
            fillRect.pivot = new Vector2(0, 0.5f);
            fillRect.anchoredPosition = Vector2.zero;
            fillRect.sizeDelta = new Vector2(0, 0);
            
            Image fillImage = barFill.AddComponent<Image>();
            fillImage.color = Color.green;
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            
            CreateTextElement(signalContainer.transform, "SignalText", "SignalLevelText", "0%", 12, TextAnchor.MiddleLeft);
            
            return panel;
        }
        
        /// <summary>
        /// インジケーターパネル作成
        /// </summary>
        private static GameObject CreateIndicatorsPanel(Transform parent)
        {
            GameObject panel = new GameObject("Indicators Panel");
            panel.transform.SetParent(parent);
            
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 30);
            
            HorizontalLayoutGroup hlg = panel.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(5, 5, 5, 5);
            hlg.spacing = 20;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            
            // Receiving Indicator
            CreateIndicator(panel.transform, "ReceivingIndicator", "● Receiving", Color.gray);
            
            // Stopped Indicator
            CreateIndicator(panel.transform, "StoppedIndicator", "● Stopped", Color.gray);
            
            // Event Count
            CreateTextElement(panel.transform, "EventCount", "EventCountText", "Events: 0", 12, TextAnchor.MiddleLeft);
            
            return panel;
        }
        
        /// <summary>
        /// デバッグメッセージパネル作成
        /// </summary>
        private static GameObject CreateDebugMessagesPanel(Transform parent)
        {
            GameObject panel = new GameObject("Debug Messages Panel");
            panel.transform.SetParent(parent);
            
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 300);
            
            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.05f, 1);
            
            // ScrollView作成
            GameObject scrollView = new GameObject("DebugScrollView");
            scrollView.transform.SetParent(panel.transform);
            
            RectTransform scrollRect = scrollView.AddComponent<RectTransform>();
            scrollRect.anchorMin = Vector2.zero;
            scrollRect.anchorMax = Vector2.one;
            scrollRect.sizeDelta = Vector2.zero;
            scrollRect.anchoredPosition = Vector2.zero;
            
            ScrollRect scroll = scrollView.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.1f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 20f;
            
            // Viewport
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollView.transform);
            
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = new Vector2(-20, 0);
            viewportRect.anchoredPosition = new Vector2(-10, 0);
            
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(1, 1, 1, 0.01f);
            Mask mask = viewport.AddComponent<Mask>();
            mask.showMaskGraphic = false;
            
            // Content
            GameObject content = new GameObject("DebugMessageContainer");
            content.transform.SetParent(viewport.transform);
            
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 0);
            
            VerticalLayoutGroup contentVlg = content.AddComponent<VerticalLayoutGroup>();
            contentVlg.padding = new RectOffset(5, 5, 5, 5);
            contentVlg.spacing = 2;
            contentVlg.childControlWidth = true;
            contentVlg.childControlHeight = false;
            contentVlg.childForceExpandWidth = true;
            contentVlg.childForceExpandHeight = false;
            
            ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // ScrollRectの設定
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            
            // Scrollbar
            GameObject scrollbar = new GameObject("Scrollbar");
            scrollbar.transform.SetParent(scrollView.transform);
            
            RectTransform scrollbarRect = scrollbar.AddComponent<RectTransform>();
            scrollbarRect.anchorMin = new Vector2(1, 0);
            scrollbarRect.anchorMax = new Vector2(1, 1);
            scrollbarRect.pivot = new Vector2(1, 0.5f);
            scrollbarRect.anchoredPosition = new Vector2(0, 0);
            scrollbarRect.sizeDelta = new Vector2(20, 0);
            
            Image scrollbarBg = scrollbar.AddComponent<Image>();
            scrollbarBg.color = new Color(0.1f, 0.1f, 0.1f, 1);
            
            Scrollbar sb = scrollbar.AddComponent<Scrollbar>();
            sb.direction = Scrollbar.Direction.BottomToTop;
            
            // Scrollbar Handle
            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(scrollbar.transform);
            
            RectTransform handleRect = handle.AddComponent<RectTransform>();
            handleRect.anchorMin = Vector2.zero;
            handleRect.anchorMax = Vector2.one;
            handleRect.sizeDelta = Vector2.zero;
            handleRect.anchoredPosition = Vector2.zero;
            
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.3f, 0.3f, 0.3f, 1);
            
            sb.targetGraphic = handleImage;
            sb.handleRect = handleRect;
            
            scroll.verticalScrollbar = sb;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            scroll.verticalScrollbarSpacing = -3;
            
            return panel;
        }
        
        /// <summary>
        /// コントロールパネル作成
        /// </summary>
        private static GameObject CreateControlsPanel(Transform parent)
        {
            GameObject panel = new GameObject("Controls Panel");
            panel.transform.SetParent(parent);
            
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 60);
            
            VerticalLayoutGroup vlg = panel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(5, 5, 5, 5);
            vlg.spacing = 5;
            
            // ボタン行
            GameObject buttonRow = new GameObject("ButtonRow");
            buttonRow.transform.SetParent(panel.transform);
            RectTransform buttonRowRect = buttonRow.AddComponent<RectTransform>();
            buttonRowRect.sizeDelta = new Vector2(0, 25);
            
            HorizontalLayoutGroup buttonHlg = buttonRow.AddComponent<HorizontalLayoutGroup>();
            buttonHlg.spacing = 5;
            
            CreateButton(buttonRow.transform, "ClearButton", "Clear", 80);
            CreateButton(buttonRow.transform, "ExportButton", "Export CSV", 80);
            CreateButton(buttonRow.transform, "CopyButton", "Copy", 80);
            
            // フィルター行
            GameObject filterRow = new GameObject("FilterRow");
            filterRow.transform.SetParent(panel.transform);
            RectTransform filterRowRect = filterRow.AddComponent<RectTransform>();
            filterRowRect.sizeDelta = new Vector2(0, 25);
            
            HorizontalLayoutGroup filterHlg = filterRow.AddComponent<HorizontalLayoutGroup>();
            filterHlg.spacing = 5;
            
            CreateTextElement(filterRow.transform, "FilterLabel", "FilterLabel", "Filter:", 12, TextAnchor.MiddleLeft);
            
            GameObject dropdown = new GameObject("FilterDropdown");
            dropdown.transform.SetParent(filterRow.transform);
            RectTransform dropdownRect = dropdown.AddComponent<RectTransform>();
            dropdownRect.sizeDelta = new Vector2(150, 25);
            
            Image dropdownBg = dropdown.AddComponent<Image>();
            dropdownBg.color = new Color(0.2f, 0.2f, 0.2f, 1);
            
            Dropdown dd = dropdown.AddComponent<Dropdown>();
            
            // Dropdown Template等の設定（簡略化）
            CreateDropdownTemplate(dd, dropdown);
            
            return panel;
        }
        
        /// <summary>
        /// UI参照を接続
        /// </summary>
        private static void ConnectUIReferences(LTCDecoderUI decoderUI, GameObject statusPanel, 
            GameObject indicatorsPanel, GameObject messagesPanel, GameObject controlsPanel)
        {
            // ステータス関連
            SetPrivateField(decoderUI, "statusText", GameObject.Find("StatusText")?.GetComponent<Text>());
            SetPrivateField(decoderUI, "currentTimecodeText", GameObject.Find("CurrentTimecodeText")?.GetComponent<Text>());
            SetPrivateField(decoderUI, "decodedTimecodeText", GameObject.Find("DecodedTimecodeText")?.GetComponent<Text>());
            SetPrivateField(decoderUI, "signalLevelBar", GameObject.Find("SignalLevelBar/Fill")?.GetComponent<Image>());
            SetPrivateField(decoderUI, "signalLevelText", GameObject.Find("SignalLevelText")?.GetComponent<Text>());
            
            // インジケーター
            SetPrivateField(decoderUI, "receivingIndicator", GameObject.Find("ReceivingIndicator")?.GetComponent<Image>());
            SetPrivateField(decoderUI, "stoppedIndicator", GameObject.Find("StoppedIndicator")?.GetComponent<Image>());
            SetPrivateField(decoderUI, "eventCountText", GameObject.Find("EventCountText")?.GetComponent<Text>());
            
            // デバッグメッセージ
            SetPrivateField(decoderUI, "debugScrollView", GameObject.Find("DebugScrollView")?.GetComponent<ScrollRect>());
            SetPrivateField(decoderUI, "debugMessageContainer", GameObject.Find("DebugMessageContainer")?.transform);
            
            // コントロール
            SetPrivateField(decoderUI, "clearButton", GameObject.Find("ClearButton")?.GetComponent<Button>());
            SetPrivateField(decoderUI, "exportButton", GameObject.Find("ExportButton")?.GetComponent<Button>());
            SetPrivateField(decoderUI, "copyButton", GameObject.Find("CopyButton")?.GetComponent<Button>());
            SetPrivateField(decoderUI, "filterDropdown", GameObject.Find("FilterDropdown")?.GetComponent<Dropdown>());
            
            UnityEngine.Debug.Log("[LTC Debug Setup] UI references connected successfully");
        }
        
        /// <summary>
        /// メッセージプレハブ作成
        /// </summary>
        private static void CreateMessagePrefab(GameObject mainPanel, LTCDecoderUI decoderUI)
        {
            GameObject prefab = new GameObject("MessagePrefab");
            prefab.transform.SetParent(mainPanel.transform);
            prefab.SetActive(false);
            
            RectTransform rect = prefab.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 18);
            
            Text text = prefab.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 11;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleLeft;
            text.text = "[00:00:00] [Info] Sample message";
            
            SetPrivateField(decoderUI, "messagePrefab", prefab);
        }
        
        /// <summary>
        /// サンプルタイムコードイベント追加
        /// </summary>
        private static void AddSampleTimecodeEvents(LTCDecoder decoder)
        {
            var tcEvents = decoder.GetType().GetField("timecodeEvents",
                BindingFlags.NonPublic | BindingFlags.Instance);
            
            if (tcEvents != null)
            {
                var list = new System.Collections.Generic.List<TimecodeEvent>();
                
                // サンプルイベント
                list.Add(new TimecodeEvent
                {
                    eventName = "10 Second Mark",
                    targetTimecode = "00:00:10:00",
                    toleranceFrames = 2,
                    oneShot = true,
                    enabled = true
                });
                
                list.Add(new TimecodeEvent
                {
                    eventName = "30 Second Mark",
                    targetTimecode = "00:00:30:00",
                    toleranceFrames = 2,
                    oneShot = false,
                    enabled = true
                });
                
                tcEvents.SetValue(decoder, list);
            }
        }
        
        #region ヘルパーメソッド
        
        private static void CreateTextElement(Transform parent, string name, string objName, 
            string text, int fontSize, TextAnchor anchor)
        {
            GameObject textObj = new GameObject(objName);
            textObj.transform.SetParent(parent);
            
            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, fontSize + 4);
            
            Text textComp = textObj.AddComponent<Text>();
            textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComp.fontSize = fontSize;
            textComp.color = Color.white;
            textComp.alignment = anchor;
            textComp.text = text;
        }
        
        private static void CreateIndicator(Transform parent, string name, string text, Color color)
        {
            GameObject indicator = new GameObject(name);
            indicator.transform.SetParent(parent);
            
            RectTransform rect = indicator.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(100, 20);
            
            HorizontalLayoutGroup hlg = indicator.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 5;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            
            // Dot
            GameObject dot = new GameObject("Dot");
            dot.transform.SetParent(indicator.transform);
            RectTransform dotRect = dot.AddComponent<RectTransform>();
            dotRect.sizeDelta = new Vector2(10, 10);
            
            Image dotImage = dot.AddComponent<Image>();
            dotImage.color = color;
            
            // Label
            CreateTextElement(indicator.transform, "Label", "Label", text.Substring(2), 12, TextAnchor.MiddleLeft);
        }
        
        private static void CreateButton(Transform parent, string name, string text, float width)
        {
            GameObject buttonObj = new GameObject(name);
            buttonObj.transform.SetParent(parent);
            
            RectTransform rect = buttonObj.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, 25);
            
            Image bg = buttonObj.AddComponent<Image>();
            bg.color = new Color(0.3f, 0.3f, 0.3f, 1);
            
            Button button = buttonObj.AddComponent<Button>();
            button.targetGraphic = bg;
            
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(buttonObj.transform);
            
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;
            
            Text textComp = textObj.AddComponent<Text>();
            textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComp.fontSize = 12;
            textComp.color = Color.white;
            textComp.alignment = TextAnchor.MiddleCenter;
            textComp.text = text;
        }
        
        private static void CreateDropdownTemplate(Dropdown dropdown, GameObject dropdownObj)
        {
            // Template作成（簡略化）
            GameObject template = new GameObject("Template");
            template.transform.SetParent(dropdownObj.transform);
            template.SetActive(false);
            
            RectTransform templateRect = template.AddComponent<RectTransform>();
            templateRect.anchorMin = new Vector2(0, 0);
            templateRect.anchorMax = new Vector2(1, 0);
            templateRect.pivot = new Vector2(0.5f, 1);
            templateRect.anchoredPosition = new Vector2(0, 2);
            templateRect.sizeDelta = new Vector2(0, 150);
            
            Image templateBg = template.AddComponent<Image>();
            templateBg.color = new Color(0.15f, 0.15f, 0.15f, 1);
            
            dropdown.template = templateRect;
        }
        
        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (value == null) return;
            
            var field = target.GetType().GetField(fieldName, 
                BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }
        
        #endregion
    }
}