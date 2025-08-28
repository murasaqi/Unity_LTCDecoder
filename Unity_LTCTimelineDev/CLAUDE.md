# Unity LTC Timeline Project

## Overview
This Unity project implements Linear Timecode (LTC) decoding and synchronization with Unity Timeline. It provides real-time timecode decoding from audio input and automatic synchronization of Unity's PlayableDirector with the incoming LTC signal.

## Key Components

### 1. LTCDecoderComponent
**Location**: `Assets/LTCDecoder/LTCDecoderComponent.cs`

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

### 2. LTCTimelineSyncComponent
**Location**: `Assets/LTCDecoder/LTCTimelineSyncComponent.cs`

Synchronizes Unity Timeline with decoded LTC. Features:
- Automatic timeline synchronization with configurable threshold
- Smooth following with adjustable smoothing factor
- Pause on signal loss option
- Frame rate auto-detection

**Key Settings**:
- `syncThreshold`: Time difference to trigger sync (default 0.1s)
- `smoothingFactor`: Smoothing for timeline adjustments (0-1)
- `pauseWhenNoSignal`: Auto-pause timeline when LTC signal lost

### 3. LTCDecoderComponentEditor
**Location**: `Assets/LTCDecoder/Editor/LTCDecoderComponentEditor.cs`

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
1. Add `LTCDecoderComponent` to a GameObject
2. Add `LTCTimelineSyncComponent` to GameObject with PlayableDirector
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

## Development Rules & Workflow

### 🚀 開発の進め方

#### 0. 言語ルール
- **対話**: 日本語で行うこと
- **コメント**: コード内のコメントは日本語で記述
- **思考**: 内部思考は英語で行う（効率的な処理のため）
- **ドキュメント**: 基本的に日本語、技術用語は英語可

#### 1. Unity Natural MCPの活用
- 開発は必ず**Unity Natural MCP**を使用してUnity Consoleを確認しながら進める
- コンパイルエラーは即座に確認: `mcp__unity-natural-mcp__get_compile_logs`
- 実行時ログの確認: `mcp__unity-natural-mcp__get_current_console_logs`
- アセットのリフレッシュ: `mcp__unity-natural-mcp__refresh_assets`

#### 2. タスク管理
- **作業開始前**：必ずToDoリストを作成し、タスクを優先度順に整理
- **タスク着手前**：現在のToDoリストを確認し、最優先タスクから着手
- **進捗記録**：各タスクの進捗をこまめに記入（in_progress, completed等）
- **新規課題**：開発中に発見した課題は即座にToDoリストに追加

#### 3. 開発サイクル
```
1. ToDoリスト確認 → 優先度の高いタスクを選択
2. タスクのステータスを "in_progress" に更新
3. Unity Consoleで動作確認しながら実装
4. エラーが出たら即座に対処
5. タスク完了 → ステータスを "completed" に更新
6. Gitにコミット
7. 次のタスクへ（1に戻る）
```

#### 4. Git運用
- **タスク完了ごと**にコミット（大きなタスクは適切に分割）
- コミットメッセージは明確に（何を・なぜ・どのように）
- エラーが残っている状態でのコミットは避ける

#### 5. 品質管理
- Unity Consoleにエラーが出ていないことを確認
- パフォーマンスへの影響を常に意識
- 既存機能を壊していないか確認

### 実装例
```
// 良い例：タスク管理されたワークフロー
1. TodoWrite: "Add new feature X" → in_progress
2. Unity Console確認 → エラーなし
3. 実装完了
4. Unity Console再確認 → 動作確認OK
5. TodoWrite: "Add new feature X" → completed
6. git commit -m "Add feature X: implemented Y for Z purpose"
7. 次のタスクへ

// 悪い例：場当たり的な開発
- ToDoリストなしで作業開始
- エラーを無視して次の作業へ
- まとめて大量の変更をコミット
```

## Version History
- Initial implementation: Basic LTC decoding
- Added Timeline synchronization
- Improved validation logic for stability
- Added comprehensive jitter filtering and denoising
- Optimized logging system for performance
- Fixed intentional jump handling
- Added development workflow rules