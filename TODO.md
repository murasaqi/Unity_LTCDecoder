# Unity LTC Timeline - タスク管理

## 📋 現在のタスク

### 🔥 進行中 (In Progress)
（なし）

### ⏳ 待機中 (Pending)
【Phase H: LTCDecoder から Debug GUI を作成】
1. [ ] インスペクタに「Debug Tools」セクションを追加
   - 目的: LTCDecoder 選択時に Debug GUI 作成/更新の導線を提供する。
   - 内容: Inspector に Create / Update / Open Docs ボタンと既存UIの有無表示（Select）を追加。
   - 受け入れ基準: ボタンが表示され、クリック可能。既存UIがあれば名前表示とSelect動作。
   - 影響: `jp.iridescent.ltcdecoder/Editor/LTCDecoderEditor.cs`
   - 参照: `Documents/ltc-debug-gui-create-from-component.md`

2. [ ] Create ボタン: 既存 UI がなければ新規作成（Undo 対応）
   - 目的: ワンクリックで Debug GUI を生成。
   - 内容: `LTCTimelineSyncDebugSetup.CreateTimelineSyncDebugUI()` を呼ぶ。既存がある場合は重複を防ぎダイアログで案内。
   - 受け入れ基準: 既存なし→生成＆選択＆Undo 可能。既存あり→重複せず案内表示。
   - 影響: `LTCDecoderEditor.cs`
   - 参照: `Documents/ltc-debug-gui-create-from-component.md`

3. [ ] Update ボタン: 既存 UI の再構成（レイアウト刷新を適用）
   - 目的: 旧バージョンのDebug UIを最新レイアウトへ更新。
   - 内容: `LTCTimelineSyncDebugSetup.UpdateTimelineSyncDebugUI()` を呼ぶ。
   - 受け入れ基準: 既存UIの構造が更新される（ツールバー/スクロール等が反映）。
   - 影響: `LTCDecoderEditor.cs`
   - 参照: `Documents/ltc-debug-gui-layout-refactor.md`, `Documents/ltc-debug-gui-create-from-component.md`

4. [ ] Open Docs ボタン: ドキュメントを開く
   - 目的: 実装仕様/使い方をすぐ参照できるようにする。
   - 内容: ローカル `Documents/ltc-debug-gui-layout-refactor.md` またはリポジトリURLを `Application.OpenURL` で開く。
   - 受け入れ基準: クリックでドキュメントが開く。
   - 影響: `LTCDecoderEditor.cs`
   - 参照: `Documents/ltc-debug-gui-create-from-component.md`
（なし）

### ✅ 完了済み（Phase G: Debug GUI レイアウト刷新）（2025-09-05）
1. [x] パネルのアンカー/ピボット/横幅計算の適用
   - 目的: 画面右固定・縦ストレッチで高さ追従、横幅はビュー幅比（30%）で決定。
   - 内容: Panel RectTransform を `anchorMin(1,0) / anchorMax(1,1) / pivot(1,1)` に設定。生成時に `sizeDelta.x = clamp(viewWidth*0.3, 320, 560)` を計算して適用。
   - 受け入れ基準: ウィンドウ高さに追従し、横幅が最小/最大の範囲で適切に決定される。
   - 影響: `jp.iridescent.ltcdecoder/Editor/LTCTimelineSyncDebugSetup.cs`
   - 参照: `Documents/ltc-debug-gui-layout-refactor.md`

2. [x] Header（ツールバー）実装とボタン最適化
   - Clear/Export/Copy をコンパクトに右寄せ配置
   - HorizontalLayoutGroupでTitle + Flexible Space + ボタン配置

3. [x] ScrollRect/Viewport/Content 構造への再構成
   - Panel直下にScrollRect配置、Viewport（Mask+Image）とContent（VLG+CSF）構造
   - 長文/多数要素でも縦スクロール可能

