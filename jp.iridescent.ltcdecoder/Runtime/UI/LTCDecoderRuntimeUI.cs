using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

namespace jp.iridescent.ltcdecoder
{
    /// <summary>
    /// LTCDecoderのランタイムUI
    /// Play中にLTC設定を変更するためのUI
    /// </summary>
    [AddComponentMenu("Audio/LTC Decoder Runtime UI")]
    [RequireComponent(typeof(Canvas))]
    public class LTCDecoderRuntimeUI : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private LTCDecoder ltcDecoder;
        
        [Header("UI Elements")]
        [SerializeField] private Dropdown deviceDropdown;
        [SerializeField] private Dropdown frameRateDropdown;
        [SerializeField] private Dropdown sampleRateDropdown;
        [SerializeField] private Text statusText;
        [SerializeField] private Text timecodeText;
        [SerializeField] private Text signalLevelText;
        [SerializeField] private Toggle dropFrameToggle;
        
        [Header("UI Settings")]
        [SerializeField] private bool autoHideWhenNoSignal = false;
        [SerializeField] private float updateInterval = 0.1f;
        
        private float lastUpdateTime;
        private readonly int[] sampleRates = { 44100, 48000, 96000 };
        
        private void Start()
        {
            if (ltcDecoder == null)
            {
                ltcDecoder = FindObjectOfType<LTCDecoder>();
                if (ltcDecoder == null)
                {
                    Debug.LogError("LTCDecoder not found!");
                    enabled = false;
                    return;
                }
            }
            
            InitializeUI();
        }
        
        private void InitializeUI()
        {
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
            
            // ドロップフレームトグルの初期化（現在は使用していない）
            // if (dropFrameToggle != null)
            // {
            //     dropFrameToggle.isOn = ltcDecoder.DropFrame;
            //     dropFrameToggle.onValueChanged.AddListener(OnDropFrameChanged);
            // }
        }
        
