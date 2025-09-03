# Unity LTC Decoder: DSP時刻スタンプによる同期精度向上（実装計画）

## 要約（TL;DR）
- 目的: マイク入力のサンプル到達時刻をDSP基準で厳密に推定し、各デコードTCに正確な`dspTime`を刻印して同期ジッタを排除する。
- 手段: `Microphone.GetPosition()`と`AudioSettings.dspTime`から録音0サンプルのDSP時刻を較正（`micStartDspTime`）し、サンプル番号→DSP時刻に写像。セグメント末端の絶対サンプルから`segmentEndDsp`を計算し、`ProcessDecodedLTC`で使用。
- 効果: コルーチン実行タイミング由来のジッタが消え、LTCと内部クロックの差分が安定・縮小。マシン間での同時性が向上。

## 背景と課題
- 現状は`ProcessDecodedLTC`内で「処理時点の`AudioSettings.dspTime`」を刻印。ポーリング/スレッドのジッタが混入。
- 複数マシンで同一LTC入力に対して同時刻出力を目指すには、サンプル基準の決定的な時刻付けが必要。

## 方針（サンプル→DSP時刻の写像）
1) 録音開始の較正
- `now = AudioSettings.dspTime`、`pos = Microphone.GetPosition(device)`
- `micStartDspTime = now - (double)pos / sampleRate`
- 任意サンプルiのDSP時刻: `micStartDspTime + i / sampleRate`

2) リングバッファのラップ追跡
- `currentPosition < lastSamplePosition`で`wrapCount++`
- 絶対サンプル: `absIndex = wrapCount * clip.samples + position`

3) セグメントのDSP時刻
- `endPos = (start + length) % clip.samples`
- `endAbs = wrapCount * clip.samples + endPos`
- `segmentEndDsp = micStartDspTime + (double)endAbs / sampleRate`

4) デコード結果の時刻刻印
- `LTCSample.dspTime = segmentEndDsp` としてバッファへ。
- 将来拡張: 同期ワード検出オフセット（サンプル数）を返せる場合は `segmentEndDsp - offset / sampleRate`。

5) 同期計算への反映
- `expectedTc = target.tcSeconds + (nowDsp - target.dspTime)` の`target.dspTime`に上記を使用。

## 実装ステップ（最小変更）
- フィールド追加（LTCDecoder.cs）
  - `double micStartDspTime;`
  - `int wrapCount;`
  - `int clipSamples;`
  - `double lastSegmentEndDsp;`
  - `bool isMicCalibrated;`（較正完了フラグ）
- StartRecording/ProcessAudioData
  - `clipSamples = microphoneClip.samples; wrapCount = 0;`
  - 最初に`pos>0`確認できた時に`micStartDspTime`較正し、`isMicCalibrated = true`。
  - 未較正中はフォールバックで従来の `AudioSettings.dspTime` を用いる（安全策）。
  - ラップ検出と`lastSamplePosition`更新の一貫性を担保。
- ProcessAudioSegment
  - 上記計算で`lastSegmentEndDsp`を更新。
- ProcessDecodedLTC
  - `dspTime = lastSegmentEndDsp`に差し替え。
- しきい値微調整（任意）
  - `syncThreshold`は0.03〜0.05秒程度へ試験的に縮小。

## 擬似コード（差分イメージ）
- ProcessAudioSegment内:
  - `endPos = (startPosition + length) % clipSamples`
  - `endAbs = wrapCount * clipSamples + endPos`
  - `lastSegmentEndDsp = micStartDspTime + (double)endAbs / sampleRate`
- ProcessDecodedLTC内:
  - `dspTime = lastSegmentEndDsp` を使用して `ltcBuffer.Enqueue(new LTCSample { dspTime, ... })`

## 検証手順
- Before/After比較: `internalTcTime - (latest.tcSeconds + age)` の分散・標準偏差をログ化し、50%以上縮小を目標。
- オフライン: `TimecodeEncoder`で合成LTCを生成し、デコード刻印の誤差を評価。
- 実機: 入力レベル・デバイス構成を変化させ、ドリフト補正挙動の安定性とジャンプ検出の過検出減少を確認。

## 受け入れ基準（Acceptance Criteria）
- 連続1分の運用で、出力TCとデコードTCの差分標準偏差が現行比50%以上縮小。
- デコード開始/停止・ジャンプ検出ロジックが既存と等価以上に安定。
- 追加によるGC割り当て増がない（Profilerで平常フレーム0B）。

## リスクと注意
- マイク経路の固定レイテンシは残存（ただし一定なら相対同期に影響小）。
- 入出力デバイスが別クロックだと長時間で微小ドリフトは発生（正しく観測・補正対象にできる）。
- `micStartDspTime`較正は`pos>0`を待つ。未較正時は従来法にフォールバック可能。

## 将来的拡張
- デコーダを拡張し、同期ワード検出サンプルオフセットを返すAPI追加。
- `OnAudioFilterRead`ベースのブロック先頭DSP時刻を活用する高精度版（要評価）。

## 変更対象ファイル
- `jp.iridescent.ltcdecoder/Runtime/Scripts/LTCDecoder.cs`

## 参照
- 優先度付き全体計画: `Documents/ltc-priority-implementation-plan.md`

## 更新履歴
- 2025-09-04: 内容を再作成（要約/受け入れ基準/検証/参照を明確化）
