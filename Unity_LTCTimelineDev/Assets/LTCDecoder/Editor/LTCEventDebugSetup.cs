using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using LTC.Debug;
using LTC.Timeline;

namespace LTC.Editor
{
    /// <summary>
    /// LTCイベントデバッグ環境のセットアップヘルパー
    /// </summary>
    public static class LTCEventDebugSetup
    {
        [MenuItem("GameObject/LTC Debug/Create Debug Setup (Complete)", false, 10)]
        public static void CreateCompleteDebugSetup()
        {
            // LTCDecoder GameObjectを作成
            GameObject ltcObject = new GameObject("LTC Decoder with Debugger");
            LTCDecoder decoder = ltcObject.AddComponent<LTCDecoder>();
            LTCEventDebugger debugger = ltcObject.AddComponent<LTCEventDebugger>();
            
            // サンプルタイムコードイベントを追加
            var tcEvents = decoder.GetType().GetField("timecodeEvents",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (tcEvents != null)
            {
                var list = new System.Collections.Generic.List<TimecodeEvent>();
                
                // サンプルイベント1
                var event1 = new TimecodeEvent
                {
                    eventName = "10 Second Mark",
                    targetTimecode = "00:00:10:00",
                    toleranceFrames = 2,
                    oneShot = true,
                    enabled = true
                };
                list.Add(event1);
                
                // サンプルイベント2
                var event2 = new TimecodeEvent
                {
                    eventName = "30 Second Mark",
                    targetTimecode = "00:00:30:00",
                    toleranceFrames = 2,
                    oneShot = false,
                    enabled = true
                };
                list.Add(event2);
                
                // サンプルイベント3
                var event3 = new TimecodeEvent
                {
                    eventName = "1 Minute Mark",
                    targetTimecode = "00:01:00:00",
                    toleranceFrames = 3,
                    oneShot = true,
                    enabled = true
                };
                list.Add(event3);
                
                tcEvents.SetValue(decoder, list);
            }
            
            // 完全なUIキャンバスを作成して参照を設定
            CreateCompleteDebugUI(debugger);
            
            // 選択
            Selection.activeGameObject = ltcObject;
            
            UnityEngine.Debug.Log("[LTC Debug Setup] Complete setup created successfully!");
            UnityEngine.Debug.Log("All UI references have been automatically connected.");
            UnityEngine.Debug.Log("Press Play to start debugging!");
        }
        
        private static void CreateCompleteDebugUI(LTCEventDebugger debugger)
        {
            // Canvas を作成
            GameObject canvasObject = GameObject.Find("LTC Debug Canvas");
            if (canvasObject != null)
            {
                // 既存のCanvasがあれば削除
                Object.DestroyImmediate(canvasObject);
            }
            
            canvasObject = new GameObject("LTC Debug Canvas");
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObject.AddComponent<GraphicRaycaster>();
            
            // EventSystemを作成（必要な場合）
            if (GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
            
            // メインパネルを作成
            GameObject mainPanel = CreateMainPanel(canvasObject.transform);
            
            // LTCEventDebugUIコンポーネントを追加
            LTCEventDebugUI debugUI = mainPanel.AddComponent<LTCEventDebugUI>();
            
            // すべてのUI参照を設定
            SetupAllReferences(mainPanel, debugUI, debugger);
            
            UnityEngine.Debug.Log("[LTC Debug UI] All references connected successfully!");
        }
        
        private static GameObject CreateMainPanel(Transform canvasTransform)
        {
            GameObject mainPanel = new GameObject("LTC Debug Main Panel");
            mainPanel.transform.SetParent(canvasTransform);
            
            RectTransform mainRect = mainPanel.AddComponent<RectTransform>();
            mainRect.anchorMin = new Vector2(0, 0);
            mainRect.anchorMax = new Vector2(0.3f, 1);
            mainRect.offsetMin = new Vector2(10, 10);
            mainRect.offsetMax = new Vector2(-10, -10);
            
            // 背景
            Image bgImage = mainPanel.AddComponent<Image>();
            bgImage.color = new Color(0.1f, 0.1f, 0.1f, 0.95f);
            
            // メインレイアウト
            VerticalLayoutGroup mainLayout = mainPanel.AddComponent<VerticalLayoutGroup>();
            mainLayout.padding = new RectOffset(15, 15, 15, 15);
            mainLayout.spacing = 10;
            mainLayout.childControlWidth = true;
            mainLayout.childControlHeight = false;
            mainLayout.childForceExpandWidth = true;
            mainLayout.childForceExpandHeight = false;
            
            // ヘッダー
            CreateHeader(mainPanel.transform);
            
            // ステータスセクション
            CreateStatusSection(mainPanel.transform);
            
            // イベントインジケーターセクション
            CreateEventIndicatorsSection(mainPanel.transform);
            
            // タイムコードイベントセクション
            CreateTimecodeEventsSection(mainPanel.transform);
            
            // イベント履歴セクション
            CreateEventHistorySection(mainPanel.transform);
            
            // 統計セクション
            CreateStatisticsSection(mainPanel.transform);
            
            return mainPanel;
        }
        
        private static void CreateHeader(Transform parent)
        {
            GameObject header = new GameObject("Header");
            header.transform.SetParent(parent);
            
            RectTransform headerRect = header.AddComponent<RectTransform>();
            headerRect.sizeDelta = new Vector2(0, 40);
            
            Text headerText = header.AddComponent<Text>();
            headerText.text = "LTC Event Debugger";
            headerText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            headerText.fontSize = 20;
            headerText.fontStyle = FontStyle.Bold;
            headerText.color = Color.white;
            headerText.alignment = TextAnchor.MiddleCenter;
            
            // セパレーター
            CreateSeparator(parent);
        }
        
        private static void CreateStatusSection(Transform parent)
        {
            GameObject section = CreateSection(parent, "Status", 80);
            Transform content = section.transform.Find("Content");
            
            // ステータステキスト
            CreateLabeledText(content, "Status:", "Waiting...", "StatusLabel", "StatusText");
            
            // タイムコード
            GameObject tcObject = new GameObject("TimecodeDisplay");
            tcObject.transform.SetParent(content);
            RectTransform tcRect = tcObject.AddComponent<RectTransform>();
            tcRect.sizeDelta = new Vector2(0, 35);
            
            Text tcText = tcObject.AddComponent<Text>();
            tcText.name = "CurrentTimecodeText";
            tcText.text = "00:00:00:00";
            tcText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tcText.fontSize = 24;
            tcText.fontStyle = FontStyle.Bold;
            tcText.color = new Color(0.5f, 1f, 0.5f);
            tcText.alignment = TextAnchor.MiddleCenter;
            
            // 信号レベル
            CreateLabeledText(content, "Signal:", "0%", "SignalLabel", "SignalLevelText");
            
            // 信号レベルバー
            GameObject barContainer = new GameObject("SignalLevelBarContainer");
            barContainer.transform.SetParent(content);
            RectTransform barContainerRect = barContainer.AddComponent<RectTransform>();
            barContainerRect.sizeDelta = new Vector2(0, 20);
            
            GameObject barBg = new GameObject("BarBackground");
            barBg.transform.SetParent(barContainer.transform);
            RectTransform barBgRect = barBg.AddComponent<RectTransform>();
            barBgRect.anchorMin = Vector2.zero;
            barBgRect.anchorMax = Vector2.one;
            barBgRect.sizeDelta = Vector2.zero;
            barBgRect.anchoredPosition = Vector2.zero;
            Image barBgImage = barBg.AddComponent<Image>();
            barBgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
            
            GameObject bar = new GameObject("SignalLevelBar");
            bar.transform.SetParent(barContainer.transform);
            RectTransform barRect = bar.AddComponent<RectTransform>();
            barRect.anchorMin = new Vector2(0, 0);
            barRect.anchorMax = new Vector2(0, 1);
            barRect.sizeDelta = new Vector2(100, 0);
            barRect.anchoredPosition = Vector2.zero;
            Image barImage = bar.AddComponent<Image>();
            barImage.color = Color.green;
            barImage.type = Image.Type.Filled;
            barImage.fillMethod = Image.FillMethod.Horizontal;
            barImage.fillAmount = 0;
        }
        
        private static void CreateEventIndicatorsSection(Transform parent)
        {
            GameObject section = CreateSection(parent, "Event Indicators", 120);
            Transform content = section.transform.Find("Content");
            
            // LTC Started
            GameObject startedRow = CreateEventIndicatorRow(content, "LTC Started", "StartedRow");
            CreateIndicator(startedRow.transform, "LtcStartedIndicator");
            CreateText(startedRow.transform, "Count: 0", "LtcStartedCountText", 12);
            CreateText(startedRow.transform, "Last: --:--:--", "LtcStartedLastTimeText", 12);
            
            // LTC Stopped
            GameObject stoppedRow = CreateEventIndicatorRow(content, "LTC Stopped", "StoppedRow");
            CreateIndicator(stoppedRow.transform, "LtcStoppedIndicator");
            CreateText(stoppedRow.transform, "Count: 0", "LtcStoppedCountText", 12);
            CreateText(stoppedRow.transform, "Last: --:--:--", "LtcStoppedLastTimeText", 12);
            
            // LTC Receiving
            GameObject receivingRow = CreateEventIndicatorRow(content, "LTC Receiving", "ReceivingRow");
            CreateIndicator(receivingRow.transform, "LtcReceivingIndicator");
            CreateText(receivingRow.transform, "0 fps", "LtcReceivingFPSText", 12);
            
            // LTC No Signal
            GameObject noSignalRow = CreateEventIndicatorRow(content, "LTC No Signal", "NoSignalRow");
            CreateIndicator(noSignalRow.transform, "LtcNoSignalIndicator");
            CreateText(noSignalRow.transform, "0.0s", "LtcNoSignalDurationText", 12);
        }
        
        private static void CreateTimecodeEventsSection(Transform parent)
        {
            GameObject section = CreateSection(parent, "Timecode Events", 100);
            Transform content = section.transform.Find("Content");
            
            // スクロールビュー for タイムコードイベント
            GameObject scrollView = CreateScrollView(content, "TimecodeEventsScrollView", 80);
            Transform scrollContent = scrollView.transform.Find("Viewport/Content");
            scrollContent.name = "TimecodeEventsContent";
            
            // プレハブプレースホルダー（実行時に動的生成されるため）
            GameObject placeholder = new GameObject("TimecodeEventPlaceholder");
            placeholder.transform.SetParent(scrollContent);
            Text placeholderText = placeholder.AddComponent<Text>();
            placeholderText.text = "Timecode events will appear here";
            placeholderText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            placeholderText.fontSize = 11;
            placeholderText.color = new Color(0.5f, 0.5f, 0.5f);
            placeholderText.alignment = TextAnchor.MiddleCenter;
            RectTransform placeholderRect = placeholder.GetComponent<RectTransform>();
            placeholderRect.sizeDelta = new Vector2(0, 30);
        }
        
        private static void CreateEventHistorySection(Transform parent)
        {
            GameObject section = CreateSection(parent, "Event History", 200);
            Transform content = section.transform.Find("Content");
            
            // スクロールビュー
            GameObject scrollView = CreateScrollView(content, "HistoryScrollView", 180);
            Transform scrollContent = scrollView.transform.Find("Viewport/Content");
            scrollContent.name = "HistoryContent";
            
            // エントリープレハブを作成
            GameObject entryPrefab = CreateHistoryEntryPrefab();
            entryPrefab.transform.SetParent(scrollContent);
            entryPrefab.SetActive(false);
            entryPrefab.name = "HistoryEntryPrefab";
        }
        
        private static void CreateStatisticsSection(Transform parent)
        {
            GameObject section = CreateSection(parent, "Statistics", 80);
            Transform content = section.transform.Find("Content");
            
            CreateText(content, "Total Events: 0", "TotalEventsText", 12);
            CreateText(content, "Session: 00:00:00", "SessionDurationText", 12);
            CreateText(content, "Avg Signal: 0%", "AverageSignalText", 12);
        }
        
        private static GameObject CreateSection(Transform parent, string title, float height)
        {
            GameObject section = new GameObject(title + " Section");
            section.transform.SetParent(parent);
            
            RectTransform sectionRect = section.AddComponent<RectTransform>();
            sectionRect.sizeDelta = new Vector2(0, height);
            
            // セクション背景
            Image sectionBg = section.AddComponent<Image>();
            sectionBg.color = new Color(0.15f, 0.15f, 0.15f, 1f);
            
            // セクションレイアウト
            VerticalLayoutGroup sectionLayout = section.AddComponent<VerticalLayoutGroup>();
            sectionLayout.padding = new RectOffset(10, 10, 5, 5);
            sectionLayout.spacing = 5;
            sectionLayout.childControlWidth = true;
            sectionLayout.childControlHeight = false;
            sectionLayout.childForceExpandWidth = true;
            
            // タイトル
            GameObject titleObj = new GameObject("Title");
            titleObj.transform.SetParent(section.transform);
            Text titleText = titleObj.AddComponent<Text>();
            titleText.text = title;
            titleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            titleText.fontSize = 14;
            titleText.fontStyle = FontStyle.Bold;
            titleText.color = new Color(0.8f, 0.8f, 0.8f);
            RectTransform titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.sizeDelta = new Vector2(0, 20);
            
            // コンテンツコンテナ
            GameObject contentObj = new GameObject("Content");
            contentObj.transform.SetParent(section.transform);
            RectTransform contentRect = contentObj.AddComponent<RectTransform>();
            contentRect.sizeDelta = new Vector2(0, height - 30);
            
            VerticalLayoutGroup contentLayout = contentObj.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(5, 5, 0, 0);
            contentLayout.spacing = 3;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = true;
            
            return section;
        }
        
        private static GameObject CreateEventIndicatorRow(Transform parent, string label, string name)
        {
            GameObject row = new GameObject(name);
            row.transform.SetParent(parent);
            
            RectTransform rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0, 20);
            
            HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 5;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;
            
            // ラベル
            GameObject labelObj = new GameObject("Label");
            labelObj.transform.SetParent(row.transform);
            Text labelText = labelObj.AddComponent<Text>();
            labelText.text = label + ":";
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 12;
            labelText.color = Color.white;
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(100, 0);
            
            return row;
        }
        
        private static void CreateIndicator(Transform parent, string name)
        {
            GameObject indicator = new GameObject(name);
            indicator.transform.SetParent(parent);
            
            Image indicatorImage = indicator.AddComponent<Image>();
            indicatorImage.color = Color.gray;
            
            RectTransform indicatorRect = indicator.GetComponent<RectTransform>();
            indicatorRect.sizeDelta = new Vector2(12, 12);
        }
        
        private static void CreateText(Transform parent, string text, string name, int fontSize)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent);
            
