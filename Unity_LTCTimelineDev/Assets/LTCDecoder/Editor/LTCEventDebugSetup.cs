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
        [MenuItem("GameObject/LTC Debug/Create Debug Setup", false, 10)]
        public static void CreateDebugSetup()
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
            
            // UIキャンバスを作成
            CreateDebugUI(debugger);
            
            // 選択
            Selection.activeGameObject = ltcObject;
            
            UnityEngine.Debug.Log("LTC Event Debug Setup created successfully!");
            UnityEngine.Debug.Log("1. Add your audio input device in the LTCDecoder component");
            UnityEngine.Debug.Log("2. Press Play to start debugging");
            UnityEngine.Debug.Log("3. Use the Inspector to manually trigger events");
        }
        
        [MenuItem("GameObject/LTC Debug/Create Simple Debug UI", false, 11)]
        public static void CreateDebugUI(LTCEventDebugger debugger = null)
        {
            // Canvas を作成
            GameObject canvasObject = GameObject.Find("Debug Canvas");
            if (canvasObject == null)
            {
                canvasObject = new GameObject("Debug Canvas");
                Canvas canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.AddComponent<CanvasScaler>();
                canvasObject.AddComponent<GraphicRaycaster>();
            }
            
            // EventSystemを作成（必要な場合）
            if (GameObject.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
            
            // デバッグパネルを作成
            GameObject debugPanel = new GameObject("LTC Debug Panel");
            debugPanel.transform.SetParent(canvasObject.transform);
            RectTransform panelRect = debugPanel.AddComponent<RectTransform>();
            
            // パネルの位置とサイズ設定
            panelRect.anchorMin = new Vector2(0, 0.5f);
            panelRect.anchorMax = new Vector2(0, 0.5f);
            panelRect.pivot = new Vector2(0, 0.5f);
            panelRect.anchoredPosition = new Vector2(20, 0);
            panelRect.sizeDelta = new Vector2(400, 600);
            
            // 背景画像
            Image bgImage = debugPanel.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.8f);
            
            // 垂直レイアウトグループ
            VerticalLayoutGroup vlg = debugPanel.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.spacing = 5;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            
            // タイトル
            CreateTextElement(debugPanel.transform, "LTC Event Debugger", 18, FontStyle.Bold);
            
            // セパレーター
            CreateSeparator(debugPanel.transform);
            
            // ステータス表示エリア
            CreateTextElement(debugPanel.transform, "Status: Waiting...", 14, FontStyle.Normal, "StatusText");
            CreateTextElement(debugPanel.transform, "TC: 00:00:00:00", 16, FontStyle.Bold, "TimecodeText");
            CreateTextElement(debugPanel.transform, "Signal: 0%", 14, FontStyle.Normal, "SignalText");
            
            CreateSeparator(debugPanel.transform);
            
            // イベントインジケーター
            CreateTextElement(debugPanel.transform, "Events:", 14, FontStyle.Bold);
            CreateTextElement(debugPanel.transform, "● LTC Started: 0", 12, FontStyle.Normal, "StartedIndicator");
            CreateTextElement(debugPanel.transform, "● LTC Stopped: 0", 12, FontStyle.Normal, "StoppedIndicator");
            CreateTextElement(debugPanel.transform, "● Receiving: No", 12, FontStyle.Normal, "ReceivingIndicator");
            CreateTextElement(debugPanel.transform, "● No Signal: No", 12, FontStyle.Normal, "NoSignalIndicator");
            
            CreateSeparator(debugPanel.transform);
            
            // イベント履歴ヘッダー
            CreateTextElement(debugPanel.transform, "Event History:", 14, FontStyle.Bold);
            
            // イベント履歴スクロールビュー
            GameObject scrollView = CreateScrollView(debugPanel.transform, "HistoryScrollView");
            
            // デバッガーとUIを接続（もしデバッガーが提供されていれば）
            if (debugger != null)
            {
                LTCEventDebugUI debugUI = debugPanel.AddComponent<LTCEventDebugUI>();
                
                // UI要素の参照を設定
                var texts = debugPanel.GetComponentsInChildren<Text>();
                foreach (var text in texts)
                {
                    switch (text.gameObject.name)
                    {
                        case "StatusText":
                            SetPrivateField(debugUI, "statusText", text);
                            break;
                        case "TimecodeText":
                            SetPrivateField(debugUI, "currentTimecodeText", text);
                            break;
                        case "SignalText":
                            SetPrivateField(debugUI, "signalLevelText", text);
                            break;
                    }
                }
                
                // デバッガー参照を設定
                SetPrivateField(debugUI, "debugger", debugger);
                SetPrivateField(debugUI, "historyContent", scrollView.transform.Find("Viewport/Content"));
            }
            
            UnityEngine.Debug.Log("Debug UI created successfully!");
        }
        
        private static GameObject CreateTextElement(Transform parent, string text, int fontSize, FontStyle fontStyle, string name = null)
        {
            GameObject textObject = new GameObject(name ?? "Text");
            textObject.transform.SetParent(parent);
            
            Text textComponent = textObject.AddComponent<Text>();
            textComponent.text = text;
            textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComponent.fontSize = fontSize;
            textComponent.fontStyle = fontStyle;
            textComponent.color = Color.white;
            
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, fontSize + 10);
            
            return textObject;
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
        
        private static GameObject CreateScrollView(Transform parent, string name)
        {
            GameObject scrollView = new GameObject(name);
            scrollView.transform.SetParent(parent);
            
            ScrollRect scrollRect = scrollView.AddComponent<ScrollRect>();
            Image scrollBg = scrollView.AddComponent<Image>();
            scrollBg.color = new Color(0, 0, 0, 0.3f);
            
            RectTransform scrollRectTransform = scrollView.GetComponent<RectTransform>();
            scrollRectTransform.sizeDelta = new Vector2(0, 200);
            
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
            contentLayout.childForceExpandHeight = false;
            
            ContentSizeFitter contentFitter = content.AddComponent<ContentSizeFitter>();
            contentFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            
            // ScrollRectの設定
            scrollRect.content = contentRect;
            scrollRect.viewport = viewportRect;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 20;
            
            return scrollView;
        }
        
        private static void SetPrivateField(object obj, string fieldName, object value)
        {
            var field = obj.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(obj, value);
        }
    }
}