using UnityEngine;
using LTC.Timeline;

namespace LTC.Debug
{
    /// <summary>
    /// LTCEventDebuggerの動作テスト用スクリプト
    /// </summary>
    [RequireComponent(typeof(LTCEventDebugger))]
    public class LTCEventDebuggerTest : MonoBehaviour
    {
        private LTCEventDebugger debugger;
        private float testTimer = 0f;
        private int testPhase = 0;
        
        void Start()
        {
            debugger = GetComponent<LTCEventDebugger>();
            
            UnityEngine.Debug.Log("[LTC Test] デバッガーテストを開始します");
            UnityEngine.Debug.Log("[LTC Test] 自動でイベントをトリガーしてデバッガーの動作を確認します");
        }
        
        void Update()
        {
            if (!debugger || !debugger.enabled) return;
            
            testTimer += Time.deltaTime;
            
            // フェーズごとにテスト実行
            switch (testPhase)
            {
                case 0: // 2秒後: LTC Start
                    if (testTimer > 2f)
                    {
                        UnityEngine.Debug.Log("[LTC Test] Phase 1: LTC Started イベントをトリガー");
                        debugger.TriggerEvent(LTCEventDebugger.EventType.LTCStarted);
                        testPhase++;
                        testTimer = 0f;
                    }
                    break;
                    
                case 1: // 1秒後: Receiving開始
                    if (testTimer > 1f)
                    {
                        UnityEngine.Debug.Log("[LTC Test] Phase 2: LTC Receiving イベントを開始");
                        testPhase++;
                        testTimer = 0f;
                    }
                    break;
                    
                case 2: // Receiving中（3秒間）
                    if (testTimer < 3f)
                    {
                        // 0.1秒ごとにReceivingイベント
                        if (Time.frameCount % 6 == 0)
                        {
                            debugger.TriggerEvent(LTCEventDebugger.EventType.LTCReceiving);
                        }
                    }
                    else
                    {
                        UnityEngine.Debug.Log("[LTC Test] Phase 3: タイムコードイベントをトリガー");
                        debugger.SimulateTimecode("00:00:10:00");
                        testPhase++;
                        testTimer = 0f;
                    }
                    break;
                    
                case 3: // 2秒後: No Signal
                    if (testTimer > 2f)
                    {
                        UnityEngine.Debug.Log("[LTC Test] Phase 4: No Signal イベントをトリガー");
                        debugger.TriggerEvent(LTCEventDebugger.EventType.LTCNoSignal);
                        testPhase++;
                        testTimer = 0f;
                    }
                    break;
                    
                case 4: // 2秒後: LTC Stop
                    if (testTimer > 2f)
                    {
                        UnityEngine.Debug.Log("[LTC Test] Phase 5: LTC Stopped イベントをトリガー");
                        debugger.TriggerEvent(LTCEventDebugger.EventType.LTCStopped);
                        testPhase++;
                        testTimer = 0f;
                    }
                    break;
                    
                case 5: // テスト完了
                    if (testTimer > 2f)
                    {
                        UnityEngine.Debug.Log("[LTC Test] テスト完了！");
                        UnityEngine.Debug.Log("[LTC Test] デバッガーのInspectorでイベント履歴と統計を確認してください");
                        
                        // 統計情報を表示
                        ShowStatistics();
                        
                        // このスクリプトを無効化
                        enabled = false;
                    }
                    break;
            }
        }
        
        private void ShowStatistics()
        {
            var stats = debugger.EventStats;
            
            UnityEngine.Debug.Log("===== イベント統計 =====");
            foreach (var kvp in stats)
            {
                var stat = kvp.Value;
                UnityEngine.Debug.Log($"{kvp.Key}: {stat.totalCount} 回発火, 最終: {stat.lastTimecode}");
            }
            
            var tcStats = debugger.TimecodeEventStats;
            if (tcStats.Count > 0)
            {
                UnityEngine.Debug.Log("===== タイムコードイベント統計 =====");
                foreach (var kvp in tcStats)
                {
                    var stat = kvp.Value;
                    UnityEngine.Debug.Log($"{kvp.Key}: {stat.totalCount} 回発火");
                }
            }
            
            UnityEngine.Debug.Log($"履歴エントリ数: {debugger.EventHistory.Count}");
        }
    }
}