4. [x] CreateCompleteUIメソッドを新レイアウト構造に更新
   - Content内に各セクション（Timecode、Audio Settings、Timeline Sync、Debug）を配置
   - セクションタイトル、セパレータ、水平グループレイアウト実装

5. [x] SetupUIControllerの参照更新
   - 新レイアウト構造（ScrollRect/Viewport/Content）に対応
   - Header内ボタン参照、Content内各UI要素参照を更新

6. [x] Debug Message Boxのスクロール対応（2025-09-05）
   - Debug Message Boxに200px固定高さ制限を設定
   - ScrollRect/Viewport/Content構造で無制限な高さ増加を防止
   - RectMask2Dで効率的な描画最適化

7. [x] Inspector表示のクリーンアップ（2025-09-05）
   - "Phase F"などの開発フェーズ表記を全て削除
   - LTCTimelineSync, LTCDecoder, LTCDecoderEventsから計17箇所の不要な表記を削除
   - プロフェッショナルな表示に改善

### ✅ 完了済み (Completed) - 改善
【Phase I: Timeline 再開時コード修正（最小修正）（2025-09-06）】
1. [x] 再生順序を修正（time → Evaluate → Play）
   - 再開直後に過去時刻が一瞬描画される"フラッシュ"を確実に防止
   - LTC開始検知ブロックで正しい順序を確認・維持
2. [x] 基準TCを DecodedTimecode 優先に変更（Current はフォールバック）
   - 受信生値に揃えて開始し、ロック前自走誤差の持ち越しを排除
   - 既存実装で対応済みを確認
3. [x] 秒換算を Decoder の FPS/DF 準拠に（30固定廃止）
   - ParseTimecodeToSecondsでLTCDecoderのユーティリティを使用済み
   - 24/25/29.97(DF/NDF)/30 いずれでも正しく動作
4. [x] 目標時刻のクランプと観測リセット
   - targetTime = Mathf.Clamp()で範囲制限を追加
   - isDrifting=false, driftStartTime=0fでリセット済み
5. [x] DSPClock 採用
   - デフォルトでDirectorUpdateMode.DSPClockを設定
   - OnEnable()で確実に適用

【LTC再開時のタイミング改善（2025-09-05）】
1. [x] LTC停止時の内部時刻リセット
   - LTCDecoder.csでinternalTcTime = 0を追加
   - 古いタイムコード値が残らないように修正
2. [x] 同期パラメータの最適化
   - syncThreshold: 0.5秒 → 0.033秒（約1フレーム）
   - continuousObservationTime: 1.0秒 → 0.1秒
   - 最小値を0.01秒に変更（従来は0.1秒）
3. [x] LTC再開時の待機処理
   - 100ms待機して安定したLTC信号を取得
   - PerformInitialSync()で確実な同期を実行

### ✅ 完了済み (Completed) - 不具合是正
【不具合是正：LTC再開時のフラッシュ防止（最小修正）（2025-09-05）】
1. [x] 再開時の同期順序を修正（time → Evaluate → Play）
   - 目的: 再Play直後に過去状態や別時刻が一瞬描画される現象を防ぐ。
   - 内容: LTC開始検知ブロックで `PlayableDirector.time = targetSec;` → `PlayableDirector.Evaluate();` → `PlayableDirector.Play();` の順序に統一。
   - 受け入れ基準: Play/Stopを10回繰り返しても再Play直後にフラッシュ（大きなズレ描画）が発生しない。
   - 影響: `jp.iridescent.ltcdecoder/Runtime/Scripts/LTCTimelineSync.cs`
   - 参照: `Documents/ltc-hard-resync-on-restart.md`

2. [x] 再開時に `DecodedTimecode` を優先使用（`CurrentTimecode` はフォールバック）
   - 目的: 直前までの許容ドリフトを持ち越さず、受信生TCに揃えて開始する。
   - 内容: 再開時の基準TCは `!string.IsNullOrEmpty(DecodedTimecode) ? DecodedTimecode : CurrentTimecode` で選択。
   - 受け入れ基準: 再Play直後の `|director.time - (LTC秒+offset)|` が常に1フレーム相当以下。
   - 影響: `LTCTimelineSync.cs`
   - 参照: `Documents/ltc-hard-resync-on-restart.md`

