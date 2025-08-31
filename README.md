# Unity LTC Timeline Sync

[English Version](README_EN.md)

Unity上でLinear Timecode (LTC)をリアルタイムデコードし、Unity Timelineと同期させるためのパッケージです。オーディオ入力からLTC信号を解析し、PlayableDirectorを自動的に同期させます。

![LTC Decoder Inspector View](Documents/LTC_Decoder_InspectorView.png)

## ✨ 主な機能

- 🎙️ **リアルタイムLTCデコード** - マイク入力からLTC信号を解析
- 🎬 **Unity Timeline自動同期** - PlayableDirectorとの高精度同期
- 🔧 **高度なノイズ除去** - 適応フィルタリングによるジッター除去
- 📊 **包括的なデバッグツール** - 波形表示、ジッター解析、詳細ログ
- 🎮 **柔軟なイベントシステム** - UnityEventによる拡張性の高い連携
- 🖥️ **デバッグUI** - リアルタイムタイムコード表示とステータスモニター

![Debug UI Screenshot](Documents/UI_ScreenShot.png)

## 📦 インストール

### Unity Package Manager経由

1. Unity Package Managerを開く (Window > Package Manager)
2. 「+」ボタンから「Add package from git URL...」を選択
3. 以下のURLを入力:
```
https://github.com/iridescent-jp/Unity_LTCDecoder.git?path=jp.iridescent.ltcdecoder
```

### 手動インストール

1. このリポジトリをクローン
2. `jp.iridescent.ltcdecoder`フォルダをプロジェクトの`Packages`フォルダにコピー

## 🚀 クイックスタート

### 基本セットアップ

1. **LTC Decoderを追加**
   - GameObjectに`LTCDecoder`コンポーネントを追加
   - Inspectorでオーディオ入力デバイスを選択

2. **Timeline同期を設定**
   - PlayableDirectorを持つGameObjectに`LTCTimelineSync`コンポーネントを追加
   - LTC Decoderコンポーネントへの参照を設定

3. **再生**
   - Playモードを開始
   - LTC信号ソースを起動

### メニューからの簡単セットアップ

**基本セットアップ**: `GameObject > LTC Decoder > Create LTC Decoder`

**デバッグUI付きセットアップ**: `GameObject > LTC Decoder > Create Complete UI Setup`

## ⚙️ コンポーネント詳細

### LTCDecoder

メインのLTCデコードコンポーネント。オーディオ入力からLTC信号を解析します。

**主要設定**:
- `Device`: オーディオ入力デバイス
- `Frame Rate`: タイムコードフレームレート (24/25/29.97/30 fps)
- `Drop Frame`: ドロップフレームタイムコード使用
- `Sample Rate`: オーディオサンプリングレート

**ノイズ除去設定**:
- `Use Timecode Validation`: タイムコード連続性チェック
- `Jitter Threshold`: ジッター検出閾値 (デフォルト: 100ms)
- `Denoising Strength`: フィルタ強度 0-1 (デフォルト: 0.8)

### LTCTimelineSync

Unity TimelineをデコードされたLTCと同期させるコンポーネント。

**同期設定**:
- `Sync Threshold`: 同期トリガー閾値 (デフォルト: 0.1秒)
- `Smoothing Factor`: タイムライン調整の滑らかさ (0-1)
- `Pause When No Signal`: LTC信号喪失時の自動一時停止

**API機能**:
```csharp
// 動的にTimelineを設定
ltcSync.SetTimeline(timelineAsset);

// PlayableDirectorを別のGameObjectから設定
ltcSync.SetPlayableDirector(director);

// タイムコードオフセットを設定
ltcSync.SetTimelineOffset(10.0f);

// トラックバインディングを設定
ltcSync.SetBinding(trackName, bindingObject);
```

### LTCEventDebugger

イベントシステムのデバッグとモニタリング用コンポーネント。

**イベント**:
- `OnTimecodeReceived`: タイムコード受信時
- `OnTimecodeJump`: タイムコードジャンプ検出時
- `OnSignalLost`: LTC信号喪失時
- `OnSignalRestored`: LTC信号復旧時

## 🎛️ 推奨設定

### クリーンなLTCソース（ハードウェアジェネレータ）
```
Jitter Threshold: 0.05 (50ms)
Denoising Strength: 0.5
Min Consecutive Valid Frames: 2
```

### ノイジーなLTCソース（テープ/ワイヤレス）
```
Jitter Threshold: 0.15 (150ms)
Denoising Strength: 0.8-1.0
Min Consecutive Valid Frames: 3-4
```

### 開発/テスト環境
```
Enable Debug Mode: ON
Log Debug Info: ON
Log To Console: OFF (パフォーマンスのため)
```

## 🔍 トラブルシューティング

### タイムコードが表示されない
1. オーディオ入力デバイスが正しく選択されているか確認
2. LTC信号が入力されているか確認
3. フレームレートとドロップフレーム設定を確認

### タイムコードが不安定
1. `Jitter Threshold`を上げる
2. `Denoising Strength`を上げる
3. `Min Consecutive Valid Frames`を増やす

### Timeline同期が動作しない
1. PlayableDirectorが正しく設定されているか確認
2. `Sync Threshold`を調整
3. TimelineAssetが設定されているか確認

## 📊 パフォーマンス最適化

### ログ設定
- `Log To Console`は**必ずOFF**にする（大きなパフォーマンス影響）
- 必要な時のみ特定のログカテゴリを有効化
- Inspector内のDebug Logsセクションでログを確認

### バッファサイズ
- レイテンシと安定性のトレードオフ
- 推奨: 512-1024サンプル

## 🛠️ 開発者向け情報

### ビルド設定
- マイクロフォン権限を有効化
- サンプルレート: 48000 Hz推奨
- プラットフォーム固有の設定に注意

### 拡張開発
新機能追加時の注意点:
1. `ValidateTimecode`ロジックへの影響を確認
2. クリーン/ノイジー両方のLTCソースでテスト
3. ログがパフォーマンスに影響しないことを確認
4. このドキュメントを更新

## 📋 動作環境

- Unity 2021.3 LTS以降
- Windows / macOS / Linux
- マイクロフォン入力デバイス

## 📄 ライセンス

MIT License

## 🤝 コントリビューション

Issue報告やPull Requestは[GitHubリポジトリ](https://github.com/iridescent-jp/Unity_LTCDecoder)にお願いします。

## 📝 更新履歴

### v1.2.0 (2025-08-31)
- 外部制御API拡充
- PlayableDirector参照の柔軟化
- Inspector自動更新機能
- 設定永続化システム改善

### v1.1.0 (2025-08-30)
- Unity Package Manager対応
- イベントシステム再設計
- デバッグUI改善

### v1.0.0
- 初回リリース
- 基本的なLTCデコード機能
- Timeline同期機能

---

開発元: [Iridescent](https://iridescent.jp)