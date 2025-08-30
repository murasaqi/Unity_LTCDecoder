# GitHubリポジトリ名変更手順

## リポジトリ名を Unity_LTCTimeline → Unity_LTCDecoder に変更する方法

### 1. GitHubでの作業

1. GitHubで `https://github.com/murasaqi/Unity_LTCTimeline` にアクセス
2. Settings（設定）タブをクリック
3. Repository name（リポジトリ名）欄を `Unity_LTCDecoder` に変更
4. "Rename" ボタンをクリック

### 2. ローカルでの作業

リポジトリ名変更後、ローカルのリモートURLを更新：

```bash
# 現在のリモートURL確認
git remote -v

# リモートURLを新しい名前に更新
git remote set-url origin https://github.com/murasaqi/Unity_LTCDecoder.git

# 確認
git remote -v
```

### 3. パッケージのインストール方法

Unity Package Manager経由でインストール可能：

#### 方法1: Git URL経由
```
https://github.com/murasaqi/Unity_LTCDecoder.git#jp.iridescent.ltcdecoder
```

#### 方法2: manifest.json経由
```json
{
  "dependencies": {
    "jp.iridescent.ltcdecoder": "https://github.com/murasaqi/Unity_LTCDecoder.git#jp.iridescent.ltcdecoder"
  }
}
```

### 4. ブランチ構造について

現在のパッケージは `jp.iridescent.ltcdecoder` ディレクトリ内にあるため、インストール時は `#jp.iridescent.ltcdecoder` を指定します。

別の方法として、パッケージ専用ブランチを作成することも可能：

```bash
# パッケージ専用ブランチ作成（オプション）
git subtree push --prefix=jp.iridescent.ltcdecoder origin package

# この場合のインストールURL
https://github.com/murasaqi/Unity_LTCDecoder.git#package
```

### 5. 既存プロジェクトへの影響

- 既存のリンクは自動的にリダイレクトされます
- ローカルクローンは上記手順でリモートURLを更新すれば引き続き使用可能

### 完了後の確認事項

- [ ] GitHubでリポジトリ名が変更されている
- [ ] ローカルのリモートURLが更新されている
- [ ] パッケージがPackage Manager経由でインストール可能
- [ ] README内のURLが更新されている