3. [x] 秒換算でDecoderのFPS/DFを参照（30fps固定を廃止）
   - 目的: 25/24/29.97(DF/NDF)/30 いずれでも開始地点の秒換算が正しくなるようにする（大ズレ回避）。
   - 内容: `ParseTimecodeToSeconds`（または相当処理）で `decoder.GetActualFrameRate()` と `decoder.DropFrame` に準拠して換算する（厳密DF換算は別タスク）。
   - 受け入れ基準: 各フレームレート素材で再Play直後の大ズレが発生しない。
   - 影響: `LTCTimelineSync.cs`
   - 参照: `Documents/ltc-timeline-sync-improvement.md`, `Documents/ltc-hard-resync-on-restart.md`

4. [x] 再開直後にドリフト観測状態をリセット
   - 目的: 直前の観測状態を持ち越さず、再開後の同期判定を安定化させる。
   - 内容: 再Play直後に `isDrifting=false; driftStartTime=0f;` を明示セット。
   - 受け入れ基準: 再Play直後の補正判定が即時に誤発動しない（観測リセットが効いている）。
   - 影響: `LTCTimelineSync.cs`
   - 参照: `Documents/ltc-hard-resync-on-restart.md`

### ✅ 完了済み (Completed) - Phase F
【Phase F: 再Play時のハード同期（DSPClock前提）（2025-09-05）】
1. [x] 設定項目の追加（LTCTimelineSync）
   - 目的: 再Play時の同期動作を制御するスイッチを用意する。
   - 内容: `hardResyncOnLTCStart`（既定ON）、`useDspGateOnStart`（既定OFF）、`snapToTimelineFps`（既定OFF）を `LTCTimelineSync` に追加。
   - 受け入れ基準: Inspectorで設定可能、既定値で従来動作に影響なし。
   - 影響: `jp.iridescent.ltcdecoder/Runtime/Scripts/LTCTimelineSync.cs`
   - 参照: `Documents/ltc-hard-resync-on-restart.md`, `Documents/ltc-timeline-sync-improvement.md`
2. [x] 再Play検知時のハード同期ロジックを実装
   - 目的: 再Play開始時にLTC準拠の時刻へ“ピタッと”合わせてから再生する。
   - 内容: `DecodedTimecode` を優先してLTC準拠の秒へ換算（DecoderのFPS/DF準拠）。`PlayableDirector.time=targetSec; Evaluate(); Play();` の順に実行。直後に `isDrifting=false; driftStartTime=0f;` を明示リセット。
   - 受け入れ基準: 再Play直後の位相差が1フレーム相当以下、10回反復でも累積せず。
   - 影響: `LTCTimelineSync.cs`
   - 参照: `Documents/ltc-hard-resync-on-restart.md`, `Documents/ltc-drop-frame-conversion.md`
3. [x] 将来ゲートへの予約Evaluate/Play（任意・有効時）
   - 目的: 複数PCで開始位相をさらに一致させる。
   - 内容: 直近LTCの“絶対秒”と“DSP刻印”から `dsp_gate` を算出し、その時刻に `time/Evaluate/Play` を予約実行。DSPClock未対応環境では近似予約。
   - 受け入れ基準: 複数PC同時試験で開始差が±1フレーム以内に収束。
   - 影響: `LTCTimelineSync.cs`（スケジューラ処理）
   - 参照: `Documents/ltc-hard-resync-on-restart.md`, `Documents/ltc-sync-dsp-timestamp-plan.md`
