# Unity LTC Decoder

Linear Timecode (LTC) decoder and Timeline synchronization package for Unity.

## 概要 / Overview

Unity LTC Decoderは、オーディオ入力からリニアタイムコード（LTC）をリアルタイムでデコードし、Unity Timelineと自動同期するパッケージです。

This package provides real-time Linear Timecode (LTC) decoding from audio input and automatic synchronization with Unity Timeline.

## 主な機能 / Features

- 🎵 **リアルタイムLTCデコード** / Real-time LTC decoding from microphone input
- ⏱️ **Unity Timeline自動同期** / Automatic Unity Timeline synchronization
- 🎯 **高精度DSPクロック同期** / High-precision DSP clock synchronization
- 📊 **ジッター除去・ノイズフィルタリング** / Advanced jitter detection and noise filtering
- 🎨 **カスタムInspector UI** / Comprehensive custom Inspector UI
- 📝 **イベントシステム** / Extensive Unity Event system
- 🔧 **デバッグツール** / Built-in debug and monitoring tools

## 必要環境 / Requirements

- Unity 2021.3 LTS以降
- Unity Timeline package
- マイク入力対応デバイス

## インストール / Installation

### Package Manager経由 (Git URL)

1. Unity Package Managerを開く
2. "+" ボタンから "Add package from git URL..." を選択
3. 以下のURLを入力:
```
https://github.com/murasaqi/Unity_LTCDecoder.git#jp.iridescent.ltcdecoder
```

### manifest.json経由

`Packages/manifest.json`に以下を追加:

```json
{
  "dependencies": {
    "jp.iridescent.ltcdecoder": "https://github.com/murasaqi/Unity_LTCDecoder.git#jp.iridescent.ltcdecoder"
  }
}
```

## 基本的な使い方 / Basic Usage

### 1. LTC Decoderのセットアップ

```csharp
using jp.iridescent.ltcdecoder;

// GameObjectにLTCDecoderコンポーネントを追加
LTCDecoder decoder = gameObject.AddComponent<LTCDecoder>();

// オーディオデバイスを選択（Inspectorで設定可能）
decoder.SelectedDevice = "Microphone Name";
```

### 2. Timeline同期の設定

```csharp
// TimelineとLTCを同期
LTCTimelineSync sync = gameObject.AddComponent<LTCTimelineSync>();
sync.SetLTCDecoder(decoder);

// 同期閾値の設定（秒）
sync.SyncThreshold = 0.5f;
```

### 3. イベントの利用

```csharp
// LTCイベントの購読
decoder.OnLTCStarted.AddListener((data) => {
    Debug.Log($"LTC Started: {data.currentTimecode}");
});

decoder.OnLTCStopped.AddListener((data) => {
    Debug.Log($"LTC Stopped at: {data.currentTimecode}");
});

decoder.OnLTCReceiving.AddListener((data) => {
    Debug.Log($"Receiving: {data.currentTimecode}");
});
```

## 高度な設定 / Advanced Settings

### 同期パラメータ

- **Sync Threshold**: 同期を開始する時間差の閾値（秒）
- **Jump Threshold**: ジャンプとして検出する時間差（秒）
- **Drift Correction**: ドリフト補正の強度（0-1）
- **Buffer Queue Size**: 同期バッファのサイズ（5-30）

### ノイズ除去

- **Signal Threshold**: 信号検出の閾値（0.001-0.1）
- **Denoising Strength**: ノイズ除去フィルタの強度（0-1）

## API リファレンス / API Reference

### LTCDecoder

主要プロパティ:
- `CurrentTimecode`: 現在の出力タイムコード（string）
- `DecodedTimecode`: デコードされた生のタイムコード（string）
- `HasSignal`: 信号受信状態（bool）
- `SignalLevel`: 信号レベル（0-1）
- `IsRecording`: 録音状態（bool）

主要メソッド:
- `StartRecording()`: 録音開始
- `StopRecording()`: 録音停止
- `ResetDecoder()`: デコーダーリセット

### LTCTimelineSync

主要プロパティ:
- `EnableSync`: 同期の有効/無効（bool）
- `IsPlaying`: Timeline再生状態（bool）
- `TimeDifference`: 現在の時間差（float）

主要メソッド:
- `SetLTCDecoder(decoder)`: LTCデコーダーを設定
- `ResetSync()`: 同期をリセット
- `SeekToTimecode(timecode)`: 指定タイムコードにシーク

## サンプル / Samples

パッケージには基本的なセットアップのサンプルが含まれています。
Package Managerの"Samples"タブからインポートできます。

サンプル内容:
- 基本的なLTCデコーダーセットアップ
- Timeline同期の実装例
- デバッグUI
- イベントロガー

## トラブルシューティング / Troubleshooting

### LTC信号が検出されない

1. オーディオデバイスが正しく選択されているか確認
2. Signal Thresholdを下げる（0.01程度）
3. マイクの権限設定を確認

### Timeline同期が不安定

1. Sync Thresholdを調整（0.1-1.0秒）
2. Buffer Queue Sizeを増やす（15-20）
3. Drift Correctionを調整（0.1-0.3）

### パフォーマンスの問題

1. Console出力を無効化（`logToConsole = false`）
2. Debug表示を最小限に
3. Update Intervalを増やす（UIの場合）

## ライセンス / License

MIT License

## 作者 / Author

Murasaqi

## サポート / Support

Issues: [GitHub Issues](https://github.com/murasaqi/Unity_LTCDecoder/issues)

## 更新履歴 / Changelog

### 1.0.0 (2024-08-30)
- 初回リリース
- LTCデコード機能
- Timeline同期機能
- カスタムInspector UI
- イベントシステム
- デバッグツール