            Text textComp = textObj.AddComponent<Text>();
            textComp.text = text;
            textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComp.fontSize = fontSize;
            textComp.color = Color.white;
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(80, 0);
        }
        
        private static void CreateLabeledText(Transform parent, string label, string value, string labelName, string valueName)
        {
            GameObject row = new GameObject(labelName + "Row");
            row.transform.SetParent(parent);
            
            RectTransform rowRect = row.AddComponent<RectTransform>();
            rowRect.sizeDelta = new Vector2(0, 20);
            
            HorizontalLayoutGroup rowLayout = row.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 5;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = true;
            
            // ラベル
            GameObject labelObj = new GameObject(labelName);
            labelObj.transform.SetParent(row.transform);
            Text labelText = labelObj.AddComponent<Text>();
            labelText.text = label;
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            labelText.fontSize = 12;
            labelText.color = new Color(0.7f, 0.7f, 0.7f);
            RectTransform labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.sizeDelta = new Vector2(50, 0);
            
            // 値
            GameObject valueObj = new GameObject(valueName);
            valueObj.transform.SetParent(row.transform);
            Text valueText = valueObj.AddComponent<Text>();
            valueText.text = value;
            valueText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            valueText.fontSize = 12;
            valueText.fontStyle = FontStyle.Bold;
            valueText.color = Color.white;
            RectTransform valueRect = valueObj.GetComponent<RectTransform>();
            valueRect.sizeDelta = new Vector2(150, 0);
        }
        