4. [x] TL-FPSスナップ（任意・有効時）
   - 目的: フレーム境界厳守が必要なコンテンツでの安全な同期。
   - 内容: `TimelineAsset.editorSettings.fps` を取得し、`dt=1/fps` に丸めてから `Evaluate/Play` を実行するオプション。
   - 受け入れ基準: スナップON時、境界上で同期される（視覚的破綻なし）。OFF時は連続値同期。
   - 影響: `LTCTimelineSync.cs`
   - 参照: `Documents/ltc-hard-resync-on-restart.md`
5. [x] 測定用ログと検証シナリオの追加
   - 目的: 再Play直後の位相差を定量評価し、回帰を防ぐ。
   - 内容: `director.time` と `LTC絶対秒+offset` の差分を再Play直後に記録。単体（10回反復）・複数PC（5回）で統計化。
   - 受け入れ基準: 受け入れ基準を満たすログが取得できる。
   - 影響: `LTCTimelineSync.cs`（ログ）/ ドキュメント（検証手順）
   - 参照: `Documents/ltc-hard-resync-on-restart.md`

### ✅ 完了済み (Completed)
【駆動方式の見直し（2025-09-03）】
- [x] DSPClockモード対応の実装
- [x] DirectorUpdateMode選択可能に変更
- [x] LateUpdateでの微調整処理追加（1フレーム以内の誤差補正）
- [x] updateMode設定とメソッドの公開API追加

【Phase E: GC/ジッタ削減（安定化）（2025-09-03）】
- [x] `AnalyzeBuffer` のLINQ除去（手書きループ化）
- [x] バッファを固定長リング化・先行確保
- [x] audioBufferの最大サイズ事前確保（再割り当て防止）
- [x] リングバッファ実装によるGC削減

【Phase D: イベントの決定化と最適化（2025-09-03）】
- [x] 開始/停止イベントの単一ステートマシン化（ヒステリシス導入）
- [x] イベント判定のフレーム量子化（絶対フレーム基準）
- [x] `LTCEventData` メタ拡張（dspTimestamp/absoluteFrame追加）
【Phase C: 参照一本化とTimeline同期改善（2025-09-03）】
- [x] `GetActualFrameRate()` 公開（公開メソッドの追加）
- [x] `GetNominalFrameRate()` 公開（DF計算用）
- [x] `IsDropFrame`/`FrameRateMode` プロパティ追加
- [x] `LTCTimelineSync` の30fps固定排除（Decoder参照化）

【Phase B: DF/NDF 厳密換算とフレーム基準化（2025-09-03）】
- [x] 変換ユーティリティ追加: 文字列↔絶対フレーム（DF対応）
- [x] 秒↔フレーム変換の窓口統一（内部はフレーム優先）

