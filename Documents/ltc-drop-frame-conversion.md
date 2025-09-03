# Drop Frame（29.97 DF）厳密換算ガイド（実装用）

本ガイドは、29.97fps Drop Frame（DF）のタイムコードとフレーム数の相互変換を、誤差なく実装するための手順・擬似コードをまとめたものです。NDFや他のフレームレートは通常の換算で問題ありません。

## 前提
- 名目フレームレート: 30fps（DFは“番号を落とす”方式）
- 実フレームレート: 30000/1001 ≈ 29.97fps
- ドロップ規則: 毎分の先頭で2フレームをスキップ（00:00は除外）。ただし10分ごと（分が0,10,20,30,40,50）はスキップしない。

## 記号
- h: 時（0–23）
- m: 分（0–59）
- s: 秒（0–59）
- f: フレーム（0–29）
- F: 絶対フレーム数（0以上の整数、24時間ロール等は別途クランプ）

## 方向1: タイムコード → 絶対フレーム（DF）
擬似コード:
```
function DF_TC_to_frames(h, m, s, f):
    totalMinutes = 60*h + m
    dropped = 2 * (totalMinutes - floor(totalMinutes / 10))
    framesAt30 = ((h * 3600) + (m * 60) + s) * 30 + f
    F = framesAt30 - dropped
    return F
```
注意:
- 10分ごとの分（0,10,20,30,40,50）は drop 対象から除外されるため、`floor(totalMinutes / 10)` を用いて差し引く。

## 方向2: 絶対フレーム → タイムコード（DF）
擬似コード（名目30fpsを基準に逆算）:
```
function DF_frames_to_TC(F):
    # 10分ブロック毎のパラメータ
    framesPer10Min = 17982      # 10分あたりの実フレーム数（DF）
    framesPerMin_nominal = 30 * 60  # 1800

    # 10分単位で切り出し
    tenMinBlocks = floor(F / framesPer10Min)
    remainder = F % framesPer10Min

    # 10分ブロックの中で、各分の先頭2フレームをスキップする分を計算
    # 10分ブロック内の minuteIndex = 0..9 のうち、0はドロップなし、1..9は各2フレームのドロップ
    minutesInBlock = min(9, floor((remainder + (2 * 9)) / (framesPerMin_nominal - 2)))
    # より堅牢な手順: minuteを0..9で線形探索し、各分の容量を加算しながら決定してもよい

    # 時・分の決定
    totalMinutes = tenMinBlocks * 10 + minutesInBlock
    h = floor(totalMinutes / 60) % 24
    m = totalMinutes % 60

    # その分の先頭からのオフセット（ドロップ補正を戻す）
    # minuteIndex==0 の分だけはドロップ0、他は2フレームドロップ
    droppedInThisMinute = 0 if (minutesInBlock % 10 == 0) else 2

    # 分の先頭からのフレーム数を求める
    framesIntoMinute_nominal = remainder - (minutesInBlock * (framesPerMin_nominal - 2))
    framesIntoMinute_nominal += droppedInThisMinute

    # 秒・フレームへ分解（名目30fps）
    s = floor(framesIntoMinute_nominal / 30)
    f = framesIntoMinute_nominal % 30

    return (h, m, s, f)
```
実装メモ:
- 上の10分ブロック/分内の算出は、単純化のための近似式を含む。決定的にするには、以下の「確実な線形探索」方式を推奨。

確実な線形探索（推奨）:
```
function DF_frames_to_TC(F):
    framesPerHour = 107892     # 3600秒×30fps - ドロップ相当
    framesPer10Min = 17982
    framesPerMin_nominal = 1800

    # 時の決定（時間単位で引き算）
    h = 0
    while F >= framesPerHour:
        F -= framesPerHour
        h += 1
    h = h % 24

    # 10分ブロック
    tenMin = 0
    while F >= framesPer10Min:
        F -= framesPer10Min
        tenMin += 1

    # 10分ブロック内の分
    mInBlock = 0
    while True:
        # minuteIndex=0 はドロップ0、それ以外は2フレーム
        drop = 0 if mInBlock == 0 else 2
        if F < (framesPerMin_nominal - drop):
            break
        F -= (framesPerMin_nominal - drop)
        mInBlock += 1
        if mInBlock == 10: break

    totalMinutes = h*60 + tenMin*10 + mInBlock
    m = totalMinutes % 60

    # この分の先頭にドロップがあれば戻す
    fTotal = F + (0 if mInBlock == 0 else 2)
    s = floor(fTotal / 30)
    f = fTotal % 30

    return (h, m, s, f)
```

## NDF・他fps
- NDFや25/24/30fpsは、単純に `F = floor(seconds * fps) + frame` 等の通常換算でよい。

## 参考値（チェック用）
- 29.97 DF
  - `framesPerHour = 107892`
  - `framesPer10Min = 17982`
  - 1分あたりの実フレーム数
    - 10分ごとの最初の分: 1800
    - それ以外の分: 1798

## 実装のヒント
- 24時間ロール処理: 表示時のみ `(F % (24*60*60*30))` 等で丸める。
- 整数演算を優先し、浮動小数の丸め誤差を避ける。
- 単体テスト: ランダムTCで往復（TC→F→TC）し、完全一致を確認。

## 参照
- 全体計画: `Documents/ltc-priority-implementation-plan.md`