        private void SetupFrameRateDropdown()
        {
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
        
        private LTCDecoder.LTCFrameRate GetFrameRateFromIndex(int index)
        {
            switch (index)
            {
                case 0: return LTCDecoder.LTCFrameRate.FPS_24;
                case 1: return LTCDecoder.LTCFrameRate.FPS_25;
                case 2: return LTCDecoder.LTCFrameRate.FPS_29_97_DF;
                case 3: return LTCDecoder.LTCFrameRate.FPS_29_97_NDF;
                case 4: return LTCDecoder.LTCFrameRate.FPS_30;
                default: return LTCDecoder.LTCFrameRate.FPS_30;
            }
        }
        
        private void SetupSampleRateDropdown()
        {
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
            int index = System.Array.IndexOf(sampleRates, currentRate);
            if (index == -1) index = 1; // デフォルト48000
            sampleRateDropdown.value = index;
        }
        
        private void RefreshDeviceList()
        {
            if (deviceDropdown == null) return;
            
            deviceDropdown.ClearOptions();
            
            string[] devices = ltcDecoder.AvailableDevices;
            if (devices.Length == 0)
            {
                deviceDropdown.AddOptions(new List<string> { "No devices found" });
                return;
            }
            
            deviceDropdown.AddOptions(devices.ToList());
            
            // 現在選択されているデバイスを選択
            string currentDevice = ltcDecoder.SelectedDevice;
            int index = System.Array.IndexOf(devices, currentDevice);
            if (index >= 0)
            {
                deviceDropdown.value = index;
            }
        }
        
        private void Update()
        {
            if (Time.time - lastUpdateTime < updateInterval) return;
            lastUpdateTime = Time.time;
            
            UpdateStatusDisplay();
        }
        
        private void UpdateStatusDisplay()
        {
            if (ltcDecoder == null) return;
            
            // ステータステキスト更新
            if (statusText != null)
            {
                string status = GetStatusText(ltcDecoder.State);
                statusText.text = $"Status: {status}";
                statusText.color = GetStatusColor(ltcDecoder.State);
            }
            
            // タイムコード表示更新
            if (timecodeText != null)
            {
                timecodeText.text = $"TC: {ltcDecoder.CurrentTimecode}";
            }
            
            // シグナルレベル表示更新
            if (signalLevelText != null)
            {
                float level = ltcDecoder.SignalLevel * 100f;
                signalLevelText.text = $"Signal: {level:F1}%";
            }
            
            // 自動非表示
            if (autoHideWhenNoSignal)
            {
                bool shouldShow = ltcDecoder.HasSignal || ltcDecoder.State != LTCDecoder.SyncState.NoSignal;
                gameObject.SetActive(shouldShow);
            }
        }
        
        private string GetStatusText(LTCDecoder.SyncState state)
        {
            switch (state)
            {
                case LTCDecoder.SyncState.NoSignal: return "No Signal";
                case LTCDecoder.SyncState.Syncing: return "Syncing...";
                case LTCDecoder.SyncState.Locked: return "Locked";
                case LTCDecoder.SyncState.Drifting: return "Drifting";
                default: return "Unknown";
            }
        }
        
        private Color GetStatusColor(LTCDecoder.SyncState state)
        {
            switch (state)
            {
                case LTCDecoder.SyncState.NoSignal: return Color.gray;
                case LTCDecoder.SyncState.Syncing: return Color.yellow;
                case LTCDecoder.SyncState.Locked: return Color.green;
                case LTCDecoder.SyncState.Drifting: return new Color(1f, 0.5f, 0f); // Orange
                default: return Color.white;
            }
        }
        
        #region UI Callbacks
        
        private void OnDeviceChanged(int index)
        {
            if (deviceDropdown == null || ltcDecoder == null) return;
            
            string[] devices = ltcDecoder.AvailableDevices;
            if (index >= 0 && index < devices.Length)
            {
                ltcDecoder.SetDevice(devices[index]);
            }
        }
        
        private void OnFrameRateChanged(int index)
        {
            if (frameRateDropdown == null || ltcDecoder == null) return;
            
            LTCDecoder.LTCFrameRate frameRate = GetFrameRateFromIndex(index);
            ltcDecoder.SetLTCFrameRate(frameRate);
            
            // ドロップフレームトグルも更新
            if (dropFrameToggle != null)
            {
                dropFrameToggle.isOn = (frameRate == LTCDecoder.LTCFrameRate.FPS_29_97_DF);
            }
        }
        
        private void OnSampleRateChanged(int index)
        {
            if (sampleRateDropdown == null || ltcDecoder == null) return;
            
            if (index >= 0 && index < sampleRates.Length)
            {
                ltcDecoder.SetSampleRate(sampleRates[index]);
            }
        }
        
        // ドロップフレーム変更ハンドラー（現在は使用していない）
        // private void OnDropFrameChanged(bool value)
        // {
        //     if (ltcDecoder == null) return;
        //     
        //     // 29.97fpsの場合のみドロップフレーム設定を反映
        //     var currentRate = ltcDecoder.FrameRate;
        //     if (currentRate == LTCDecoder.LTCFrameRate.FPS_29_97_DF || 
        //         currentRate == LTCDecoder.LTCFrameRate.FPS_29_97_NDF)
        //     {
        //         var newRate = value ? 
        //             LTCDecoder.LTCFrameRate.FPS_29_97_DF : 
        //             LTCDecoder.LTCFrameRate.FPS_29_97_NDF;
        //         ltcDecoder.SetLTCFrameRate(newRate);
        //         
        //         // ドロップダウンも更新
        //         if (frameRateDropdown != null)
        //         {
        //             frameRateDropdown.value = GetFrameRateIndex(newRate);
        //         }
        //     }
        // }
        
        #endregion
        
        #region Public Methods
        
        public void RefreshDevices()
        {
            ltcDecoder?.RefreshDevices();
            RefreshDeviceList();
        }
        
        public void ShowUI()
        {
            gameObject.SetActive(true);
        }
        
        public void HideUI()
        {
            gameObject.SetActive(false);
        }
        
        #endregion
    }
}