【Phase A: DSP時刻スタンプ導入（2025-09-03）】
- [x] フィールド追加: `micStartDspTime/wrapCount/clipSamples/lastSegmentEndDsp`
- [x] 較正ロジック実装: 録音開始直後に `micStartDspTime` を確定
- [x] ラップ検出と絶対サンプル番号: `wrapCount` 運用
- [x] `ProcessAudioSegment`で `lastSegmentEndDsp` を更新
- [x] デコード結果の刻印差し替え: `dspTime = lastSegmentEndDsp`
- [x] READMEを日本語/英語版で更新（画像付き）
- [x] README内の専門用語説明と文体を改善
- [x] ビルド設定セクションの改善（マイクロフォン権限とサンプルレート説明）
- [x] クイックスタートセクションの改善とメニュー画像追加
- [x] LTC DecoderとTimeline同期機能の説明を分離
- [x] LTCTimelineSync.png画像をREADMEに追加
- [x] GitHubリポジトリURLを正しいものに修正
- [x] LTC Timeline Syncの外部制御API拡充
- [x] LTC Timeline SyncコンポーネントのPlayableDirector参照を柔軟化
- [x] Play終了後のInspector自動リフレッシュ処理を実装
- [x] Play終了後のInspector/UI表示の更新処理を実装
- [x] 設定管理システムの完全な再実装（UI/Inspector両対応）
- [x] LTC Decoder初回インスタンス化時の録音開始問題を修正
- [x] UI設定の永続化バグ修正（ドロップダウンの初期値同期問題）
- [x] UIステータス表示をOutput TC形式に変更、設定永続化機能追加
- [x] タイムコードとステータスの2段表示対応
- [x] UIレイアウト再配置（タイムコードを最上部、設定を中段に配置）
- [x] Menu追加時のUIレイアウトも1行表示に対応
- [x] UIレイアウトの最適化（タイムコード表示を1行に統合、固定幅フォント対応）
- [x] LTCUIControllerのコンパイルエラー修正（未定義変数decodedTimecodeTextの参照を削除）
- [x] Package化して、開発しているUnityプロジェクトとPackageを分離管理
- [x] CLAUDE.md作成と開発ルール追加
- [x] 言語ルールの追加（日本語対話・コメント）
- [x] LTCデコーダーの安定性改善
- [x] タイムコード検証ロジックの修正
- [x] ログシステムのパフォーマンス最適化
- [x] Inspector UIの強化
- [x] LTC Event Debuggerシステムの再設計と実装
- [x] LTC Debug UIの文字視認性改善
- [x] CLAUDE.mdの開発指針を最上部に移動（既に配置済み）
- [x] Debug Message Container問題の修正（nullチェック追加・手動参照設定対応）

---

## 🎯 今後の改善案

### 高優先度
1. **パフォーマンス最適化**
   - バッファ処理の更なる効率化
   - メモリアロケーションの削減

2. **機能拡張**
   - 複数のLTCソース対応
   - タイムコードオフセット機能
   - リモート制御API

### 中優先度
1. **UI/UX改善**
   - タイムコード表示のカスタマイズ
   - ジッター統計のエクスポート機能

2. **テスト強化**
   - 自動テストの追加
   - 各種LTCフォーマット対応テスト

### 低優先度
1. **ドキュメント**
   - API リファレンス
   - サンプルプロジェクト

---

## 📝 開発メモ

### 既知の問題
- なし（現時点）

### 注意事項
- Console出力は必ずフラグで制御（パフォーマンス影響大）
- タイムコード検証は用途に応じて調整が必要
- Unity 2021.3 LTS以降での動作を推奨

---

## 🔄 更新履歴

### 2025-09-01 (7)
- GitHubリポジトリURLを正しいものに修正
  - Unity Package ManagerのGit URLを修正 (iridescent-jp → murasaqi)
  - コントリビューションセクションのリポジトリURLも修正
  - 日本語版・英語版の両方を修正

### 2025-09-01 (6)
- LTCTimelineSync.png画像をREADMEに追加
  - LTCTimelineSyncコンポーネントのInspector画像を追加
  - 日本語版・英語版の両方のREADMEに画像を配置
  - コンポーネントの設定画面を視覚的に分かりやすく

### 2025-09-01 (5)
- LTC DecoderとTimeline同期機能の説明を分離
  - LTCデコード機能をメインとして説明
  - Timeline同期はLTC Decoderに依存した追加機能として説明
  - 日本語版・英語版の両方を修正

### 2025-09-01 (4)
- クイックスタートセクションの改善とメニュー画像追加
  - 「再生」を「動作確認」に変更し、より自然な説明に
  - LTC信号の入力方法を具体的に説明
  - メニューからのセットアップ画像(Menu.png)を追加
  - 日本語版・英語版の両方を同様に修正

### 2025-09-01 (3)
- ビルド設定セクションの改善
  - マイクロフォン権限について各プラットフォーム別の具体的な設定方法を追加
  - サンプルレートを固定値ではなく環境に応じた設定を促す説明に変更
  - Windows/macOS、iOS、Androidそれぞれの権限設定方法を明記

