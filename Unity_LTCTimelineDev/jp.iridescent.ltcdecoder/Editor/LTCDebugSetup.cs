using UnityEngine;
using UnityEditor;
using jp.iridescent.ltcdecoder;

namespace jp.iridescent.ltcdecoder.Editor
{
    /// <summary>
    /// LTCデバッグセットアップユーティリティ
    /// </summary>
    public static class LTCDebugSetup
    {
        [MenuItem("GameObject/LTC Debug/Create LTC Decoder", false, 10)]
        public static void CreateLTCDecoder()
        {
            // LTCDecoder + Debugger作成
            GameObject ltcObject = CreateLTCDecoderWithDebugger();
            
            // 選択
            Selection.activeGameObject = ltcObject;
            
            UnityEngine.Debug.Log("[LTC Debug Setup] LTC Decoder created successfully!");
            UnityEngine.Debug.Log("[LTC Debug Setup] Note: For UI components, import the package samples via Package Manager.");
        }
        
        /// <summary>
        /// LTCDecoder + Debuggerオブジェクト作成
        /// </summary>
        private static GameObject CreateLTCDecoderWithDebugger()
        {
            GameObject ltcObject = new GameObject("LTC Decoder");
            
            // LTCDecoder追加
            LTCDecoder decoder = ltcObject.AddComponent<LTCDecoder>();
            
            // LTCEventDebugger追加
            LTCEventDebugger debugger = ltcObject.AddComponent<LTCEventDebugger>();
            
            // デフォルト設定
            SetupDefaultSettings(decoder, debugger);
            
            UnityEngine.Debug.Log("[LTC Debug Setup] LTC Decoder with Debugger created");
            
            return ltcObject;
        }
        
        /// <summary>
        /// デフォルト設定
        /// </summary>
        private static void SetupDefaultSettings(LTCDecoder decoder, LTCEventDebugger debugger)
        {
            // LTCDecoderのデフォルト設定
            if (decoder != null)
            {
                SerializedObject decoderSO = new SerializedObject(decoder);
                
                // プロパティが存在するか確認してから設定
                var enableDebugProp = decoderSO.FindProperty("enableDebugMode");
                if (enableDebugProp != null) enableDebugProp.boolValue = true;
                
                var logDebugProp = decoderSO.FindProperty("logDebugInfo");
                if (logDebugProp != null) logDebugProp.boolValue = true;
                
                var logConsoleProp = decoderSO.FindProperty("logToConsole");
                if (logConsoleProp != null) logConsoleProp.boolValue = false;
                
                decoderSO.ApplyModifiedProperties();
            }
            
            // LTCEventDebuggerのデフォルト設定
            if (debugger != null)
            {
                SerializedObject debuggerSO = new SerializedObject(debugger);
                
                // プロパティが存在するか確認してから設定
                var enableLogProp = debuggerSO.FindProperty("enableLogging");
                if (enableLogProp != null) enableLogProp.boolValue = true;
                
                var logConsoleProp = debuggerSO.FindProperty("logToConsole");
                if (logConsoleProp != null) logConsoleProp.boolValue = false;
                
                var maxHistoryProp = debuggerSO.FindProperty("maxHistorySize");
                if (maxHistoryProp != null) maxHistoryProp.intValue = 100;
                
                debuggerSO.ApplyModifiedProperties();
            }
            
            UnityEngine.Debug.Log("[LTC Debug Setup] Default settings applied");
        }
        
        [MenuItem("GameObject/LTC Debug/Create Timeline Sync", false, 11)]
        public static void CreateTimelineSync()
        {
            GameObject syncObject = new GameObject("LTC Timeline Sync");
            
            // PlayableDirector追加
            var director = syncObject.AddComponent<UnityEngine.Playables.PlayableDirector>();
            
            // LTCTimelineSync追加
            LTCTimelineSync sync = syncObject.AddComponent<LTCTimelineSync>();
            
            // 既存のLTCDecoderを検索して設定
            LTCDecoder decoder = GameObject.FindObjectOfType<LTCDecoder>();
            if (decoder != null)
            {
                sync.SetLTCDecoder(decoder);
                UnityEngine.Debug.Log("[LTC Debug Setup] LTC Decoder linked to Timeline Sync");
            }
            else
            {
                UnityEngine.Debug.LogWarning("[LTC Debug Setup] No LTC Decoder found in scene. Please link manually.");
            }
            
            // 選択
            Selection.activeGameObject = syncObject;
            
            UnityEngine.Debug.Log("[LTC Debug Setup] Timeline Sync created successfully!");
        }
        
        [MenuItem("GameObject/LTC Debug/Documentation", false, 100)]
        public static void OpenDocumentation()
        {
            Application.OpenURL("https://github.com/murasaqi/Unity_LTCDecoder");
        }
    }
}