LTC 停止検出の高速化・決定化 実装計画（誰でも実装できる版）

## 目的
- 現状: LTC が Stop した際の検出が遅れ（最大 ~1 秒）、Stop イベントや Output TC 停止が遅延する。
- 目標: デコードが安定している前提で、Stop 検出をフレームスケール（≒100ms 以下）まで短縮し、誤検出を避けつつ決定的に停止させる。

## 方針（コアアイデア）
- 秒の固定タイムアウト依存を縮小し、オーディオ/DSP基準での「未更新フレーム数」と「連続サイレンス」を用いて停止を確定。
- 開始/停止イベントの発火を単一の状態機械に集約（重複/遅延を排除）。
- コルーチンのポーリング間隔を見直し、検出レイテンシを削減。
- しきい値は Inspector から調整可能にし、現場で素早くチューニングできるようにする。

## 変更対象
- ファイル: `jp.iridescent.ltcdecoder/Runtime/Scripts/LTCDecoder.cs`

## 追加/変更するフィールド（SerializeField 含む）
- 追加（SerializeField）
  - `float stopTimeoutSeconds = 0.2f;` 最終フォールバックの秒タイムアウト（現行 0.5s を短縮/可変化）
  - `int stopAfterMissingFrames = 3;` 未更新フレーム数で停止確定（例: 30fps なら ≒100ms）
  - `float silenceStopSeconds = 0.1f;` 連続サイレンスで停止確定（例: 100ms）
- 追加（内部）
  - `double lastDecodedDspTime;` 最後に LTC がデコードされた DSP 時刻
  - `int missedFrameCount;` 継続して未更新の LTC フレーム数（推定）
  - `double silenceStartDspTime; bool inSilence;` サイレンス連続時間の計測

## 実装手順（ステップ）
1) 最終タイムアウトの短縮と可変化
- `decodeTimeoutSeconds` を `stopTimeoutSeconds` に置換（SerializeField）。
- 役割はフォールバックのみ（未更新/サイレンスで通常は先に停止）。

2) デコード未更新のフレーム基準化
- `ProcessDecodedLTC()` で `lastDecodedDspTime = AudioSettings.dspTime; missedFrameCount = 0; inSilence = false;` を更新。
- `Update()` もしくは `CheckDecodeTimeout()` 内で
  - `double now = AudioSettings.dspTime;`
  - `double elapsed = now - lastDecodedDspTime;`
  - `missedFrameCount = (int)Mathf.Floor((float)(elapsed * GetActualFrameRate()));`
  - `missedFrameCount >= stopAfterMissingFrames` で停止確定（下記の停止関数を呼ぶ）。

3) 連続サイレンスでの早期停止
- `ProcessAudioSegment()` で区間最大振幅 `maxAmplitude <= signalThreshold` のとき
  - `if (!inSilence) { inSilence = true; silenceStartDspTime = AudioSettings.dspTime; }`
  - `else if (AudioSettings.dspTime - silenceStartDspTime >= silenceStopSeconds)` → 停止確定
- 振幅がしきい値を超えれば `inSilence = false` に戻す。

4) 停止確定の一元化関数
- 新規: `private void ConfirmStop(string reason)`
  - 処理: `hasSignal = false; isRunning = false; currentState = SyncState.NoSignal; isDecodingLTC = false;`
  - イベント: `OnLTCStopped`/`onLTCStopped` の発火（重複防止のガード）
  - デバッグ: 理由文字列をログ/Debugger へ
- 既存の停止経路（AnalyzeBuffer/Timeout/サイレンス/未更新フレーム）はすべて `ConfirmStop("...reason...")` を呼び出す。

5) コルーチン待機の微調整
- `ProcessAudioData()` の `yield return new WaitForSeconds(0.01f);` を
  - `yield return null;`（毎フレーム）+ 内部で 5ms 相当の時間が経過していなければスキップ、または
  - `yield return new WaitForSecondsRealtime(0.005f);`
- 目的: 停止検出のレイテンシを削る。ただし CPU と相談し、Profiler で負荷を確認。

6) 状態機械の一元化（開始/停止の責務）
- 開始: `ProcessDecodedLTC()` で初回デコード時に即時 `Started` 発火（重複防止）。
- 停止: 本ドキュメントの経路（未更新/サイレンス/フォールバック）に集約。
- 目的: 二重発火や遅延の温床をなくす。

## 擬似コード（抜粋）
```
// Update() or CheckDecodeTimeout()
var now = AudioSettings.dspTime;
var elapsed = now - lastDecodedDspTime;
var fps = GetActualFrameRate();
missedFrameCount = (int)Mathf.Floor((float)(elapsed * fps));
if (missedFrameCount >= stopAfterMissingFrames)
{
    ConfirmStop($"Missed {missedFrameCount} frames (~{elapsed:F3}s)");
}
else if (elapsed >= stopTimeoutSeconds) // フォールバック
{
    ConfirmStop($"Timeout {elapsed:F3}s");
}

// ProcessAudioSegment()
if (maxAmplitude <= signalThreshold)
{
    if (!inSilence) { inSilence = true; silenceStartDspTime = AudioSettings.dspTime; }
    else if (AudioSettings.dspTime - silenceStartDspTime >= silenceStopSeconds)
    {
        ConfirmStop($"Silence {AudioSettings.dspTime - silenceStartDspTime:F3}s");
    }
}
else
{
    inSilence = false;
}
```

## 受け入れ基準（測定可能）
- 安定した入力で Stop から停止イベントまでの遅延が 150ms 以下（目標 100ms 程度）。
- フォールス・ポジティブ（誤停止）が実運用で観測されない（短パルス/瞬断に耐える）。
- 連続 Play/Stop で停止イベントの二重/欠落がない。

## テスト手順
1) 合成 LTC（30fps）で Stop を任意タイミングに挿入し、停止イベントまでの経過をログで計測（10 回平均）。
2) 実機入力（異なる信号レベル）で同様の計測。`stopAfterMissingFrames` と `silenceStopSeconds` を調整し最短化。
3) 瞬断（50ms 程度の短い欠落）で停止しないことを確認（フォールス・ポジティブ対策）。

## 注意点
- stopAfterMissingFrames はフレームレートに依存。29.97/25/24 を考慮し、現場で微調整すること。
- サイレンス停止のしきい値は `signalThreshold` と組み合わせて過検出しないように（ノイズ環境で要調整）。
- コルーチン間隔縮小は負荷増に繋がるため、Profiler で確認しつつ適用する。

---
作成: 2025-09-04 / 担当: 開発
