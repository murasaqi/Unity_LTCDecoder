using System;
using UnityEngine;

namespace jp.iridescent.ltcdecoder
{
    /// <summary>
    /// セッション統計データ
    /// </summary>
    [Serializable]
    public struct SessionStatistics
    {
        public DateTime startTime;
        public DateTime endTime;
        public TimeSpan duration;
        public int totalEvents;
        public float averageSignalLevel;
        public int dropoutCount;
        public int timecodeJumpCount;
        public float longestDropout;
        public bool isActive;
    }
    
    /// <summary>
    /// 信号品質レポート
    /// </summary>
    [Serializable]
    public struct SignalQualityReport
    {
        public float averageLevel;
        public float dropoutRate;
        public float stability;
        public float qualityScore;
    }
    
    /// <summary>
    /// デバッグメッセージのデータ構造
    /// </summary>
    [Serializable]
    public class DebugMessage
    {
        // メッセージ内容
        public string message;
        
        // カテゴリ
        public string category;
        
        // メッセージ発生時のタイムコード
        public string timecode;
        
        // タイムスタンプ
        public DateTime timestamp;
        
        // 表示色
        public Color color;
        
        // 信号レベル（0-1）
        public float signalLevel;
        
        // カテゴリ定数
        public const string INFO = "Info";
        public const string EVENT = "Event";
        public const string WARNING = "Warning";
        public const string ERROR = "Error";
        public const string TIMECODE_EVENT = "TimecodeEvent";
        public const string PERFORMANCE = "Performance";
        public const string DEBUG = "Debug";
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public DebugMessage(string msg, string cat = INFO, string tc = "00:00:00:00", float signal = 0f, Color? col = null)
        {
            message = msg;
            category = cat;
            timecode = tc;
            timestamp = DateTime.Now;
            signalLevel = signal;
            color = col ?? GetDefaultColorForCategory(cat);
        }
        
        /// <summary>
        /// カテゴリに応じたデフォルト色を取得
        /// </summary>
        private static Color GetDefaultColorForCategory(string category)
        {
            switch (category)
            {
                case INFO:
                    return Color.white;
                case EVENT:
                    return Color.green;
                case WARNING:
                    return Color.yellow;
                case ERROR:
                    return Color.red;
                case TIMECODE_EVENT:
                    return Color.cyan;
                case PERFORMANCE:
                    return Color.magenta;
                case DEBUG:
                    return Color.gray;
                default:
                    return Color.white;
            }
        }
        
        /// <summary>
        /// フォーマット済み文字列を取得
        /// </summary>
        public string GetFormattedMessage()
        {
            return $"[{timestamp:HH:mm:ss.fff}] [{category}] {message} (TC: {timecode})";
        }
        
        /// <summary>
        /// CSV形式で出力
        /// </summary>
        public string ToCSV()
        {
            return $"{timestamp:yyyy-MM-dd HH:mm:ss.fff},{category},{message},{timecode},{signalLevel:F2}";
        }
        
        /// <summary>
        /// JSON形式で出力
        /// </summary>
        public string ToJSON()
        {
            return JsonUtility.ToJson(this, true);
        }
    }
}