### 2025-09-01 (2)
- README内の専門用語説明と文体を改善
  - LTC、ジッター、PlayableDirectorなどの専門用語に簡潔な説明を追加
  - API機能のコードブロックを削除
  - 推奨設定セクションを削除し、トラブルシューティングに統合
  - 文体を「〜です」「〜してください」の丁寧語で統一
  - 不安定な信号への対処法を具体的な数値と共に説明

### 2025-09-01
- READMEを日本語/英語版で更新
  - 日本語版README.mdをメインに配置
  - 英語版README_EN.mdへのリンクを追加
  - Documents/ディレクトリの画像を埋め込み
  - Inspector ViewとDebug UIのスクリーンショットを追加
  - 最新の実装状況に合わせて内容を更新
  - API使用例、推奨設定、トラブルシューティング等を充実

### 2025-08-31 (13)
- LTC Timeline Syncの外部制御API拡充
  - SetTimeline()メソッド：TimelineAssetを動的に設定
  - SetTimelineAndDirector()メソッド：Timeline＋Director同時設定
  - GetTimeline()/GetPlayableDirector()：現在の設定を取得
  - SetBinding()メソッド：Timelineトラックのバインディング設定
  - SetTimelineOffset()/GetTimelineOffset()：オフセット管理
  - SyncThreshold/ContinuousObservationTimeプロパティ：同期パラメータの動的制御

### 2025-08-31 (12)
- LTC Timeline SyncコンポーネントのPlayableDirector参照を柔軟化
  - RequireComponent制約を削除
  - PlayableDirectorをPublicフィールドで手動設定可能に
  - 別GameObjectのPlayableDirectorも参照可能に
  - SetPlayableDirector()メソッドを追加
  - 後方互換性を維持（同一GameObject構成も引き続きサポート）

### 2025-08-31 (11)
- Play終了後のInspector自動リフレッシュ処理を実装
  - LTCDecoderEditorにPlayMode終了検知処理を追加
  - InspectorWindowを直接Repaint()で更新
  - 選択中のLTCDecoderオブジェクトを再選択して確実に更新
  - OnEnableでSerializedObjectを再取得
  - Play終了後も最新のInspector表示を維持

### 2025-08-31 (10)
- Play終了後のInspector/UI表示の更新処理を実装
  - PlayModeStateChange.EnteredEditModeイベントで設定を復元
  - LoadSettingsForEditor()メソッドでInspectorを更新
  - SerializedObjectを使用して確実にInspectorに反映
  - UIコントローラーもEditMode復帰時に更新
  - Play終了後も最終状態が表示されるように改善

### 2025-08-31 (9)
- 設定管理システムの完全な再実装
  - 状態管理フラグ（isInitialized, isDirtyFromRuntime）を追加
  - OnValidateを修正して初期化前の処理を防止
  - Play Mode終了時の設定保存処理を追加（Editor限定）
  - UI同期処理にSetValueWithoutNotifyを使用してループ防止
  - 設定の妥当性チェック機能を追加
  - エラーハンドリングを強化
  - UI/Inspectorどちらから変更しても確実に永続化

### 2025-08-31 (8)
- LTC Decoder初回インスタンス化時の録音開始問題を修正
  - OnEnable()でPlay中は自動的に録音を開始するよう改善
  - デバイスが未設定の場合は利用可能な最初のデバイスを自動選択
  - SetDevice()メソッドをPlay中は常に録音を開始するよう修正
  - StartRecording()にデバイス自動選択のフォールバック処理追加
  - Menuから追加直後でも正常に動作するように改善

### 2025-08-31 (7)
- UI設定の永続化バグ修正
  - LTCUIControllerのドロップダウン初期化処理を改善
  - 初期化時にリスナーを一時的に無効化して不要なイベント発火を防止
  - 保存された設定値を正しくUIに反映するように修正
  - デバイスが見つからない場合のフォールバック処理追加
  - Start()メソッドでLTCDecoderのみの場合もUI初期化を実行

