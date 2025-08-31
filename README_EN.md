# Unity LTC Timeline Sync

[日本語版](README.md)

A Unity package for real-time Linear Timecode (LTC) decoding and synchronization with Unity Timeline. Analyzes LTC signals from audio input and automatically synchronizes PlayableDirector.

![LTC Decoder Inspector View](Documents/LTC_Decoder_InspectorView.png)

## ✨ Key Features

- 🎙️ **Real-time LTC Decoding** - Analyze LTC signals from microphone input
- 🎬 **Unity Timeline Auto-sync** - High-precision synchronization with PlayableDirector
- 🔧 **Advanced Noise Filtering** - Jitter removal with adaptive filtering
- 📊 **Comprehensive Debug Tools** - Waveform display, jitter analysis, detailed logging
- 🎮 **Flexible Event System** - Extensible integration via UnityEvents
- 🖥️ **Debug UI** - Real-time timecode display and status monitoring

![Debug UI Screenshot](Documents/UI_ScreenShot.png)

## 📦 Installation

### Via Unity Package Manager

1. Open Unity Package Manager (Window > Package Manager)
2. Click "+" button and select "Add package from git URL..."
3. Enter the following URL:
```
https://github.com/iridescent-jp/Unity_LTCDecoder.git?path=jp.iridescent.ltcdecoder
```

### Manual Installation

1. Clone this repository
2. Copy the `jp.iridescent.ltcdecoder` folder to your project's `Packages` folder

## 🚀 Quick Start

### Basic Setup

1. **Add LTC Decoder**
   - Add `LTCDecoder` component to a GameObject
   - Select audio input device in Inspector

2. **Configure Timeline Sync**
   - Add `LTCTimelineSync` component to GameObject with PlayableDirector
   - Set reference to LTC Decoder component

3. **Play**
   - Start Play mode
   - Start your LTC source

### Easy Setup from Menu

**Basic Setup**: `GameObject > LTC Decoder > Create LTC Decoder`

**Setup with Debug UI**: `GameObject > LTC Decoder > Create Complete UI Setup`

## ⚙️ Component Details

### LTCDecoder

Main LTC decoding component. Analyzes LTC signals from audio input.

**Main Settings**:
- `Device`: Audio input device
- `Frame Rate`: Timecode frame rate (24/25/29.97/30 fps)
- `Drop Frame`: Use drop-frame timecode
- `Sample Rate`: Audio sampling rate

**Noise Filtering Settings**:
- `Use Timecode Validation`: Enable timecode continuity checking
- `Jitter Threshold`: Jitter detection threshold (default: 100ms)
- `Denoising Strength`: Filter strength 0-1 (default: 0.8)

### LTCTimelineSync

Component for synchronizing Unity Timeline with decoded LTC.

**Sync Settings**:
- `Sync Threshold`: Sync trigger threshold (default: 0.1s)
- `Smoothing Factor`: Timeline adjustment smoothness (0-1)
- `Pause When No Signal`: Auto-pause timeline when LTC signal is lost

**API Features**:
```csharp
// Dynamically set Timeline
ltcSync.SetTimeline(timelineAsset);

// Set PlayableDirector from another GameObject
ltcSync.SetPlayableDirector(director);

// Set timecode offset
ltcSync.SetTimelineOffset(10.0f);

// Set track binding
ltcSync.SetBinding(trackName, bindingObject);
```

### LTCEventDebugger

Component for debugging and monitoring the event system.

**Events**:
- `OnTimecodeReceived`: When timecode is received
- `OnTimecodeJump`: When timecode jump is detected
- `OnSignalLost`: When LTC signal is lost
- `OnSignalRestored`: When LTC signal is restored

## 🎛️ Recommended Settings

### Clean LTC Source (Hardware Generator)
```
Jitter Threshold: 0.05 (50ms)
Denoising Strength: 0.5
Min Consecutive Valid Frames: 2
```

### Noisy LTC Source (Tape/Wireless)
```
Jitter Threshold: 0.15 (150ms)
Denoising Strength: 0.8-1.0
Min Consecutive Valid Frames: 3-4
```

### Development/Testing
```
Enable Debug Mode: ON
Log Debug Info: ON
Log To Console: OFF (for performance)
```

## 🔍 Troubleshooting

### No Timecode Display
1. Check if audio input device is correctly selected
2. Verify LTC signal is being input
3. Check frame rate and drop frame settings

### Unstable Timecode
1. Increase `Jitter Threshold`
2. Increase `Denoising Strength`
3. Increase `Min Consecutive Valid Frames`

### Timeline Sync Not Working
1. Check if PlayableDirector is correctly configured
2. Adjust `Sync Threshold`
3. Verify TimelineAsset is set

## 📊 Performance Optimization

### Log Settings
- **Always keep** `Log To Console` **OFF** (major performance impact)
- Enable specific log categories only when needed
- Check logs in Inspector's Debug Logs section

### Buffer Size
- Trade-off between latency and stability
- Recommended: 512-1024 samples

## 🛠️ Developer Information

### Build Settings
- Enable microphone permissions
- Recommended sample rate: 48000 Hz
- Note platform-specific settings

### Extension Development
When adding new features:
1. Check impact on `ValidateTimecode` logic
2. Test with both clean and noisy LTC sources
3. Ensure logging doesn't impact performance
4. Update this documentation

## 📋 Requirements

- Unity 2021.3 LTS or later
- Windows / macOS / Linux
- Microphone input device

## 📄 License

MIT License

## 🤝 Contributing

Please submit issues and pull requests to the [GitHub repository](https://github.com/iridescent-jp/Unity_LTCDecoder).

## 📝 Version History

### v1.2.0 (2025-08-31)
- Enhanced external control API
- Flexible PlayableDirector reference
- Automatic Inspector update
- Improved settings persistence system

### v1.1.0 (2025-08-30)
- Unity Package Manager support
- Event system redesign
- Debug UI improvements

### v1.0.0
- Initial release
- Basic LTC decoding functionality
- Timeline synchronization

---

Developed by [Iridescent](https://iridescent.jp)