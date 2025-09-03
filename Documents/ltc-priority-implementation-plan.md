# Unity LTC Decoder: 複数マシン同時性のための優先度付き実装計画（ユーザー設定非依存）

## 目的
- 複数マシンで、同一の入力LTCに対して可能な限り同一時刻の出力・イベント発火・Timeline同期を実現する。
- ユーザーの手動設定に依存せず、コードと設計で担保できる範囲のみ対象とする。

## 優先度順タスク（High → Low）

1) Drop Frame/NDF の厳密換算（絶対フレーム基準へ移行）
- 29.97DF を 29.97fps 実数で扱う近似を廃止し、DF規則（毎分2フレーム除外、10分ごと例外）に基づく厳密往復換算を実装。
- 内部表現を `long absoluteFrame`（24hロール）に統一し、表示時のみ文字列化。

2) フレームレート/DF参照の一本化（消費側の固定値排除）
- `LTCDecoder` から `FrameRate`/`DropFrame`/`CurrentAbsoluteFrame` を公開し、`LTCTimelineSync` はこれを参照。
- 可能なら秒換算ではなくフレーム基準で同期する。

3) イベント発火の単一ステートマシン化（ヒステリシス付き）
- 開始は新規フレーム受信で即時、停止は連続未デコード `> timeout` で確定。発火は単一箇所からのみ。

4) GC/ジッタ要因の排除（安定化）
- `AnalyzeBuffer` の LINQ を手書きループ化、`Queue` を固定長リングバッファ化、`audioBuffer` を先行確保してアロケーションゼロ化。

5) フレーム量子化に基づくイベント判定
- 秒の浮動小数ではなく「±許容フレーム」で判定。DF/NDFでも決定的に一致。

6) メタデータ拡張（下流の決定性向上）
- `LTCEventData` に `double dspTimestamp` と `long absoluteFrame` を追加して伝搬。

7) PlayableDirector の駆動方式見直し
- 可能なら `DirectorUpdateMode.DSPClock` を採用。不可の場合も将来フレーム到達DSP秒に合わせて `Evaluate()` を予約実行。

## 実装順序（フェーズ）
- Phase 1: (1) DF厳密換算 → (2) 参照一本化 → (3) 発火単一路
- Phase 2: (5) フレーム量子化 → (6) メタ拡張
- Phase 3: (4) GC/ジッタ削減（並行可） → (7) Director駆動見直し

## 受け入れ基準（共通）
- DF/NDF 往復換算: 1時間相当で往復誤差0フレーム。
- Timeline同期: 29.97DF/25/24/30 の各設定で、1分再生時の差分が±1フレーム以内。
- イベント発火: 断続試験で二重発火・欠落なし、ログ順序が決定的。
- 安定性: 10分連続で通常フレームのGC割当が0Bに近似。
- マルチマシン: 5回試験で同時イベント差が±1フレーム以内に収束。

## 検証計画（抜粋）
- オフライン: `TimecodeEncoder` 生成波形でDF/NDF変換の往復テスト。
- 実機: 2台以上に同一LTCを分配し、`absoluteFrame` と `dspTimestamp` の差分分布を比較。
- Timeline: 各フレームレートのアセットで再生/停止/ジャンプ/長時間を検証。

## 変更対象・影響
- Runtime: `LTCDecoder.cs`, `LTCDecoderEvents.cs`（フレーム換算・メタ拡張・バッファ最適化）
- Editor/Tooling: `LTCTimelineSync.cs`（参照の一本化、必要に応じて駆動見直し）

## 参照
- DSP時刻スタンプ導入の詳細: `Documents/ltc-sync-dsp-timestamp-plan.md`
- タスク登録（実行順の目安）: `TODO.md` の「待機中 (Pending)」

## 更新履歴
- 2025-09-04: 内容を再作成（目的/優先度/検証/参照を明確化）
