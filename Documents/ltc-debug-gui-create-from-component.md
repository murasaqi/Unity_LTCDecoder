LTCDecoder インスペクタから Debug GUI を作成する実装計画

## 目的
- シーン右クリックや上部メニューに加えて、LTCDecoder コンポーネントのインスペクタからワンクリックで Debug GUI（Timeline Sync Debug UI）を作成/更新できるようにする。
- 既存の Editor ユーティリティ（`LTCTimelineSyncDebugSetup`）を再利用し、重複生成やレイアウト破綻を避ける安全な導線を提供する。

## ユーザー体験（UX）
- LTCDecoder を選択 → インスペクタに「Debug Tools」セクションが表示。
- ボタン:
  - Create Debug UI: Debug GUI を作成（既存があれば選択または更新に誘導）。
  - Update Debug UI: 既存の Debug GUI 構造を最新仕様に再構成（レイアウト刷新適用）。
  - Open Docs: レイアウトや使い方のドキュメントを開く。
- 実行結果は Undo 対応。作成後は対象 GameObject を選択状態にしてすぐ編集できる。

## 実装ポリシー
- 既存の `jp.iridescent.ltcdecoder/Editor/LTCTimelineSyncDebugSetup.cs` の公開メソッドを呼び出して生成/更新を行う（コード重複を避ける）。
- 生成時はレイアウト刷新方針に準拠（参照: `ltc-debug-gui-layout-refactor.md`）。
- 二重生成防止:
  - シーン内に既に `LTCTimelineSyncDebugUI` が存在する場合は、それを選択して通知（必要に応じ Update を案内）。
  - 強制的に複数枚ほしい要件が将来出るまでは、重複生成をデフォルトで抑止し UX を安定化。
- Undo/Redo 対応: 新規作成時は `Undo.RegisterCreatedObjectUndo()` を呼ぶ（既存ロジック流用）。

## 追加/変更ファイル
- 変更: `jp.iridescent.ltcdecoder/Editor/LTCDecoderEditor.cs`
  - インスペクタ末尾（または「LTC Status & Output」の後）に「Debug Tools」ボックスを追加。
  - 内部から `LTCTimelineSyncDebugSetup.CreateTimelineSyncDebugUI()` / `UpdateTimelineSyncDebugUI()` を呼ぶ。
  - 存在チェックの前置き（`Object.FindFirstObjectByType<LTCTimelineSyncDebugUI>()` 互換）を行い、重複生成を抑止。
  - 「Open Docs」は `Application.OpenURL()` でリポジトリ or ローカルドキュメントを開く。

## UI 仕様（Inspector セクション）
- 見出し: Debug Tools（`EditorStyles.boldLabel`）
- ボタン群（1行または2行構成）:
  - Create Debug UI（幅 140–160）
  - Update Debug UI（幅 140–160）
  - Open Docs（幅 100）
- 状態表示:
  - 既存 Debug UI がある場合は、その GameObject 名と選択ボタンを表示（Select）。
  - 無い場合は「Not Found」表示。

## 疑似コード（Editor 実装）
```
EditorGUILayout.BeginVertical(EditorStyles.helpBox);
EditorGUILayout.LabelField("Debug Tools", EditorStyles.boldLabel);

// 既存UIチェック
var existing = Object.FindFirstObjectByType<LTCTimelineSyncDebugUI>();
EditorGUILayout.BeginHorizontal();
EditorGUILayout.LabelField("Debug UI:", GUILayout.Width(80));
if (existing)
{
    EditorGUILayout.LabelField(existing.gameObject.name);
    if (GUILayout.Button("Select", GUILayout.Width(80)))
        Selection.activeGameObject = existing.gameObject;
}
else
{
    EditorGUILayout.LabelField("Not Found");
}
EditorGUILayout.EndHorizontal();

EditorGUILayout.Space(4);
EditorGUILayout.BeginHorizontal();
if (GUILayout.Button("Create Debug UI", GUILayout.Width(160)))
{
    if (existing == null)
        LTCTimelineSyncDebugSetup.CreateTimelineSyncDebugUI();
    else
        EditorUtility.DisplayDialog("Debug UI", "既に存在します。UpdateまたはSelectをご利用ください。", "OK");
}
if (GUILayout.Button("Update Debug UI", GUILayout.Width(160)))
{
    LTCTimelineSyncDebugSetup.UpdateTimelineSyncDebugUI();
}
if (GUILayout.Button("Open Docs", GUILayout.Width(100)))
{
    Application.OpenURL("file://" + PathToDocs);
}
EditorGUILayout.EndHorizontal();
EditorGUILayout.EndVertical();
```

## PathToDocs（候補）
- レイアウト刷新: `Documents/ltc-debug-gui-layout-refactor.md`
- 生成導線: 本ファイル `Documents/ltc-debug-gui-create-from-component.md`

## 受け入れ基準
- Create: Debug UI が存在しない時のみ新規作成され、Undo が効く。作成直後に対象が選択される。
- Update: 既存 UI が最新構造へ更新される（ツールバー/スクロール/レイアウトが反映）。
- 重複生成抑止: 既存 UI がある状態で Create を押すと重複せずユーザーに案内される。
- Docs: クリックで該当ドキュメントが開く。

## テスト項目
- 既存 UI なし → Create → 生成/選択/Undo → 復元を確認。
- 既存 UI あり → Create → 重複抑止のダイアログ表示。
- 既存 UI あり → Update → 構造更新・参照設定の健全性。
- 複数シーン/再ロードでの安定動作。

## 備考
- `FindFirstObjectByType<T>` は Unity 2022.2+。旧環境では `Object.FindObjectOfType<T>()` にフォールバックする互換ラッパーを用意しても良い。
- 生成ロジックは `LTCTimelineSyncDebugSetup` に集約し、Editor ボタンは呼び口のみ保持することで保守性を高める。
