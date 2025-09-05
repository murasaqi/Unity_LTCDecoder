# LTC 再生再開時のハード同期（DSPClock 前提）実装計画

## 目的と要件
- 目的: LTCのPlay/Stopを繰り返したとき、再Play開始時に直前までの誤差を持ち越さず、いったんLTC準拠の時刻に“ピタッと”揃えてからTimelineを再生する。
- 前提:
  - Timeline は `PlayableDirector.timeUpdateMode = DSPClock` を推奨・使用する。
  - Timeline のFPS（TL-FPS）はコンテンツごとに異なるため固定しない。
  - 同期の基準は“秒（実時間）”。LTCはDF/NDF厳密換算で絶対秒を得る。

## 改善の基本方針
- 再Play開始時は「デコード直近のLTC準拠の秒」に“ハード同期”してから `Evaluate()` → `Play()` の順で開始する。
- 可能なら“将来ゲート（共通の未来のLTC時刻）”に向けてEvaluate/PlayをDSP時刻で予約実行し、複数PCの開始位相をさらに一致させる。
- 換算はDecoderのFPS/DFに準拠（DF/NDF厳密換算）。TL-FPSは参照のみ（必要ならスナップで丸める）。

## 追加設定（LTCTimelineSync に追加）
- `bool hardResyncOnLTCStart = true`（既定: ON）: 再Play時のハード同期を有効。
- `bool useDspGateOnStart = false`（既定: OFF）: 将来ゲートでのEvaluate/Play予約を有効。
- `bool snapToTimelineFps = false`（既定: OFF）: Timelineフレーム境界へのスナップを有効。

## 実装手順
1) Decoder参照の一本化
- 取得: `DecodedTimecode` / `GetActualFrameRate()`（public化）/ `DropFrame`。
- 可能ならDecoderから“絶対秒”取得APIを将来追加。

2) DF/NDF準拠の秒換算
- 再Play時の `targetSec = LtcToSeconds(decodedTC, fps, df) + timelineOffset`。
- 当面は近似可、最終的に厳密換算へ（参照: `Documents/ltc-drop-frame-conversion.md`）。

3) ハード同期（再Play検知時の順序）
- `director.time = targetSec; director.Evaluate(); director.Play();`
- 直後に `isDrifting=false; driftStartTime=0f;` を明示リセット。

4) 将来ゲート予約（任意・推奨）
- 直近LTCの“絶対秒”= `ltcSec_now`、そのDSP刻印= `dsp_now`。
- 境界例: 次の整数秒/次の+Nフレーム/固定ゲート間隔（0.5s/1.0s）。
- `dsp_gate = dsp_now + (ltcSec_gate - ltcSec_now)` を計算し、`dsp_gate` 到達時に `time/Evaluate/Play` を実行（予約）。
- DSPClock未使用時は `AudioSettings.dspTime` 監視で近似予約。

5) TL-FPSスナップ（任意）
- `timelineFps = TimelineAsset.editorSettings.fps`、`dt=1/timelineFps`。
- `targetSec = round(targetSec/dt)*dt` に丸めてから `Evaluate/Play`。

## 擬似コード（要点）
```csharp
if (hardResyncOnLTCStart && isReceivingLTC && !wasReceivingLTC)
{
    var tc = string.IsNullOrEmpty(decoder.DecodedTimecode)
        ? decoder.CurrentTimecode
        : decoder.DecodedTimecode;

    float fps = decoder.GetActualFrameRate();
    bool df = decoder.DropFrame;
    double ltcSec = LtcToSecondsUsingDecoder(tc, fps, df);
    double targetSec = ltcSec + timelineOffset;

    if (snapToTimelineFps)
    {
        double timelineFps = GetTimelineFps(playableDirector);
        double dt = 1.0 / timelineFps;
        targetSec = Math.Round(targetSec / dt) * dt;
    }

    if (useDspGateOnStart && TryGetDspStampedLtc(out double ltcNow, out double dspNow))
    {
        double ltcGate = GetNextGate(ltcNow); // 例: 次の整数秒
        double dspGate = dspNow + (ltcGate - ltcNow);
        ScheduleAtDspTime(dspGate, () => {
            director.time = ltcGate + timelineOffset;
            director.Evaluate();
            director.Play();
        });
    }
    else
    {
        director.time = targetSec;
        director.Evaluate();
        director.Play();
    }

    isDrifting = false;
    driftStartTime = 0f;
}
```

## 受け入れ基準（測定）
- 再Play直後の位相差（`director.time` vs `LTC絶対秒+offset`）が毎回1フレーム相当以下。
- Play/Stopを10回繰り返しても再Play時の位相差が累積しない。
- 複数PC同時試験で開始時の位相差が±1フレーム以内に収束。

## 参照
- DSP時刻スタンプ: `Documents/ltc-sync-dsp-timestamp-plan.md`
- DF/NDF厳密換算: `Documents/ltc-drop-frame-conversion.md`
- Timeline改善: `Documents/ltc-timeline-sync-improvement.md`
- 全体計画: `Documents/ltc-priority-implementation-plan.md`
