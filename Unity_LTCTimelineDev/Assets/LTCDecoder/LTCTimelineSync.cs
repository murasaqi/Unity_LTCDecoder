using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Playables;
using LTC.Timeline;

[AddComponentMenu("Audio/LTC Timeline Sync")]
[RequireComponent(typeof(PlayableDirector))]
public class LTCTimelineSync : MonoBehaviour
{
    [Header("LTC Source")]
    [SerializeField] private LTCDecoder ltcDecoder;
    
    [Header("Sync Settings")]
    [SerializeField] private float syncThreshold = 0.1f;
    [SerializeField] private float smoothingFactor = 0.3f;
    [SerializeField] private bool enableSync = true;
    [SerializeField] private bool pauseWhenNoSignal = true;
    
    [Header("Frame Rate Settings")]
    [SerializeField] private float frameRate = 30f;
    [SerializeField] private bool autoDetectFrameRate = true;
    
    [Header("Status")]
    [SerializeField] private bool isSynced = false;
    [SerializeField] private float currentTimeDifference = 0f;
    [SerializeField] private string lastSyncedTimecode = "00:00:00:00";
    [SerializeField] private int consecutiveSyncFrames = 0;
    [SerializeField] private float timelineTime = 0f;
    [SerializeField] private float ltcTime = 0f;
    
    [Header("Logging")]
    [SerializeField] private bool enableLogging = false;
    [SerializeField] private bool logToConsole = false;
    
    private PlayableDirector playableDirector;
    private Coroutine syncCoroutine;
    private Timecode lastTimecode;
    private float lastLTCUpdateTime;
    private const int SYNC_STABILITY_FRAMES = 3;
    
    public bool IsSynced => isSynced;
    public float TimeDifference => currentTimeDifference;
    public bool EnableSync 
    { 
        get => enableSync; 
        set => enableSync = value; 
    }
    
    private void Awake()
    {
        playableDirector = GetComponent<PlayableDirector>();
        
        if (ltcDecoder == null)
        {
            ltcDecoder = FindFirstObjectByType<LTCDecoder>();
            if (ltcDecoder == null)
            {
                LogError("LTC Decoder Component not found. Please assign it manually.");
            }
        }
    }
    
    private void OnEnable()
    {
        if (ltcDecoder != null && playableDirector != null)
        {
            StartSync();
        }
    }
    
    private void OnDisable()
    {
        StopSync();
    }
    
    public void SetLTCDecoder(LTCDecoder decoder)
    {
        bool wasRunning = syncCoroutine != null;
        
        if (wasRunning)
        {
            StopSync();
        }
        
        ltcDecoder = decoder;
        
        if (wasRunning && ltcDecoder != null)
        {
            StartSync();
        }
    }
    
    public void StartSync()
    {
        if (syncCoroutine != null)
        {
            StopCoroutine(syncCoroutine);
        }
        
        syncCoroutine = StartCoroutine(SyncTimeline());
        LogInfo("Started LTC Timeline synchronization");
    }
    
    public void StopSync()
    {
        if (syncCoroutine != null)
        {
            StopCoroutine(syncCoroutine);
            syncCoroutine = null;
        }
        
        isSynced = false;
        consecutiveSyncFrames = 0;
        LogInfo("Stopped LTC Timeline synchronization");
    }
    
    private IEnumerator SyncTimeline()
    {
        while (enabled && ltcDecoder != null && playableDirector != null)
        {
            if (!enableSync)
            {
                yield return new WaitForSeconds(0.1f);
                continue;
            }
            
            if (ltcDecoder.HasSignal && ltcDecoder.IsRecording)
            {
                ProcessLTCSync();
            }
            else if (pauseWhenNoSignal && playableDirector.state == PlayState.Playing)
            {
                playableDirector.Pause();
                isSynced = false;
                consecutiveSyncFrames = 0;
                LogInfo("No LTC signal detected, pausing timeline");
            }
            
            yield return new WaitForSeconds(1f / 60f);
        }
    }
    
