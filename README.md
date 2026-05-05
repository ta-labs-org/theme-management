# theme-management

テーマ稼働管理システム

## 認証 / Authentication

認証は **Azure App Service Easy Auth + Entra ID** で行います。アプリケーションコード内に認証ロジックはなく、Azure インフラ側で処理されます。

Easy Auth が有効化された App Service 上では、未認証リクエストは Entra ID ログインページへ自動的にリダイレクトされます。

## 認可 / Authorization (RBAC)

認証後のロールベースアクセス制御はアプリケーション側で実装しています。

### ロール設定

Entra ID のアプリ登録で App Role を定義し、ユーザーまたはグループをロールに割り当ててください。

| ロール | 権限 |
|--------|------|
| Admin  | 閲覧・編集・追加・削除すべて可能 |
| (ロール未割当) | 閲覧のみ可能（編集・追加・削除ボタン非表示） |

> **注**: ロールが未割当のユーザーもアプリにはアクセスできます（Easy Auth で認証済みのため）。ただし、編集・追加・削除の操作は Admin ロールのみに表示されます。完全にアクセスをブロックするには、Azure Portal のアプリ登録で「割り当てが必要」を有効化してください。

### Easy Auth の設定

Azure Portal の App Service → 認証 で以下を設定してください：

1. ID プロバイダー: Microsoft (Entra ID)
2. アクセスを制限する: 認証が必要
3. アプリ登録でアプリロール（例: `Admin`）を定義
4. ユーザーまたはグループをロールに割り当て

## 稼働予測エージェント（GitHub Copilot）

稼働予測エージェントは、GitHub Copilot を活用したアシスタント機能です。「来月アサイン可能なエンジニアを教えて」「テーマ〇〇に稼働余力があるエンジニアをアサインして」といった自然な日本語での問い合わせに対して、システムのデータを分析し、最適なエンジニア配置を提案・実行します。

### 前提条件

エージェント機能を利用するには、以下の準備が必要です：

- **GitHub CLI**: `gh` コマンドラインツール（[github.com/cli/cli](https://github.com/cli/cli) から入手）
- **GitHub Copilot**: GitHub CLI 上で利用するための拡張機能
- **GitHub Copilot ライセンス**: 個人用 Copilot または企業用 Copilot の有効なサブスクリプション
- **GitHub アカウント**: Copilot ライセンスが割り当てられている GitHub アカウント

### セットアップ手順

#### 1. GitHub CLI をインストール

```bash
# Windows (Chocolatey)
choco install gh

# macOS (Homebrew)
brew install gh

# Linux
# https://github.com/cli/cli/blob/trunk/docs/install_linux.md 参照
```

#### 2. GitHub CLI で認証

```bash
gh auth login
```

対話的にログイン情報を入力します。GitHub Copilot ライセンスが割り当てられたアカウントでログインしてください。

#### 3. GitHub Copilot 拡張機能をインストール

```bash
gh extension install github/gh-copilot
```

#### 4. 利用可能性を確認（オプション）

```bash
gh copilot --version
```

### 設定

#### モデル選択

エージェントが使用するモデルは `copilot-agent-settings.json` で設定します：

```json
{
  "Model": "claude-sonnet-4.6"
}
```

開発環境の場合は `appsettings.Development.json` で上書きすることもできます：

```json
{
  "CopilotAgent": {
    "Model": "claude-sonnet-4.6"
  }
}
```

利用可能なモデルは `appsettings.json` の `CopilotAgent.AvailableModels` で定義されています。

### 利用方法

1. アプリにログインした状態で「稼働予測エージェント」ページに遷移してください
2. テキスト入力欄にエンジニア配置に関する質問や指示を日本語で入力します
   - 例1: 「来月アサイン可能なエンジニアを教えて」
   - 例2: 「テーマ A に稼働余力があるエンジニアを教えて」
   - 例3: 「テーマ B にエンジニア C を 50 時間アサインして」
3. **Enter** キーを押すか、送信ボタンをクリックします
4. エージェントが提案・実行結果を表示します

### トラブルシューティング

**「エージェントの起動に失敗しました」エラーが表示される場合:**

- GitHub CLI が正常にインストールされているか確認してください（`gh --version`）
- GitHub Copilot 拡張機能がインストールされているか確認してください（`gh extension list`）
- GitHub CLI で正しいアカウントにログインしているか確認してください（`gh auth status`）
- アカウントに有効な Copilot ライセンスが割り当てられているか確認してください

**エージェントが応答しない場合:**

- ネットワーク接続を確認してください
- GitHub Copilot サービスが利用可能か確認してください
- ブラウザのコンソール（F12）を確認してください