### 2025-08-31 (6)
- UIステータス表示をOutput TC形式に変更
  - `[REC]` → `Output TC: Running`
  - `[---]` → `Output TC: Stopped`
  - LTCUIController.csでIsRunningプロパティを使用
- Audio Input Settings永続化機能実装
  - PlayerPrefsを使用した統一実装（Editor/ビルド共通）
  - 設定キー: LTCDecoder.Device, FrameRate, SampleRate, DropFrame
  - Awakeで設定読み込み、変更時に自動保存
  - OnDestroy, OnApplicationPause, OnApplicationFocusで保存
  - Editor専用: Reset Settingsコンテキストメニュー追加

### 2025-08-31 (5)
- タイムコードとステータスの2段表示実装
  - ヘッダー "LTC Decoder Debug UI" を復活
  - タイムコード（1段目）：Y=-70、フォントサイズ28、タイムコードのみ表示
  - ステータス（2段目）：Y=-105、フォントサイズ16、[REC] ████████░░ 85% 形式
  - LTCUIController.csに statusLineText フィールド追加
  - UpdateTimecodeDisplay()を2段表示対応に修正

### 2025-08-31 (4)
- UIレイアウト再配置
  - ヘッダー "LTC Decoder Debug UI" を削除
  - Timecode Display ラベルを削除
  - タイムコードを最上部に大きく配置（Y=-50、フォントサイズ24）
  - Audio Settings を中段に配置（Y=-110〜-200）
  - ボタン位置調整（Y=-250）
  - スクロールビュー拡大（高さ390px）

### 2025-08-31 (3)
- Menu追加時のUIレイアウトも1行表示に対応
  - LTCDebugSetupWithUI.csのCreateCompleteUI()メソッドを修正
  - 統合表示フォーマット: `[---] 00:00:00:00 | ░░░░░░░░░░ 000%`
  - 不要なUI要素は互換性のため非表示で作成
  - ボタンとスクロールビューの位置を調整（上に移動、スクロールビューを拡大）

### 2025-08-31 (2)
- UIレイアウトの最適化実装
  - タイムコード、ステータス、シグナルレベルを1行に統合表示
  - 固定幅フォント（Consolas）を使用してタイムコード表示位置を固定
  - フォーマット: `[REC] 01:23:45:12 | ████████░░ 85%`
  - UpdateTimecodeDisplay()メソッドに全表示処理を統合
  - 画面スペースを4行から1行に削減
- LTCUIControllerのコンパイルエラー修正（未定義変数decodedTimecodeTextの参照を削除）

### 2025-08-31
- Debug Message Container問題を修正
- LTCEventDebuggerにnullチェック追加（ltcDecoderが未設定でも動作可能に）
- LTCUIControllerの初期メッセージ表示を改善（コルーチンで確実に表示）
- LTCDebugSetupWithUIでLTCDecoderの参照を自動設定

### 2025-08-30
- LTC DecoderをUnity Package Manager対応パッケージとして独立
- jp.iridescent.ltcdecoderとしてパッケージ化
- 名前空間をjp.iridescent.ltcdecoderに統一
- Assembly Definitionファイル追加
- README、LICENSE、CHANGELOG作成
- サンプルシーン・UI整理
- GitHubリポジトリ名をUnity_LTCDecoderに変更予定

### 2025-08-29
- LTC Event Debuggerシステムの完全再設計
- LTCEventDebuggerとLTCDecoderUIに役割分離
- UnityEvent対応のデバッグメッセージ機能追加
- LTC Debug UIの文字視認性改善（背景色調整）
- CLAUDE.md確認（開発指針は既に最上部に配置済み）

### 2024-08-28
- TODO.mdファイル作成
- タスク管理を独立ファイル化
- 今後の改善案を整理

### 以前の更新
- LTCデコーダーコンポーネント完成
- Timeline同期機能実装
- ジッター除去・デノイズ機能追加
- ログシステム改善
