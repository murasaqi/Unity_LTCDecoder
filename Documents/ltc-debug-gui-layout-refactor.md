# LTC Timeline Sync Debug GUI レイアウト刷新 設計・実装計画

## 目的
- 要素が増減してもレイアウトが破綻しない構造にする。
- Clear / Export / Copy ボタンをコンパクトにし、右寄せのツールバーとしてまとめる。
- Main Panel を画面（シーン/ゲームビュー）の高さに追従させ、文字が読みやすいサイズ・配色にする。

## 要求仕様（誰でも確認できる指標）
- 追加するテキスト/セクションが縦に積まれ、はみ出した分はスクロールで参照可能。
- パネルは画面右側に固定し、縦方向は常に上下にストレッチ（高さ可変）。
- 横幅は生成時にビュー幅の一定比率（既定30%）で決定し、最小320px、最大560pxを超えない。
- ボタンは行高26px程度・右寄せ・一定余白で視認性と操作性を両立。
- フォントは14–16px、長文は折返し。行間・余白は適切に確保。

## 全体方針
- 自動レイアウトを基本とし、固定ピクセル配置を避ける。
- `VerticalLayoutGroup`（VLG）、`HorizontalLayoutGroup`（HLG）、`ContentSizeFitter`（CSF）、`LayoutElement` を役割に応じて付与。
- 表示情報のコンテナは必ず `ScrollRect` に収める。
- 生成直後にパネル横幅をビュー幅から計算し、縦方向はアンカー設定で自動追従。

## 推奨UIツリー構成
- Canvas (Screen Space – Overlay / 既存Canvas再利用)
  - Panel: Main container（右側・縦ストレッチ）
    - Image（半透明背景 色: rgba(25,25,25,0.92) 目安）
    - VerticalLayoutGroup（padding: 10; spacing: 6; childForceExpandWidth: true; childForceExpandHeight: false）
    - Header（ツールバー）
      - HorizontalLayoutGroup（spacing: 6; childAlignment: MiddleCenter）
      - Title Text（左）
      - Flexible Space（LayoutElement flexibleWidth = 1）
      - Clear Button（preferredHeight: 26; preferredWidth: 72）
      - Export Button（preferredHeight: 26; preferredWidth: 72）
      - Copy Button（preferredHeight: 26; preferredWidth: 72）
    - Separator（2px ライン Image color: rgba(128,128,128,0.5)）
    - ScrollRect（horizontal: false, vertical: true）
      - Viewport（Mask + Image (透明) / RectTransform: 全面ストレッチ）
        - Content（VLG + CSF(vertical: PreferredSize)）
          - Section Title Text（太字, 16px）
          - Row Text（14px, 折返しON, LayoutElement.minHeight = 22 目安）
          - …（追加要素はすべてここに積む）

## RectTransform/アンカー詳細
- Panel（Main container）
  - anchorMin = (1, 0)
  - anchorMax = (1, 1)
  - pivot     = (1, 1)
  - anchoredPosition = (-10, -10)
  - sizeDelta.x = clamp(viewWidth * 0.3, 320, 560)
  - sizeDelta.y = 0（縦はアンカーで追従）
- ScrollRect/Viewport/Content は RectTransform を全面ストレッチ（anchorMin=(0,0), anchorMax=(1,1), pivot=(0.5,0.5), sizeDelta=(0,0)）。

## ボタン設計
- FontSize: 14、Padding（左右8–12px）
- LayoutElement: preferredHeight=26, preferredWidth=72（文言に応じ調整: 64–80）
- ヘッダー右寄せ: Title と右側ボタンの間に flexibleWidth=1 のダミー（Flexible Space）を配置

## フォント/テキスト
- Font: `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")` またはプロジェクト標準
- Header Title: 18–20px, Bold
- Section Title: 16px, Bold
- Row Text: 14px, 折返し有効（`Text.horizontalOverflow=Wrap; verticalOverflow=Truncate`）
- テキスト色: 白（rgba(255,255,255,1)）

## 生成時スケール（コード化方針）
- 横幅: 生成時に以下で算出
  - Editor生成: `var vw = SceneView.lastActiveSceneView ? SceneView.lastActiveSceneView.position.width : (float)Screen.width;`
  - Runtime生成: `var vw = (float)Screen.width;`
  - `panelRect.sizeDelta = new Vector2(Mathf.Clamp(vw * 0.3f, 320f, 560f), 0f);`
- 縦方向はストレッチ（アンカー）で常時追従のため設定不要
- 既存Canvasが `CanvasScaler` を使用する場合、二重スケール回避のため新規Canvas作成時のみ `Scale With Screen Size` を設定

## コード変更点（ファイル別）
1) jp.iridescent.ltcdecoder/Editor/LTCTimelineSyncDebugSetup.cs
- Panel作成時:
  - RectTransformのアンカー/ピボットを上記に変更
  - 背景Imageの色をセミトーンに
  - Header（GameObject）を新規作成しHLG追加、Title Text + Flexible Space + ボタン3種を生成
  - Separator（2px）を追加
  - ScrollRect/Viewport/Contentの3層を生成し、ContentにVLG+CSFを付与
- CreateTextElement:
  - 親Transformを `Content` にする（パネル直下ではなくスクロール内）
  - 折返しON、行高のLayoutElementを付与

2) jp.iridescent.ltcdecoder/Runtime/UI/LTCTimelineSyncDebugUI.cs
- 参照を新レイアウトに合わせて割当
  - headerText → Header内のTitle Text
  - 各情報テキストは Content 直下の生成要素に対応
- 文字サイズ/折返し/見出しのBold適用

## 擬似コード（生成時横幅の決定）
```csharp
float viewWidth = (float)Screen.width;
#if UNITY_EDITOR
if (SceneView.lastActiveSceneView != null)
    viewWidth = SceneView.lastActiveSceneView.position.width;
#endif
float width = Mathf.Clamp(viewWidth * 0.3f, 320f, 560f);
panelRect.sizeDelta = new Vector2(width, 0f);
```

## テスト項目
- 要素の追加/削除/順序変更で破綻しない（スクロールに収まる）
- 解像度/ウィンドウサイズ変更で縦方向に追従（アンカーが効く）
- ボタンの大きさ・右寄せ配置・クリック領域が適切
- テキストの可読性（14–16px、折返し、コントラスト）
- 長時間ログ（要素増加）でパフォーマンス問題がない

## 互換/ロールバック
- 既存のデバッグUI生成APIは維持（内部構造のみ更新）
- 既存シーンに埋め込まれた旧レイアウトは、Updateメニュー（"Update Timeline Sync Debug UI"）で再生成/更新できるようにしておく

## 作業手順チェックリスト（実装順）
1. Panel のアンカー/ピボット/背景/横幅計算を実装
2. Header（HLG）＋ Title/Flexible/Buttons（Clear/Export/Copy）を実装
3. Separator を追加
4. ScrollRect/Viewport/Content（VLG+CSF）へ再構成
5. CreateTextElement を `Content` 直下で生成し、折返し/行高調整
6. LTCTimelineSyncDebugUI の参照付けと表示更新の確認
7. 解像度/要素追加の耐性テスト

---
作成者: Codex CLI 支援
日付: 2025-09-04
