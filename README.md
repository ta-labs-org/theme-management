# theme-management

テーマ稼働管理システム

## 認証 / Authentication

このアプリはASP.NET Core Identityによるログイン認証とロールベースアクセス制御（RBAC）を使用します。

### ロール

| ロール | 権限 |
|--------|------|
| Admin  | 閲覧・編集・追加・削除すべて可能 |
| User   | 閲覧のみ可能（編集・追加・削除不可） |

### 初期管理者アカウント

初回起動時に `appsettings.json` の `SeedAdmin` 設定から管理者アカウントが自動作成されます。

```json
"SeedAdmin": {
  "Email": "admin@example.com",
  "Password": "Admin123!"
}
```

> **本番運用前に必ず変更してください。**  
> 環境変数 `SeedAdmin__Email` / `SeedAdmin__Password` でオーバーライド可能です。

### パスワードポリシー

- 最低8文字以上
- 英小文字を含む
- 数字を含む
- 5回連続ログイン失敗でアカウントが5分間ロック
