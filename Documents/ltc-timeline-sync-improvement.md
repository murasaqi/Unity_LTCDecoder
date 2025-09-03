# LTCTimelineSync 改善詳細（設計・実装手順）

## 目的
- Timeline 同期の決定性と複数マシンでの同時性を向上させる。
- Decoder と参照を一本化し、30fps固定やDF非対応・秒基準の近似を排除する。

## 概要
- 参照値の統一: `LTCDecoder` からフレームレートとDF種別を取得し、`LTCTimelineSync` ではそれを唯一の真実とする。
- 同期の粒度: 秒ではなく“フレーム”を第一級として扱い、必要時のみ秒へ換算。
- 駆動方式: 可能なら `PlayableDirector.timeUpdateMode = DSPClock` を採用。不可の場合でも将来フレーム到達のDSP秒に基づき `Evaluate()` を予約実行。

## 公開API/プロパティ（追加・変更）
- `LTCDecoder`
  - 追加（公開）: `public float GetActualFrameRatePublic()`（既存の `GetActualFrameRate()` をpublic化でも可）
  - 既存利用: `public LTCFrameRate FrameRate { get; }`, `public bool DropFrame { get; }`
  - 将来: `public long CurrentAbsoluteFrame { get; }`（実装フェーズで段階導入）

- `LTCTimelineSync`
  - 変更: `ParseTimecodeToSeconds(string)` を `LTCDecoder` のフレームレート・DFに準拠して換算。
  - 追加（任意・将来）:
    - `public void SyncByAbsoluteFrame(long frame, float offsetSeconds = 0f)`
    - `public void SetFrameSource(LTCDecoder decoder)`（デコーダ参照設定の明示）

## 実装手順（ステップ）
1) 参照統一の準備
- `LTCDecoder` に `GetActualFrameRatePublic()` を追加（`GetActualFrameRate()` を public に変更でも良い）。
- `DropFrame` プロパティは既に公開済み。

2) `LTCTimelineSync` の秒→フレーム換算修正
- 既存: `ParseTimecodeToSeconds()` が30fps固定。
- 修正: `decoder != null ? decoder.GetActualFrameRatePublic() : 30f` を用い、DFは `decoder.DropFrame` に応じたロジックを適用。
  - 当面は近似（29.97f）でもよいが、最終的には `absoluteFrame` ベース同期へ移行（別タスク）。

3) 再生・同期の決定性向上
- 再生開始/再開時に `playableDirector.time = targetSeconds; playableDirector.Evaluate(); Play();` の順に統一。
- `Update()` 内での継続同期は、閾値超過が `continuousObservationTime` 継続したら `Evaluate()` を伴うジャンプで即座に合わせる。

4) 駆動方式の見直し（環境依存）
- Unityバージョンが対応していれば `DirectorUpdateMode.DSPClock` を試験導入。
- 未対応の場合は、将来フレーム到達のDSP秒を推定し `Evaluate()` を `EditorApplication.update`（Editor）かコルーチン（Runtime）で予約実行（近似でも決定性が向上）。

## 受け入れ基準
- 29.97DF/25/24/30 の各設定でTimeline同期誤差が±1フレーム以内に収束（1分再生時）。
- 再生/停止/シーク/ドリフト継続の各シナリオで同期動作が決定的（複数実行で同一ログ順序）。

## 検証
- デコーダの `CurrentTimecode` と Timeline の `time` をそれぞれ同フレームレートで表示し、差分をログ記録。
- ドリフト継続シナリオ（SyncThreshold超過→規定時間継続）で、予定通りジャンプが行われることを確認。

## 依存・参照
- 全体計画: `Documents/ltc-priority-implementation-plan.md`
- デコーダのDSP時刻刻印: `Documents/ltc-sync-dsp-timestamp-plan.md`

## 注意
- 最終的な“フレーム優先”同期（absoluteFrame基準）への置換は別タスクで進める。まずは参照の一本化と30固定廃止から。