        private static GameObject CreateHistoryEntryPrefab()
        {
            GameObject entry = new GameObject("HistoryEntry");
            
            RectTransform entryRect = entry.AddComponent<RectTransform>();
            entryRect.sizeDelta = new Vector2(0, 20);
            
            Image entryBg = entry.AddComponent<Image>();
            entryBg.color = new Color(0.2f, 0.2f, 0.2f, 0.5f);
            
            HorizontalLayoutGroup entryLayout = entry.AddComponent<HorizontalLayoutGroup>();
            entryLayout.padding = new RectOffset(5, 5, 2, 2);
            entryLayout.spacing = 5;
            entryLayout.childControlWidth = false;
            entryLayout.childControlHeight = true;
            
            // タイムスタンプ
            CreateEntryText(entry.transform, "00:00:00.000", "Timestamp", 60);
            // イベント名
            CreateEntryText(entry.transform, "Event", "EventName", 80);
            // タイムコード
            CreateEntryText(entry.transform, "00:00:00:00", "Timecode", 70);
            
            return entry;
        }
        
        private static void CreateEntryText(Transform parent, string text, string name, float width)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent);
            
            Text textComp = textObj.AddComponent<Text>();
            textComp.text = text;
            textComp.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComp.fontSize = 10;
            textComp.color = Color.white;
            
            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(width, 0);
        }
        
        private static GameObject CreateScrollView(Transform parent, string name, float height)
        {
            GameObject scrollView = new GameObject(name);
            scrollView.transform.SetParent(parent);
            
            RectTransform scrollRect = scrollView.GetComponent<RectTransform>() ?? scrollView.AddComponent<RectTransform>();
            scrollRect.sizeDelta = new Vector2(0, height);
            
            ScrollRect scroll = scrollView.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 20;
            
            // 背景
            Image scrollBg = scrollView.AddComponent<Image>();
            scrollBg.color = new Color(0.1f, 0.1f, 0.1f, 0.5f);
            
            // Viewport
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollView.transform);
            
            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.anchoredPosition = Vector2.zero;
            
            Image viewportImage = viewport.AddComponent<Image>();
            viewportImage.color = new Color(1, 1, 1, 0.01f);
            Mask viewportMask = viewport.AddComponent<Mask>();
            viewportMask.showMaskGraphic = false;
            
            // Content
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform);
            
            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 0);
            
            VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
            contentLayout.padding = new RectOffset(5, 5, 5, 5);
            contentLayout.spacing = 2;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = true;
            
            ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // ScrollRectの設定
            scroll.content = contentRect;
            scroll.viewport = viewportRect;
            
            return scrollView;
        }
        
        private static GameObject CreateSeparator(Transform parent)
        {
            GameObject separator = new GameObject("Separator");
            separator.transform.SetParent(parent);
            
            Image image = separator.AddComponent<Image>();
            image.color = new Color(1, 1, 1, 0.2f);
            
            RectTransform rect = separator.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, 1);
            
            return separator;
        }
        
        private static void SetupAllReferences(GameObject mainPanel, LTCEventDebugUI debugUI, LTCEventDebugger debugger)
        {
            // デバッガー参照
            SetPrivateField(debugUI, "debugger", debugger);
            
            // ステータス関連
            var statusText = GameObject.Find("StatusText")?.GetComponent<Text>();
            if (statusText) SetPrivateField(debugUI, "statusText", statusText);
            
            var currentTimecodeText = GameObject.Find("CurrentTimecodeText")?.GetComponent<Text>();
            if (currentTimecodeText) SetPrivateField(debugUI, "currentTimecodeText", currentTimecodeText);
            
            var signalLevelText = GameObject.Find("SignalLevelText")?.GetComponent<Text>();
            if (signalLevelText) SetPrivateField(debugUI, "signalLevelText", signalLevelText);
            
            var signalLevelBar = GameObject.Find("SignalLevelBar")?.GetComponent<Image>();
            if (signalLevelBar) SetPrivateField(debugUI, "signalLevelBar", signalLevelBar);
            
            // イベントインジケーター - LTC Started
            var ltcStartedIndicator = GameObject.Find("LtcStartedIndicator")?.GetComponent<Image>();
            if (ltcStartedIndicator) SetPrivateField(debugUI, "ltcStartedIndicator", ltcStartedIndicator);
            
            var ltcStartedCountText = GameObject.Find("LtcStartedCountText")?.GetComponent<Text>();
            if (ltcStartedCountText) SetPrivateField(debugUI, "ltcStartedCountText", ltcStartedCountText);
            
            var ltcStartedLastTimeText = GameObject.Find("LtcStartedLastTimeText")?.GetComponent<Text>();
            if (ltcStartedLastTimeText) SetPrivateField(debugUI, "ltcStartedLastTimeText", ltcStartedLastTimeText);
            
            // イベントインジケーター - LTC Stopped
            var ltcStoppedIndicator = GameObject.Find("LtcStoppedIndicator")?.GetComponent<Image>();
            if (ltcStoppedIndicator) SetPrivateField(debugUI, "ltcStoppedIndicator", ltcStoppedIndicator);
            
            var ltcStoppedCountText = GameObject.Find("LtcStoppedCountText")?.GetComponent<Text>();
            if (ltcStoppedCountText) SetPrivateField(debugUI, "ltcStoppedCountText", ltcStoppedCountText);
            
            var ltcStoppedLastTimeText = GameObject.Find("LtcStoppedLastTimeText")?.GetComponent<Text>();
            if (ltcStoppedLastTimeText) SetPrivateField(debugUI, "ltcStoppedLastTimeText", ltcStoppedLastTimeText);
            
            // イベントインジケーター - LTC Receiving
            var ltcReceivingIndicator = GameObject.Find("LtcReceivingIndicator")?.GetComponent<Image>();
            if (ltcReceivingIndicator) SetPrivateField(debugUI, "ltcReceivingIndicator", ltcReceivingIndicator);
            
            var ltcReceivingFPSText = GameObject.Find("LtcReceivingFPSText")?.GetComponent<Text>();
            if (ltcReceivingFPSText) SetPrivateField(debugUI, "ltcReceivingFPSText", ltcReceivingFPSText);
            
            // イベントインジケーター - LTC No Signal
            var ltcNoSignalIndicator = GameObject.Find("LtcNoSignalIndicator")?.GetComponent<Image>();
            if (ltcNoSignalIndicator) SetPrivateField(debugUI, "ltcNoSignalIndicator", ltcNoSignalIndicator);
            
            var ltcNoSignalDurationText = GameObject.Find("LtcNoSignalDurationText")?.GetComponent<Text>();
            if (ltcNoSignalDurationText) SetPrivateField(debugUI, "ltcNoSignalDurationText", ltcNoSignalDurationText);
            
            // イベント履歴
            var historyContent = GameObject.Find("HistoryContent")?.transform;
            if (historyContent) SetPrivateField(debugUI, "historyContent", historyContent);
            
            // 履歴エントリプレハブを作成
            GameObject historyEntryPrefab = CreateHistoryEntryPrefab();
            historyEntryPrefab.transform.SetParent(mainPanel.transform);
            historyEntryPrefab.SetActive(false);
            historyEntryPrefab.name = "HistoryEntryPrefab";
            SetPrivateField(debugUI, "historyEntryPrefab", historyEntryPrefab);
            
            var historyScrollRect = GameObject.Find("HistoryScrollView")?.GetComponent<ScrollRect>();
            if (historyScrollRect) SetPrivateField(debugUI, "historyScrollRect", historyScrollRect);
            
            // タイムコードイベント
            var timecodeEventsContent = GameObject.Find("TimecodeEventsContent")?.transform;
            if (timecodeEventsContent) SetPrivateField(debugUI, "timecodeEventsContent", timecodeEventsContent);
            
            // タイムコードイベントプレハブを作成
            GameObject tcEventPrefab = CreateTimecodeEventPrefab();
            tcEventPrefab.transform.SetParent(mainPanel.transform);
            tcEventPrefab.SetActive(false);
            tcEventPrefab.name = "TimecodeEventPrefab";
            SetPrivateField(debugUI, "timecodeEventPrefab", tcEventPrefab);
            
            // 統計
            var totalEventsText = GameObject.Find("TotalEventsText")?.GetComponent<Text>();
            if (totalEventsText) SetPrivateField(debugUI, "totalEventsText", totalEventsText);
            
            var sessionDurationText = GameObject.Find("SessionDurationText")?.GetComponent<Text>();
            if (sessionDurationText) SetPrivateField(debugUI, "sessionDurationText", sessionDurationText);
            
            var averageSignalText = GameObject.Find("AverageSignalText")?.GetComponent<Text>();
            if (averageSignalText) SetPrivateField(debugUI, "averageSignalText", averageSignalText);
            
            // 設定フィールド（SerializedField）
            SetPrivateField(debugUI, "autoScrollHistory", true);
            SetPrivateField(debugUI, "updateInterval", 0.1f);
            SetPrivateField(debugUI, "maxHistoryDisplay", 20);
            
            // 色設定
            SetPrivateField(debugUI, "activeColor", Color.green);
            SetPrivateField(debugUI, "inactiveColor", Color.gray);
            SetPrivateField(debugUI, "warningColor", Color.yellow);
            SetPrivateField(debugUI, "errorColor", Color.red);
            
            UnityEngine.Debug.Log($"[LTC Debug Setup] Connected {CountConnectedReferences(debugUI)} references to LTCEventDebugUI");
        }
        
        private static GameObject CreateTimecodeEventPrefab()
        {
            GameObject prefab = new GameObject("TimecodeEventEntry");
            
            RectTransform prefabRect = prefab.AddComponent<RectTransform>();
            prefabRect.sizeDelta = new Vector2(0, 25);
            
            Image prefabBg = prefab.AddComponent<Image>();
            prefabBg.color = new Color(0.15f, 0.15f, 0.15f, 0.5f);
            
            HorizontalLayoutGroup prefabLayout = prefab.AddComponent<HorizontalLayoutGroup>();
            prefabLayout.padding = new RectOffset(5, 5, 2, 2);
            prefabLayout.spacing = 5;
            prefabLayout.childControlWidth = false;
            prefabLayout.childControlHeight = true;
            
            // インジケーター
            GameObject indicator = new GameObject("Indicator");
            indicator.transform.SetParent(prefab.transform);
            Image indicatorImage = indicator.AddComponent<Image>();
            indicatorImage.color = Color.gray;
            RectTransform indicatorRect = indicator.GetComponent<RectTransform>();
            indicatorRect.sizeDelta = new Vector2(10, 10);
            
            // 名前
            GameObject nameObj = new GameObject("Name");
            nameObj.transform.SetParent(prefab.transform);
            Text nameText = nameObj.AddComponent<Text>();
            nameText.text = "Event Name";
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 11;
            nameText.color = Color.white;
            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.sizeDelta = new Vector2(100, 0);
            
            // タイムコード
            GameObject tcObj = new GameObject("Timecode");
            tcObj.transform.SetParent(prefab.transform);
            Text tcText = tcObj.AddComponent<Text>();
            tcText.text = "00:00:00:00";
            tcText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tcText.fontSize = 10;
            tcText.color = new Color(0.7f, 0.7f, 0.7f);
            RectTransform tcRect = tcObj.GetComponent<RectTransform>();
            tcRect.sizeDelta = new Vector2(70, 0);
            
            // カウント
            GameObject countObj = new GameObject("Count");
            countObj.transform.SetParent(prefab.transform);
            Text countText = countObj.AddComponent<Text>();
            countText.text = "0";
            countText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            countText.fontSize = 10;
            countText.color = Color.white;
            RectTransform countRect = countObj.GetComponent<RectTransform>();
            countRect.sizeDelta = new Vector2(30, 0);
            
            // リセットボタン
            GameObject resetBtn = new GameObject("ResetButton");
            resetBtn.transform.SetParent(prefab.transform);
            Button button = resetBtn.AddComponent<Button>();
            Image btnImage = resetBtn.AddComponent<Image>();
            btnImage.color = new Color(0.3f, 0.3f, 0.3f);
            RectTransform btnRect = resetBtn.GetComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(40, 18);
            
            GameObject btnText = new GameObject("Text");
            btnText.transform.SetParent(resetBtn.transform);
            Text text = btnText.AddComponent<Text>();
            text.text = "Reset";
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 9;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            RectTransform textRect = btnText.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;
            
            return prefab;
        }
        
        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null)
            {
                field.SetValue(obj, value);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[LTC Debug Setup] Could not find field '{fieldName}'");
            }
        }
        
        private static int CountConnectedReferences(LTCEventDebugUI debugUI)
        {
            int count = 0;
            var fields = debugUI.GetType().GetFields(
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            foreach (var field in fields)
            {
                if (field.FieldType.IsSubclassOf(typeof(Component)) || 
                    field.FieldType == typeof(GameObject) ||
                    field.FieldType == typeof(Transform))
                {
                    var value = field.GetValue(debugUI);
                    if (value != null) count++;
                }
            }
            
            return count;
        }
        
        // 旧メニューアイテムは非推奨として残す
        [MenuItem("GameObject/LTC Debug/Create Simple Debug UI (Legacy)", false, 20)]
        public static void CreateDebugUI(LTCEventDebugger debugger = null)
        {
            UnityEngine.Debug.LogWarning("This is a legacy method. Please use 'Create Debug Setup (Complete)' instead.");
            CreateCompleteDebugSetup();
        }
    }
}