    private void ProcessLTCSync()
    {
        string currentLTCTimecode = ltcDecoder.CurrentTimecode;
        
        if (string.IsNullOrEmpty(currentLTCTimecode) || currentLTCTimecode == "00:00:00:00")
        {
            return;
        }
        
        Timecode currentTimecode = ParseTimecode(currentLTCTimecode);
        
        if (currentTimecode == null)
        {
            return;
        }
        
        if (autoDetectFrameRate)
        {
            DetectFrameRate(currentTimecode);
        }
        
        float ltcTimeInSeconds = TimecodeToSeconds(currentTimecode);
        ltcTime = ltcTimeInSeconds;
        
        if (playableDirector.playableAsset != null)
        {
            timelineTime = (float)playableDirector.time;
            float timeDifference = Mathf.Abs(ltcTimeInSeconds - timelineTime);
            currentTimeDifference = timeDifference;
            
            if (timeDifference > syncThreshold)
            {
                float targetTime = ltcTimeInSeconds;
                
                if (isSynced && smoothingFactor > 0)
                {
                    targetTime = Mathf.Lerp(timelineTime, ltcTimeInSeconds, smoothingFactor);
                }
                
                playableDirector.time = targetTime;
                
                if (playableDirector.state != PlayState.Playing)
                {
                    playableDirector.Play();
                }
                
                LogInfo($"Syncing Timeline: LTC={currentLTCTimecode}, Timeline={timelineTime:F3}s, Difference={timeDifference:F3}s");
                
                consecutiveSyncFrames = 0;
                isSynced = false;
            }
            else
            {
                consecutiveSyncFrames++;
                
                if (consecutiveSyncFrames >= SYNC_STABILITY_FRAMES)
                {
                    if (!isSynced)
                    {
                        LogInfo($"Timeline in sync with LTC (difference: {timeDifference:F3}s)");
                    }
                    isSynced = true;
                }
                
                if (playableDirector.state != PlayState.Playing)
                {
                    playableDirector.Play();
                }
            }
            
            lastSyncedTimecode = currentLTCTimecode;
            lastTimecode = currentTimecode;
            lastLTCUpdateTime = Time.time;
        }
    }
    
    private void DetectFrameRate(Timecode timecode)
    {
        if (timecode.DropFrame)
        {
            frameRate = 29.97f;
        }
        else
        {
            if (lastTimecode != null)
            {
                float deltaTime = Time.time - lastLTCUpdateTime;
                if (deltaTime > 0 && deltaTime < 1f)
                {
                    int frameDiff = timecode.Frame - lastTimecode.Frame;
                    if (frameDiff < 0) frameDiff += 30;
                    
                    if (frameDiff > 0)
                    {
                        float estimatedFps = frameDiff / deltaTime;
                        frameRate = Mathf.Lerp(frameRate, estimatedFps, 0.1f);
                        frameRate = Mathf.Round(frameRate * 100f) / 100f;
                    }
                }
            }
        }
    }
    
    private float TimecodeToSeconds(Timecode timecode)
    {
        float fps = frameRate;
        
        if (timecode.DropFrame)
        {
            fps = 29.97f;
        }
        
        float totalSeconds = timecode.Hour * 3600f +
                           timecode.Minute * 60f +
                           timecode.Second +
                           (timecode.Frame / fps);
        
        return totalSeconds;
    }
    
    private Timecode ParseTimecode(string timecodeString)
    {
        if (string.IsNullOrEmpty(timecodeString))
            return null;
        
        string[] parts = timecodeString.Split(':');
        if (parts.Length != 4)
            return null;
        
        if (int.TryParse(parts[0], out int hour) &&
            int.TryParse(parts[1], out int minute) &&
            int.TryParse(parts[2], out int second) &&
            int.TryParse(parts[3], out int frame))
        {
            var timecode = new Timecode();
            timecode.Hour = hour;
            timecode.Minute = minute;
            timecode.Second = second;
            timecode.Frame = frame;
            return timecode;
        }
        
        return null;
    }
    
    public void ResetSync()
    {
        if (playableDirector != null)
        {
            playableDirector.time = 0;
            playableDirector.Evaluate();
        }
        
        isSynced = false;
        consecutiveSyncFrames = 0;
        currentTimeDifference = 0f;
        lastSyncedTimecode = "00:00:00:00";
        
        LogInfo("Timeline sync reset");
    }
    
    public void SeekToTimecode(string timecodeString)
    {
        var timecode = ParseTimecode(timecodeString);
        if (timecode != null && playableDirector != null)
        {
            float targetTime = TimecodeToSeconds(timecode);
            playableDirector.time = targetTime;
            playableDirector.Evaluate();
            
            LogInfo($"Timeline seeked to {timecodeString} ({targetTime:F3}s)");
        }
    }
    
    #if UNITY_EDITOR
    private void OnValidate()
    {
        syncThreshold = Mathf.Max(0.001f, syncThreshold);
        smoothingFactor = Mathf.Clamp01(smoothingFactor);
        frameRate = Mathf.Clamp(frameRate, 1f, 120f);
    }
    #endif
    
    private void LogInfo(string message)
    {
        if (!enableLogging) return;
        if (logToConsole)
        {
            Debug.Log($"[LTC Sync] {message}");
        }
    }
    
    private void LogError(string message)
    {
        // Always log errors even if logging is disabled
        Debug.LogError($"[LTC Sync] {message}");
    }
}