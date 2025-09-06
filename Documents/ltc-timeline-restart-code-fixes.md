# Timeline 再開時の一瞬の過去シーク発生に対するコード修正計画（最小修正）

## 目的
- 症状: LTC 再生開始（または再開）直後、Timeline が一瞬だけ過去の時間に描画され、その後に LTC の時刻へ同期して見える現象を除去する。
- 方針: 実装上の不具合（順序・参照元・換算）に限定して修正する。挙動の決定性と見た目の安定性を最小差分で向上させる。

## 対象ファイル
- `jp.iridescent.ltcdecoder/Runtime/Scripts/LTCTimelineSync.cs`

## 修正点（必須）
1) 再生順序の是正（time → Evaluate → Play）
- 修正前（例）:
  - `director.time = targetSec;`
  - `director.Play();`
- 修正後（必須）:
  - `director.time = targetSec;`
  - `director.Evaluate();`
  - `director.Play();`
- ねらい: 最初の描画フレームで目標時刻を確定させ、直前の評価結果（過去時刻）が一瞬表示される“フラッシュ”を防止する。

2) 基準 TC は DecodedTimecode を優先（Current はフォールバック）
- 修正前（例）: `var tc = ltcDecoder.CurrentTimecode;`
- 修正後（必須）:
  - `var tc = !string.IsNullOrEmpty(ltcDecoder.DecodedTimecode) ? ltcDecoder.DecodedTimecode : ltcDecoder.CurrentTimecode;`
- ねらい: 受信直後のロック前自走誤差を持ち越さず、“受信生値”に合わせて開始する。

3) 秒換算に Decoder の FPS/DF を使用（30 固定廃止）
- 修正前（例）: `float frameRate = 30f;`
- 修正後（必須）:
  - `float frameRate = (ltcDecoder != null) ? /* Decoderの実FPS */ : 30f;`
  - `bool drop = (ltcDecoder != null) ? ltcDecoder.DropFrame : false;`
  - まずは FPS の参照を Decoder に統一（厳密 DF 換算自体は別タスクで対応可）。
- ねらい: 24/25/29.97(DF/NDF)/30 いずれの素材でも開始地点の秒換算が大きく外れないようにする。

## 修正点（併せて実施推奨）
4) 目標時刻のクランプとドリフト観測のリセット
- `targetSec = Mathf.Clamp(targetSec, 0f, (float)playableDirector.duration);`
- 再開直後:
  - `isDrifting = false;`
  - `driftStartTime = 0f;`
- ねらい: 端での不正シーク回避と、開始直後の誤判定抑止。

5) DSPClock の採用（可能なら）
- `playableDirector.timeUpdateMode = DirectorUpdateMode.DSPClock;`
- ねらい: `time` 変更の反映を描画フレームに依存させず、初期フレームの決定性を上げる（Evaluate と組み合わせて効果大）。

## 変更するコード位置（目安）
- `LTCTimelineSync.ProcessSync()` の「LTC 開始検知（isReceivingLTC && !wasReceivingLTC）」ブロック内:
  - 基準 TC の選択（Decoded 優先） → 秒換算（Decoder FPS/DF 準拠） → `time` 設定 → `Evaluate()` → `Play()` → ドリフト観測のリセット → 必要ならクランプ。
- `OnEnable()`（または初期化箇所）:
  - 可能なら `timeUpdateMode = DSPClock;` を設定。

## 擬似コード（置換の指針）
```
if (isReceivingLTC && !wasReceivingLTC)
{
    // 1) 基準TCの選択（Decoded優先）
    string tc = !string.IsNullOrEmpty(ltcDecoder.DecodedTimecode)
        ? ltcDecoder.DecodedTimecode
        : ltcDecoder.CurrentTimecode;

    // 2) 秒換算（DecoderのFPS/DFに準拠）
    float fps = GetFpsFromDecoderOrDefault(ltcDecoder); // 例: 24/25/29.97/30
    bool drop = (ltcDecoder != null) && ltcDecoder.DropFrame;
    double targetSec = LtcToSeconds(tc, fps, drop) + timelineOffset;

    // 3) 端でのクランプ（推奨）
    targetSec = Mathf.Clamp((float)targetSec, 0f, (float)playableDirector.duration);

    // 4) 順序: time → Evaluate → Play（必須）
    playableDirector.time = targetSec;
    playableDirector.Evaluate();
    playableDirector.Play();

    // 5) 観測リセット（推奨）
    isDrifting = false;
    driftStartTime = 0f;
}
```

## 受け入れ基準
- 同じ素材で Play/Stop を 10 回繰り返しても、再開直後に「一瞬過去の時刻が表示」されない。
- 再開直後の位相差 |director.time − (LTC 秒 + offset)| が常に 1 フレーム相当以下。
- FPS が 24/25/29.97(DF/NDF)/30 の素材でも現象が再現しない。

## テスト手順（簡易）
1. 複数フレームレート（24/25/29.97/30）の LTC を入力し、各々で Play/Stop を 10 回繰り返す。
2. 目視で再開瞬間の“過去に飛ぶ”現象が消えていることを確認する。
3. ログで再開直後の位相差（秒）を収集し、1 フレーム相当以下であることを確認する。

---
作成: 2025-09-04 / 担当: 開発
