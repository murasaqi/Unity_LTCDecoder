using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace jp.iridescent.ltcdecoder
{
    /// <summary>
    /// LTCデコーダーのイベントデータ（Phase D-14: メタ拡張）
    /// </summary>
    [System.Serializable]
    public class LTCEventData
    {
        public string currentTimecode;      // 現在のOutput TC
        public float timeInSeconds;         // 秒単位の時間
        public bool hasSignal;             // 信号有無
        public float signalLevel;          // 信号レベル（0-1）
        
        // Phase D-14: 拡張メタデータ
        public double dspTimestamp;         // DSP時刻スタンプ
        public long absoluteFrame;          // 絶対フレーム番号
        public bool isDropFrame;            // DropFrameモードかどうか
        public float frameRate;             // 実フレームレート
        
        public LTCEventData()
        {
            currentTimecode = "00:00:00:00";
            timeInSeconds = 0f;
            hasSignal = false;
            signalLevel = 0f;
            dspTimestamp = 0.0;
            absoluteFrame = 0;
            isDropFrame = false;
            frameRate = 30f;
        }
        
        public LTCEventData(string tc, float time, bool signal, float level)
        {
            currentTimecode = tc;
            timeInSeconds = time;
            hasSignal = signal;
            signalLevel = level;
            dspTimestamp = AudioSettings.dspTime;
            absoluteFrame = 0;
            isDropFrame = false;
            frameRate = 30f;
        }
        
        /// <summary>
        /// 拡張コンストラクタ（メタデータ付き）
        /// </summary>
        public LTCEventData(string tc, float time, bool signal, float level, 
                          double dspTime, long frame, bool dropFrame, float fps)
        {
            currentTimecode = tc;
            timeInSeconds = time;
            hasSignal = signal;
            signalLevel = level;
            dspTimestamp = dspTime;
            absoluteFrame = frame;
            isDropFrame = dropFrame;
            frameRate = fps;
        }
    }
    
    /// <summary>
    /// タイムコード指定イベント
    /// </summary>
    [System.Serializable]
    public class TimecodeEvent
    {
        [Header("Event Settings")]
        [Tooltip("Event name (for identification) / イベントの名前（識別用）")]
        public string eventName = "New Event";
        
        [Tooltip("Timecode to trigger (HH:MM:SS:FF) / 発火するタイムコード (HH:MM:SS:FF)")]
        public string targetTimecode = "00:00:00:00";
        
        [Tooltip("Tolerance (number of frames) / 許容誤差（フレーム数）")]
        [Range(0, 5)]
        public int toleranceFrames = 1;
        
        [Header("Trigger Settings")]
        [Tooltip("Fire only once / 一度だけ発火する")]
        public bool oneShot = true;
        
        [Tooltip("Enable event / イベントを有効にする")]
        public bool enabled = true;
        
        [Space(10)]
        [Tooltip("Event executed when this timecode is reached / このタイムコードに到達した時に実行されるイベント")]
        public UnityEvent<LTCEventData> onTimecodeReached;
        
        // 内部状態
        [HideInInspector]
        public bool triggered = false;
        
        /// <summary>
        /// イベントをリセット（再度発火可能にする）
        /// </summary>
        public void Reset()
        {
            triggered = false;
        }
        
        /// <summary>
        /// タイムコードが一致するかチェック
        /// </summary>
        public bool IsMatch(string currentTC, float frameRate = 30f)
        {
            if (!enabled) return false;
            if (oneShot && triggered) return false;
            
            // タイムコードを秒に変換して比較
            float currentSeconds = TimecodeToSeconds(currentTC, frameRate);
            float targetSeconds = TimecodeToSeconds(targetTimecode, frameRate);
            
            if (currentSeconds < 0 || targetSeconds < 0) return false;
            
            // 許容誤差を秒に変換
            float tolerance = toleranceFrames / frameRate;
            
            // 範囲内かチェック
            return Math.Abs(currentSeconds - targetSeconds) <= tolerance;
        }
        
        /// <summary>
        /// タイムコード文字列を秒に変換
        /// </summary>
        private float TimecodeToSeconds(string timecode, float frameRate)
        {
            if (string.IsNullOrEmpty(timecode)) return -1f;
            
            string[] parts = timecode.Split(':');
            if (parts.Length != 4) return -1f;
            
            if (int.TryParse(parts[0], out int hours) &&
                int.TryParse(parts[1], out int minutes) &&
                int.TryParse(parts[2], out int seconds) &&
                int.TryParse(parts[3], out int frames))
            {
                return hours * 3600f + minutes * 60f + seconds + (frames / frameRate);
            }
            
            return -1f;
        }
    }
    
    /// <summary>
    /// UnityEventの拡張定義
    /// </summary>
    [System.Serializable]
    public class LTCUnityEvent : UnityEvent<LTCEventData> { }
    
    /// <summary>
    /// タイムコードイベントのプリセット
    /// </summary>
    [System.Serializable]
    public class TimecodeEventPreset
    {
        public string presetName = "Default";
        public List<TimecodeEvent> events = new List<TimecodeEvent>();
        
        /// <summary>
        /// すべてのイベントをリセット
        /// </summary>
        public void ResetAll()
        {
            foreach (var evt in events)
            {
                evt.Reset();
            }
        }
        
        /// <summary>
        /// プリセットを複製
        /// </summary>
        public TimecodeEventPreset Clone()
        {
            var clone = new TimecodeEventPreset();
            clone.presetName = presetName + " Copy";
            clone.events = new List<TimecodeEvent>();
            
            foreach (var evt in events)
            {
                var newEvent = new TimecodeEvent();
                newEvent.eventName = evt.eventName;
                newEvent.targetTimecode = evt.targetTimecode;
                newEvent.toleranceFrames = evt.toleranceFrames;
                newEvent.oneShot = evt.oneShot;
                newEvent.enabled = evt.enabled;
                // UnityEventはコピーしない（手動で再設定が必要）
                clone.events.Add(newEvent);
            }
            
            return clone;
        }
    }
}