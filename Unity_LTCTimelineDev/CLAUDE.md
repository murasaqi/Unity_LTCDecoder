# Unity LTC Timeline Project

## 🎯 開発行動指針

### 基本原則
1. **言語ルール**
   - 対話：日本語で行う
   - コード内コメント：日本語で記述
   - 思考処理：英語（効率性のため）
   - ドキュメント：基本日本語、技術用語は英語可
   - 作業前に必ず`TODO.md`リスト確認
   - 指示がありToDoが決まったら`TODO.md`にタスクを追加してから作業を開始してください。
   - 新規で発生した課題は即座に`TODO.md`に追加
   - タスクが完了したら`TODO.md`に進捗を記載し、Gitにコミットしなさい
   - タスクが完了したら、`TODO.md`を確認してください。
   - `TODO.md`に着手可能なタスクがあれば優先度順に着手してください
   - UnityのConsoleを確認してエラーがなくなるまで開発を続けてください


2. **Unity Natural MCP活用**
   - 必ず**Unity Natural MCP**でConsole確認しながら開発
   - `mcp__unity-natural-mcp__get_compile_logs` でコンパイルエラー確認
   - `mcp__unity-natural-mcp__get_current_console_logs` で実行時ログ確認
   - `mcp__unity-natural-mcp__refresh_assets` でアセット更新


3. **開発サイクル**
   ```
   1. TODO.md確認 → 優先タスク選択
   2. Unity Console確認 → エラーなし確認
   3. 実装・テスト
   4. エラー対処 → Console確認
   5. タスク完了 → TODO.md更新
   6. Gitコミット
   7. 次タスクへ
   ```

4. **品質基準**
   - Unity Consoleエラーゼロ
   - パフォーマンス影響を考慮
   - 既存機能への影響確認
   - タスク完了ごとにコミット

---

## Overview
This Unity project implements Linear Timecode (LTC) decoding and synchronization with Unity Timeline. It provides real-time timecode decoding from audio input and automatic synchronization of Unity's PlayableDirector with the incoming LTC signal.

## Key Components

### 1. LTCDecoder
**Location**: `Assets/LTCDecoder/LTCDecoder.cs`

Main component for decoding LTC from audio input. Features:
- Real-time LTC decoding from microphone input
- Configurable jitter detection and filtering
- Advanced denoising with adaptive filtering
- Comprehensive logging system with performance optimization
- Audio level monitoring and waveform visualization

**Important Settings**:
- `useTimecodeValidation`: Enable/disable timecode continuity checking
- `jitterThreshold`: Threshold for detecting timecode jumps (default 100ms)
- `maxAllowedJitter`: Maximum allowed jump before rejection (default 500ms)
- `denoisingStrength`: Filter strength 0-1 (default 0.8)
- `logToConsole`: Set to false for better performance (default false)

### 2. LTCTimelineSync
**Location**: `Assets/LTCDecoder/LTCTimelineSync.cs`

Synchronizes Unity Timeline with decoded LTC. Features:
- Automatic timeline synchronization with configurable threshold
- Smooth following with adjustable smoothing factor
- Pause on signal loss option
- Frame rate auto-detection

**Key Settings**:
- `syncThreshold`: Time difference to trigger sync (default 0.1s)
- `smoothingFactor`: Smoothing for timeline adjustments (0-1)
- `pauseWhenNoSignal`: Auto-pause timeline when LTC signal lost

### 3. LTCDecoderEditor
**Location**: `Assets/LTCDecoder/Editor/LTCDecoderEditor.cs`

Custom Inspector UI providing:
- Real-time timecode display
- Audio level meters and waveform
- Jitter analysis with before/after comparison
- Comprehensive debug settings
- Performance-optimized logging controls

## Technical Details

### Timecode Validation Logic
The validation system distinguishes between:
1. **Noise**: Random, unstable jumps → Rejected
2. **Intentional jumps**: Stable, continuous progression → Accepted quickly
3. **Continuous playback**: Small incremental changes → Always accepted when stable

### Performance Optimization
- Console logging disabled by default (major performance impact)
- Log throttling to prevent spam
- Efficient buffer handling for audio processing
- Optimized validation logic for minimal overhead

### Known Issues & Solutions

**Issue**: TC stops when Timecode Validation is enabled
**Solution**: The validation logic now properly handles:
- First timecode on startup (immediately accepted)
- Intentional time jumps (tracked and accepted after stability check)
- Continuous progression after jumps

**Issue**: Frame rate drops due to excessive logging
**Solution**: 
- Set `logToConsole = false` (default)
- Use log level filtering
- Enable specific categories only when needed

## Testing & Debugging

### Quick Test Setup
1. Add `LTCDecoder` to a GameObject
2. Add `LTCTimelineSync` to GameObject with PlayableDirector
3. Link the decoder component to the sync component
4. Select audio input device in Inspector
5. Press Play and start LTC source

### Debug Workflow
1. Enable `logDebugInfo` in Inspector
2. Keep `logToConsole = false` for performance
3. View logs in Inspector's Debug Logs section
4. Use Jitter Analysis section to monitor filtering effectiveness
5. Adjust thresholds based on your LTC source quality

### Recommended Settings for Different Scenarios

**Clean LTC source (hardware generator)**:
- `jitterThreshold`: 0.05 (50ms)
- `denoisingStrength`: 0.5
- `minConsecutiveValidFrames`: 2

**Noisy LTC source (tape/wireless)**:
- `jitterThreshold`: 0.15 (150ms)
- `denoisingStrength`: 0.8-1.0
- `minConsecutiveValidFrames`: 3-4

**Development/Testing**:
- `enableDebugMode`: true
- `logDebugInfo`: true
- `logToConsole`: false (enable only when needed)
- Monitor rejection rate in Jitter Analysis

## Build & Deployment

### Build Settings
- Ensure microphone permissions are enabled for target platform
- Set appropriate sample rate (48000 Hz recommended)
- Buffer size affects latency vs stability tradeoff

### Platform-Specific Notes
- **Windows**: Default audio device used if none specified
- **macOS**: May require microphone permission in Privacy settings
- **Mobile**: Requires explicit microphone permission request

## Maintenance Notes

### Adding New Features
When modifying the decoder:
1. Always check impact on `ValidateTimecode` logic
2. Test with both clean and noisy LTC sources
3. Verify logging doesn't impact performance
4. Update this documentation

### Performance Profiling
Key areas to monitor:
1. `ProcessAudioBuffer` - Main audio processing
2. `ValidateTimecode` - Validation logic
3. `LogDebug` calls - Should be minimal in hot paths
4. Unity Console output - Major performance impact if enabled


## Version History
- Initial implementation: Basic LTC decoding
- Added Timeline synchronization
- Improved validation logic for stability
- Added comprehensive jitter filtering and denoising
- Optimized logging system for performance
- Fixed intentional jump handling
- Added development workflow rules