using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Playables;
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
        public Text currentTimecodeText;  // タイムコード表示用（1段目）
        public Text statusLineText;  // ステータスライン表示用（2段目）
        public Text statusText;  // 互換性のため残すが使用しない
        public Text signalLevelText;  // 互換性のため残すが使用しない
        public Image signalLevelBar;  // 互換性のため残すが使用しない
        public Transform debugMessageContainer;
        public ScrollRect debugScrollRect;
        
        [Header("Timeline Sync UI References")]
        public GameObject timelineSyncSection;  // Timeline Syncセクション全体
        public Text timelineSyncLTCText;  // LTC TC表示
        public Text timelineSyncTimelineText;  // Timeline TC表示
        public Text timelineSyncDiffText;  // 時間差表示
        public Text timelineSyncStatusText;  // 同期ステータス表示
        public Text timelineSyncThresholdText;  // 閾値/オフセット表示
        
        // Sync Settings UI要素
        public InputField syncThresholdInput;    // Sync Threshold入力
        public InputField observationTimeInput;  // Observation Time入力
        public InputField timelineOffsetInput;   // Timeline Offset入力
        public Toggle enableSyncToggle;          // Enable Syncトグル
        public Toggle snapToFpsToggle;           // Snap to FPSトグル
        
        [Header("Audio Settings UI")]
        public Dropdown deviceDropdown;
        public Dropdown frameRateDropdown;
        public Dropdown sampleRateDropdown;
        
        [Header("Target Components")]
        [SerializeField] private LTCDecoder ltcDecoder;
        [SerializeField] private LTCEventDebugger ltcEventDebugger;
        [SerializeField] private LTCTimelineSync ltcTimelineSync;  // オプショナル
        
        [Header("Debug Message Settings")]
        [SerializeField] private int maxDebugMessages = 100;
        
        private Queue<GameObject> debugMessagePool = new Queue<GameObject>();
        public List<GameObject> activeDebugMessages = new List<GameObject>();
        
        // UI同期制御用フラグ
        private bool isUpdatingFromCode = false;
        
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
            
            // Sync Settings UIの初期化
            InitializeSyncSettingsUI();
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
                // RefreshDeviceList内でリスナーが設定されるため、ここでは設定しない
            }
            
            // フレームレートドロップダウンの初期化
            if (frameRateDropdown != null)
            {
                SetupFrameRateDropdown();
                // SetupFrameRateDropdown内でリスナーが設定されるため、ここでは設定しない
            }
            
            // サンプルレートドロップダウンの初期化
            if (sampleRateDropdown != null)
            {
                SetupSampleRateDropdown();
                // SetupSampleRateDropdown内でリスナーが設定されるため、ここでは設定しない
            }
        }
        
        private void RefreshDeviceList()
        {
            if (deviceDropdown == null || ltcDecoder == null) return;
            
            // 一時的にリスナーを無効化（初期化時の不要なイベント発火を防ぐ）
            deviceDropdown.onValueChanged.RemoveListener(OnDeviceChanged);
            
            deviceDropdown.ClearOptions();
            
            string[] devices = ltcDecoder.AvailableDevices;
            if (devices.Length == 0)
            {
                deviceDropdown.AddOptions(new List<string> { "No devices found" });
                deviceDropdown.onValueChanged.AddListener(OnDeviceChanged);
                return;
            }
            
            deviceDropdown.AddOptions(new List<string>(devices));
            
            // 現在選択されているデバイスを選択
            string currentDevice = ltcDecoder.SelectedDevice;
            int index = System.Array.IndexOf(devices, currentDevice);
            if (index >= 0)
            {
                #if UNITY_2019_1_OR_NEWER
                deviceDropdown.SetValueWithoutNotify(index);
                #else
                deviceDropdown.value = index;
                #endif
            }
            else if (devices.Length > 0)
            {
                // 保存されたデバイスが見つからない場合は最初のデバイスを選択
                #if UNITY_2019_1_OR_NEWER
                deviceDropdown.SetValueWithoutNotify(0);
                #else
                deviceDropdown.value = 0;
                #endif
            }
            
            // リスナーを再度有効化
            deviceDropdown.onValueChanged.AddListener(OnDeviceChanged);
        }
        
        private void SetupFrameRateDropdown()
        {
            if (frameRateDropdown == null) return;
            
            // 一時的にリスナーを無効化（初期化時の不要なイベント発火を防ぐ）
            frameRateDropdown.onValueChanged.RemoveListener(OnFrameRateChanged);
            
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
            
            #if UNITY_2019_1_OR_NEWER
            frameRateDropdown.SetValueWithoutNotify(index);
            #else
            frameRateDropdown.value = index;
            #endif
            
            // リスナーを再度有効化
            frameRateDropdown.onValueChanged.AddListener(OnFrameRateChanged);
        }
        
        private void SetupSampleRateDropdown()
        {
            if (sampleRateDropdown == null) return;
            
            // 一時的にリスナーを無効化（初期化時の不要なイベント発火を防ぐ）
            sampleRateDropdown.onValueChanged.RemoveListener(OnSampleRateChanged);
            
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
            
            #if UNITY_2019_1_OR_NEWER
            sampleRateDropdown.SetValueWithoutNotify(index);
            #else
            sampleRateDropdown.value = index;
            #endif
            
            // リスナーを再度有効化
            sampleRateDropdown.onValueChanged.AddListener(OnSampleRateChanged);
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
            if (isUpdatingFromCode) return;  // コードからの更新時は無視
            
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
            if (isUpdatingFromCode) return;  // コードからの更新時は無視
            
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
            if (isUpdatingFromCode) return;  // コードからの更新時は無視
            
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
            // 1フレーム遅延させて初期化（確実な同期のため）
            StartCoroutine(InitializeUIDelayed());
        }
        
        /// <summary>
        /// UIの遅延初期化
        /// </summary>
        private System.Collections.IEnumerator InitializeUIDelayed()
        {
            yield return null;  // 1フレーム待機
            
            // Startで自動的にSetupを試みる（コンポーネントが設定されている場合）
            if (ltcDecoder != null && ltcEventDebugger != null)
            {
                Setup(ltcDecoder, ltcEventDebugger);
            }
            else if (ltcDecoder != null)
            {
                // デバッガーがなくてもUIの設定は初期化
                InitializeAudioSettingsUI();
            }
            
            // UIをデコーダーの現在値で同期
            if (ltcDecoder != null)
            {
                SyncUIWithDecoder();
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
            
            // Timeline Sync情報更新（Timeline Syncがある場合のみ）
            UpdateTimelineSyncDisplay();
        }
        
        /// <summary>
        /// タイムコード表示を更新（2段表示対応）
        /// </summary>
        private void UpdateTimecodeDisplay()
        {
            if (ltcDecoder == null) return;
            
            // タイムコード取得
            string timecode = ltcDecoder.CurrentTimecode;
            if (string.IsNullOrEmpty(timecode))
            {
                timecode = "00:00:00:00";
            }
            
            // 1段目：タイムコードのみ
            if (currentTimecodeText != null)
            {
                currentTimecodeText.text = timecode;
                
                // 等幅フォント設定（初回のみ）
                if (currentTimecodeText.font == null || !currentTimecodeText.font.name.Contains("Consola"))
                {
                    Font monoFont = Font.CreateDynamicFontFromOSFont("Consolas", currentTimecodeText.fontSize);
                    if (monoFont != null)
                    {
                        currentTimecodeText.font = monoFont;
                    }
                }
            }
            
            // 2段目：ステータスライン（Output TC Status＋シグナルレベル）
            if (statusLineText != null)
            {
                // ステータス取得（Output TC形式）
                bool isRunning = ltcDecoder.IsRunning;
                string status = isRunning ? "Output TC: Running" : "Output TC: Stopped";
                Color statusColor = isRunning ? Color.green : Color.gray;
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
                
                // フォーマット: Output TC: Running ████████░░ 85%
                statusLineText.text = $"<color=#{statusHex}>{status}</color> <color=#{barHex}>{signalBar}</color> {signalPercent:D3}%";
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
        /// Timeline Sync情報を更新
        /// </summary>
        private void UpdateTimelineSyncDisplay()
        {
            // Timeline Syncセクションが未設定の場合は何もしない
            if (timelineSyncSection == null) return;
            
            // LTCTimelineSyncが未設定の場合は自動検索を試みる
            if (ltcTimelineSync == null)
            {
                #if UNITY_2023_1_OR_NEWER
                ltcTimelineSync = FindFirstObjectByType<LTCTimelineSync>();
                #else
                ltcTimelineSync = FindObjectOfType<LTCTimelineSync>();
                #endif
            }
            
            // LTCTimelineSyncがない場合はセクションを非表示
            if (ltcTimelineSync == null)
            {
                if (timelineSyncSection.activeSelf)
                {
                    timelineSyncSection.SetActive(false);
                }
                return;
            }
            
            // セクションを表示
            if (!timelineSyncSection.activeSelf)
            {
                timelineSyncSection.SetActive(true);
            }
            
            // PlayableDirectorの取得
            var playableDirector = ltcTimelineSync.GetPlayableDirector();
            if (playableDirector == null) return;
            
            // LTC TC表示
            if (timelineSyncLTCText != null)
            {
                string ltcTC = ltcDecoder != null ? ltcDecoder.CurrentTimecode : "--:--:--:--";
                timelineSyncLTCText.text = $"LTC:      {ltcTC}";
                SetMonoFontIfNeeded(timelineSyncLTCText);
            }
            
            // Timeline TC表示
            if (timelineSyncTimelineText != null)
            {
                float timelineTime = (float)playableDirector.time;
                string timelineTC = ConvertToTimecode(timelineTime, GetFrameRate());
                timelineSyncTimelineText.text = $"Timeline: {timelineTC}";
                SetMonoFontIfNeeded(timelineSyncTimelineText);
            }
            
            // 時間差と同期ステータス表示
            if (timelineSyncDiffText != null)
            {
                float timeDiff = ltcTimelineSync.TimeDifference;
                string syncStatus = GetSyncStatus(ltcTimelineSync, ltcDecoder);
                Color statusColor = GetSyncStatusColor(syncStatus);
                string hexColor = ColorUtility.ToHtmlStringRGB(statusColor);
                
                timelineSyncDiffText.text = $"Diff: {timeDiff:F3}s <color=#{hexColor}>[{syncStatus}]</color>";
            }
            
            // Play状態表示
            if (timelineSyncStatusText != null)
            {
                string playState = GetPlayStateString(playableDirector.state);
                Color playColor = playableDirector.state == PlayState.Playing ? Color.green : 
                                 playableDirector.state == PlayState.Paused ? Color.yellow : Color.gray;
                string hexColor = ColorUtility.ToHtmlStringRGB(playColor);
                
                timelineSyncStatusText.text = $"Status: <color=#{hexColor}>{playState}</color>";
            }
            
            // 閾値とオフセット表示
            if (timelineSyncThresholdText != null)
            {
                float threshold = ltcTimelineSync.SyncThreshold;
                float offset = ltcTimelineSync.GetTimelineOffset();
                timelineSyncThresholdText.text = $"Threshold: {threshold:F2}s | Offset: {offset:F2}s";
            }
            
            // Sync Settings UIの更新
            UpdateSyncSettingsUI();
        }
        
        /// <summary>
        /// 秒数をタイムコード形式に変換
        /// </summary>
        private string ConvertToTimecode(float seconds, float frameRate)
        {
            if (seconds < 0) return "--:--:--:--";
            
            int totalFrames = Mathf.FloorToInt(seconds * frameRate);
            int frames = totalFrames % Mathf.RoundToInt(frameRate);
            int totalSeconds = totalFrames / Mathf.RoundToInt(frameRate);
            int secs = totalSeconds % 60;
            int mins = (totalSeconds / 60) % 60;
            int hours = totalSeconds / 3600;
            
            return $"{hours:D2}:{mins:D2}:{secs:D2}:{frames:D2}";
        }
        
        /// <summary>
        /// フレームレートを取得
        /// </summary>
        private float GetFrameRate()
        {
            if (ltcDecoder == null) return 30f;
            
            switch (ltcDecoder.FrameRate)
            {
                case LTCDecoder.LTCFrameRate.FPS_24:
                    return 24f;
                case LTCDecoder.LTCFrameRate.FPS_25:
                    return 25f;
                case LTCDecoder.LTCFrameRate.FPS_29_97_DF:
                case LTCDecoder.LTCFrameRate.FPS_29_97_NDF:
                    return 29.97f;
                case LTCDecoder.LTCFrameRate.FPS_30:
                default:
                    return 30f;
            }
        }
        
        /// <summary>
        /// 同期ステータスを取得
        /// </summary>
        private string GetSyncStatus(LTCTimelineSync timelineSync, LTCDecoder decoder)
        {
            if (!timelineSync.IsPlaying)
                return "Stopped";
            
            if (decoder == null)
                return "No Decoder";
            
            switch (decoder.State)
            {
                case LTCDecoder.SyncState.NoSignal:
                    return "No Signal";
                case LTCDecoder.SyncState.Syncing:
                    return "Syncing";
                case LTCDecoder.SyncState.Locked:
                    return "Locked";
                case LTCDecoder.SyncState.Drifting:
                    return "Drifting";
                default:
                    return "Unknown";
            }
        }
        
        /// <summary>
        /// 同期ステータスの色を取得
        /// </summary>
        private Color GetSyncStatusColor(string status)
        {
            switch (status)
            {
                case "Locked":
                    return Color.green;
                case "Syncing":
                    return Color.yellow;
                case "Drifting":
                    return new Color(1f, 0.5f, 0f); // オレンジ
                case "No Signal":
                case "Stopped":
                    return Color.gray;
                default:
                    return Color.red;
            }
        }
        
        /// <summary>
        /// PlayState文字列を取得
        /// </summary>
        private string GetPlayStateString(PlayState state)
        {
            switch (state)
            {
                case PlayState.Playing:
                    return "Playing";
                case PlayState.Paused:
                    return "Paused";
                default:
                    return "Stopped";
            }
        }
        
        /// <summary>
        /// 等幅フォントを設定（必要な場合のみ）
        /// </summary>
        private void SetMonoFontIfNeeded(Text text)
        {
            if (text != null && (text.font == null || !text.font.name.Contains("Consola")))
            {
                Font monoFont = Font.CreateDynamicFontFromOSFont("Consolas", text.fontSize);
                if (monoFont != null)
                {
                    text.font = monoFont;
                }
            }
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
        /// UIをデコーダーの現在値で同期
        /// </summary>
        private void SyncUIWithDecoder()
        {
            if (ltcDecoder == null) return;
            
            isUpdatingFromCode = true;  // フラグを立てる
            
            // デバイスドロップダウンの同期
            if (deviceDropdown != null)
            {
                RefreshDeviceList();
            }
            
            // フレームレートドロップダウンの同期
            if (frameRateDropdown != null)
            {
                int index = GetFrameRateIndex(ltcDecoder.FrameRate);
                #if UNITY_2019_1_OR_NEWER
                frameRateDropdown.SetValueWithoutNotify(index);
                #else
                frameRateDropdown.value = index;
                #endif
            }
            
            // サンプルレートドロップダウンの同期
            if (sampleRateDropdown != null)
            {
                int index = GetSampleRateIndex(ltcDecoder.SampleRate);
                #if UNITY_2019_1_OR_NEWER
                sampleRateDropdown.SetValueWithoutNotify(index);
                #else
                sampleRateDropdown.value = index;
                #endif
            }
            
            isUpdatingFromCode = false;  // フラグを下ろす
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
        
        #if UNITY_EDITOR
        /// <summary>
        /// Editor PlayMode終了後のUI更新（Editor限定）
        /// </summary>
        [UnityEditor.InitializeOnLoadMethod]
        static void SetupEditorUIUpdate()
        {
            UnityEditor.EditorApplication.playModeStateChanged += (state) =>
            {
                if (state == UnityEditor.PlayModeStateChange.EnteredEditMode)
                {
                    // EditMode復帰後にUIを更新
                    UnityEditor.EditorApplication.delayCall += () =>
                    {
                        // シーン内のLTCUIControllerを探してUIを更新
                        foreach (var uiController in FindObjectsOfType<LTCUIController>())
                        {
                            if (uiController != null && uiController.ltcDecoder != null)
                            {
                                uiController.UpdateUIAfterPlayMode();
                            }
                        }
                    };
                }
            };
        }
        
        /// <summary>
        /// PlayMode終了後のUI更新
        /// </summary>
        private void UpdateUIAfterPlayMode()
        {
            // LTCDecoderの現在値でUIを同期
            if (ltcDecoder != null)
            {
                // ドロップダウンを更新
                if (deviceDropdown != null)
                {
                    RefreshDeviceList();
                }
                
                if (frameRateDropdown != null)
                {
                    int index = GetFrameRateIndex(ltcDecoder.FrameRate);
                    if (index >= 0 && index < frameRateDropdown.options.Count)
                    {
                        frameRateDropdown.value = index;
                    }
                }
                
                if (sampleRateDropdown != null)
                {
                    int index = GetSampleRateIndex(ltcDecoder.SampleRate);
                    if (index >= 0 && index < sampleRateDropdown.options.Count)
                    {
                        sampleRateDropdown.value = index;
                    }
                }
                
                Debug.Log("[LTCUIController] UI updated after returning to Edit Mode");
            }
        }
        #endif
        
        /// <summary>
        /// Sync Settings UIの初期化
        /// </summary>
        private void InitializeSyncSettingsUI()
        {
            if (ltcTimelineSync == null)
            {
                #if UNITY_2023_1_OR_NEWER
                ltcTimelineSync = FindFirstObjectByType<LTCTimelineSync>();
                #else
                ltcTimelineSync = FindObjectOfType<LTCTimelineSync>();
                #endif
            }
            
            if (ltcTimelineSync == null) return;
            
            // Sync Threshold入力フィールドの初期化
            if (syncThresholdInput != null)
            {
                syncThresholdInput.text = ltcTimelineSync.SyncThreshold.ToString("F3");
                syncThresholdInput.onEndEdit.RemoveListener(OnSyncThresholdChanged);
                syncThresholdInput.onEndEdit.AddListener(OnSyncThresholdChanged);
            }
            
            // Observation Time入力フィールドの初期化
            if (observationTimeInput != null)
            {
                observationTimeInput.text = ltcTimelineSync.ContinuousObservationTime.ToString("F2");
                observationTimeInput.onEndEdit.RemoveListener(OnObservationTimeChanged);
                observationTimeInput.onEndEdit.AddListener(OnObservationTimeChanged);
            }
            
            // Timeline Offset入力フィールドの初期化
            if (timelineOffsetInput != null)
            {
                float offset = ltcTimelineSync.GetTimelineOffset();
                timelineOffsetInput.text = offset.ToString("F2");
                timelineOffsetInput.onEndEdit.RemoveListener(OnTimelineOffsetChanged);
                timelineOffsetInput.onEndEdit.AddListener(OnTimelineOffsetChanged);
            }
            
            // Enable Syncトグルの初期化
            if (enableSyncToggle != null)
            {
                enableSyncToggle.isOn = ltcTimelineSync.enabled;
                enableSyncToggle.onValueChanged.RemoveListener(OnEnableSyncChanged);
                enableSyncToggle.onValueChanged.AddListener(OnEnableSyncChanged);
            }
            
            // Snap to FPSトグルの初期化
            if (snapToFpsToggle != null)
            {
                snapToFpsToggle.isOn = ltcTimelineSync.SnapToTimelineFps;
                snapToFpsToggle.onValueChanged.RemoveListener(OnSnapToFpsChanged);
                snapToFpsToggle.onValueChanged.AddListener(OnSnapToFpsChanged);
            }
        }
        
        /// <summary>
        /// Sync Settings UIの更新
        /// </summary>
        private void UpdateSyncSettingsUI()
        {
            if (ltcTimelineSync == null) return;
            
            // コードからの更新中はイベントハンドラを無視
            isUpdatingFromCode = true;
            
            // Sync Threshold入力フィールドの更新
            if (syncThresholdInput != null)
            {
                string currentText = ltcTimelineSync.SyncThreshold.ToString("F3");
                if (syncThresholdInput.text != currentText && !syncThresholdInput.isFocused)
                {
                    syncThresholdInput.text = currentText;
                }
            }
            
            // Observation Time入力フィールドの更新
            if (observationTimeInput != null)
            {
                string currentText = ltcTimelineSync.ContinuousObservationTime.ToString("F2");
                if (observationTimeInput.text != currentText && !observationTimeInput.isFocused)
                {
                    observationTimeInput.text = currentText;
                }
            }
            
            // Timeline Offset入力フィールドの更新
            if (timelineOffsetInput != null)
            {
                float offset = ltcTimelineSync.GetTimelineOffset();
                string currentText = offset.ToString("F2");
                if (timelineOffsetInput.text != currentText && !timelineOffsetInput.isFocused)
                {
                    timelineOffsetInput.text = currentText;
                }
            }
            
            // Enable Syncトグルの更新
            if (enableSyncToggle != null && enableSyncToggle.isOn != ltcTimelineSync.enabled)
            {
                enableSyncToggle.isOn = ltcTimelineSync.enabled;
            }
            
            // Snap to FPSトグルの更新
            if (snapToFpsToggle != null && snapToFpsToggle.isOn != ltcTimelineSync.SnapToTimelineFps)
            {
                snapToFpsToggle.isOn = ltcTimelineSync.SnapToTimelineFps;
            }
            
            isUpdatingFromCode = false;
        }
        
        /// <summary>
        /// Sync Threshold変更時のイベントハンドラ
        /// </summary>
        private void OnSyncThresholdChanged(string value)
        {
            if (isUpdatingFromCode || ltcTimelineSync == null) return;
            
            if (float.TryParse(value, out float threshold))
            {
                // 範囲を0.01〜2.0に制限
                threshold = Mathf.Clamp(threshold, 0.01f, 2.0f);
                ltcTimelineSync.SyncThreshold = threshold;
                syncThresholdInput.text = threshold.ToString("F3");
                
                Debug.Log($"[LTCUIController] Sync Threshold changed to: {threshold:F3}s");
            }
            else
            {
                // 無効な入力の場合、現在の値に戻す
                syncThresholdInput.text = ltcTimelineSync.SyncThreshold.ToString("F3");
            }
        }
        
        /// <summary>
        /// Observation Time変更時のイベントハンドラ
        /// </summary>
        private void OnObservationTimeChanged(string value)
        {
            if (isUpdatingFromCode || ltcTimelineSync == null) return;
            
            if (float.TryParse(value, out float time))
            {
                // 範囲を0.01〜5.0に制限
                time = Mathf.Clamp(time, 0.01f, 5.0f);
                ltcTimelineSync.ContinuousObservationTime = time;
                observationTimeInput.text = time.ToString("F2");
                
                Debug.Log($"[LTCUIController] Continuous Observation Time changed to: {time:F2}s");
            }
            else
            {
                // 無効な入力の場合、現在の値に戻す
                observationTimeInput.text = ltcTimelineSync.ContinuousObservationTime.ToString("F2");
            }
        }
        
        /// <summary>
        /// Timeline Offset変更時のイベントハンドラ
        /// </summary>
        private void OnTimelineOffsetChanged(string value)
        {
            if (isUpdatingFromCode || ltcTimelineSync == null) return;
            
            if (float.TryParse(value, out float offset))
            {
                // 範囲を-10.0〜10.0に制限
                offset = Mathf.Clamp(offset, -10.0f, 10.0f);
                ltcTimelineSync.SetTimelineOffset(offset);
                timelineOffsetInput.text = offset.ToString("F2");
                
                Debug.Log($"[LTCUIController] Timeline Offset changed to: {offset:F2}s");
            }
            else
            {
                // 無効な入力の場合、現在の値に戻す
                float currentOffset = ltcTimelineSync.GetTimelineOffset();
                timelineOffsetInput.text = currentOffset.ToString("F2");
            }
        }
        
        /// <summary>
        /// Enable Sync変更時のイベントハンドラ
        /// </summary>
        private void OnEnableSyncChanged(bool enabled)
        {
            if (isUpdatingFromCode || ltcTimelineSync == null) return;
            
            ltcTimelineSync.enabled = enabled;
            Debug.Log($"[LTCUIController] Timeline Sync {(enabled ? "enabled" : "disabled")}");
        }
        
        /// <summary>
        /// Snap to FPS変更時のイベントハンドラ
        /// </summary>
        private void OnSnapToFpsChanged(bool enabled)
        {
            if (isUpdatingFromCode || ltcTimelineSync == null) return;
            
            ltcTimelineSync.SnapToTimelineFps = enabled;
            Debug.Log($"[LTCUIController] Snap to Timeline FPS {(enabled ? "enabled" : "disabled")}");
        }
    }
}