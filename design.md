# エンジニア稼働・案件テーマ管理システム 詳細設計書

作成日: 2026-04-27

---

## 1. システム概要

エンジニアの月次稼働見込みと案件テーマの進捗・完了見込みを一元管理するシステム。  
エンジニア側・案件側双方から稼働割り当てを参照・編集でき、データは共通テーブルで同期される。

### 前提条件

| 項目 | 仕様 |
|------|------|
| 1日の稼働時間 | 8時間固定 |
| 最大開発投入率 | 稼働時間の 90%（他業務バッファ） |
| 割り当て単位 | 時間（h） |
| 単価単位 | 時間単価（円/h） |
| 実績管理 | 計画値（割り当て）＝実績として扱う |
| ユーザー管理 | 不要（社内単一ユーザーツール） |
| DB | SQLite |
| アプリ | ASP.NET Core（Blazor Server） |

---

## 2. ビジネスルール

### 2.1 エンジニアの月次最大開発可能時間

```
エンジニアの稼働日数
  = EngineerMonthlyAdjustments が存在する場合 → その WorkDays
  = 存在しない場合 → MonthlyWorkDays（全体マスタ）の WorkDays

最大開発可能時間 = 稼働日数 × 8h × 0.9
```

### 2.2 割り当て制約（エンジニア側）

あるエンジニアの特定月の全テーマへの割り当て合計が、そのエンジニアの最大開発可能時間を超えてはならない。

```
SUM(EngineerThemeAllocations.AllocatedHours)
  WHERE EngineerId = X AND Year = Y AND Month = M
  ≤ 最大開発可能時間
```

### 2.3 テーマ受注金額制約

テーマへの累計稼働金額（割り当て時間 × 担当エンジニアの等級売価）が受注金額を超えてはならない。

```
SUM(AllocatedHours × Grade.UnitSalePrice)  -- エンジニアごとの等級売価で集計
  WHERE ThemeId = T
  ≤ Theme.OrderAmount
```

### 2.4 双方向連携の動作

- 割り当てデータは `EngineerThemeAllocations` テーブルが唯一の真実（Single Source of Truth）
- エンジニア管理画面・テーマ管理画面のどちらから操作しても同一レコードを CUD する
- 登録・更新時に **2.2** と **2.3** 両方のバリデーションを実施し、いずれかが違反する場合は保存を拒否してエラーメッセージを表示する

### 2.5 テーマ完了見込み算出

```
消化率(%)  = 累計稼働金額 / 受注金額 × 100
残余金額(円) = 受注金額 - 累計稼働金額

月次消化金額（過去Nヶ月平均） = 累計稼働金額 / 経過月数

完了見込み月数 = 残余金額 / 月次消化金額（平均）
完了見込み年月 = 現在年月 + 完了見込み月数（切り上げ）
```

---

## 3. データベース設計

### 3.1 ER 図

```
Grades ──< Engineers ──< EngineerMonthlyAdjustments
                │
                └──< EngineerThemeAllocations >── Themes
                                 │
MonthlyWorkDays ─────────────────┘（年月で参照）
```

### 3.2 テーブル定義

---

#### `Grades`（等級マスタ）

| カラム名 | 型 | NOT NULL | 説明 |
|----------|----|----------|------|
| `Id` | INTEGER | ✓ | PK（AUTO INCREMENT） |
| `Name` | TEXT | ✓ | 等級名（例: シニア、ミドル） |
| `UnitSalePrice` | REAL | ✓ | 売価（円/h） |
| `UnitCostPrice` | REAL | ✓ | 原価（円/h） |
| `CreatedAt` | TEXT | ✓ | 作成日時（ISO 8601） |
| `UpdatedAt` | TEXT | ✓ | 更新日時（ISO 8601） |

---

#### `Engineers`（エンジニアマスタ）

| カラム名 | 型 | NOT NULL | 説明 |
|----------|----|----------|------|
| `Id` | INTEGER | ✓ | PK（AUTO INCREMENT） |
| `Name` | TEXT | ✓ | エンジニア名 |
| `GradeId` | INTEGER | ✓ | FK → Grades.Id |
| `IsActive` | INTEGER | ✓ | 在籍フラグ（1=在籍, 0=退職） |
| `CreatedAt` | TEXT | ✓ | 作成日時 |
| `UpdatedAt` | TEXT | ✓ | 更新日時 |

---

#### `MonthlyWorkDays`（月次稼働日マスタ）

全エンジニア共通の月次稼働日数を管理する。

| カラム名 | 型 | NOT NULL | 説明 |
|----------|----|----------|------|
| `Id` | INTEGER | ✓ | PK（AUTO INCREMENT） |
| `Year` | INTEGER | ✓ | 年 |
| `Month` | INTEGER | ✓ | 月（1〜12） |
| `WorkDays` | INTEGER | ✓ | 稼働日数 |
| `CreatedAt` | TEXT | ✓ | 作成日時 |
| `UpdatedAt` | TEXT | ✓ | 更新日時 |

**ユニーク制約**: `(Year, Month)`

---

#### `EngineerMonthlyAdjustments`（エンジニア月次稼働日調整）

有給・欠勤等によりエンジニア個別に稼働日数を上書きする場合に登録する。  
レコードが存在しない年月は `MonthlyWorkDays` の値を使用する。

| カラム名 | 型 | NOT NULL | 説明 |
|----------|----|----------|------|
| `Id` | INTEGER | ✓ | PK（AUTO INCREMENT） |
| `EngineerId` | INTEGER | ✓ | FK → Engineers.Id |
| `Year` | INTEGER | ✓ | 年 |
| `Month` | INTEGER | ✓ | 月（1〜12） |
| `WorkDays` | INTEGER | ✓ | 実稼働日数（有給等考慮済み） |
| `Note` | TEXT | | 備考（有給、慶弔休暇等） |
| `CreatedAt` | TEXT | ✓ | 作成日時 |
| `UpdatedAt` | TEXT | ✓ | 更新日時 |

**ユニーク制約**: `(EngineerId, Year, Month)`

---

#### `Themes`（テーマ・案件マスタ）

| カラム名 | 型 | NOT NULL | 説明 |
|----------|----|----------|------|
| `Id` | INTEGER | ✓ | PK（AUTO INCREMENT） |
| `Name` | TEXT | ✓ | テーマ名・案件名 |
| `OrderDate` | TEXT | ✓ | 受注日（ISO 8601 date） |
| `EstimatedCompletionDate` | TEXT | ✓ | 完了予定日（ISO 8601 date） |
| `ActualCompletionDate` | TEXT | | 実際の完了日（完了時に設定） |
| `OrderAmount` | REAL | ✓ | 受注金額（円） |
| `Status` | TEXT | ✓ | ステータス（`Active` / `Completed` / `Cancelled`） |
| `CreatedAt` | TEXT | ✓ | 作成日時 |
| `UpdatedAt` | TEXT | ✓ | 更新日時 |

---

#### `EngineerThemeAllocations`（エンジニア×テーマ 月次割り当て）

エンジニアとテーマの割り当てを管理する中心テーブル。  
エンジニア管理・テーマ管理の双方からこのテーブルを参照・操作する。

| カラム名 | 型 | NOT NULL | 説明 |
|----------|----|----------|------|
| `Id` | INTEGER | ✓ | PK（AUTO INCREMENT） |
| `EngineerId` | INTEGER | ✓ | FK → Engineers.Id |
| `ThemeId` | INTEGER | ✓ | FK → Themes.Id |
| `Year` | INTEGER | ✓ | 年 |
| `Month` | INTEGER | ✓ | 月（1〜12） |
| `AllocatedHours` | REAL | ✓ | 割り当て時間（h）※0より大きい値 |
| `CreatedAt` | TEXT | ✓ | 作成日時 |
| `UpdatedAt` | TEXT | ✓ | 更新日時 |

**ユニーク制約**: `(EngineerId, ThemeId, Year, Month)`

---

### 3.3 SQLite DDL

```sql
PRAGMA foreign_keys = ON;

CREATE TABLE Grades (
    Id            INTEGER PRIMARY KEY AUTOINCREMENT,
    Name          TEXT    NOT NULL,
    UnitSalePrice REAL    NOT NULL CHECK(UnitSalePrice >= 0),
    UnitCostPrice REAL    NOT NULL CHECK(UnitCostPrice >= 0),
    CreatedAt     TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
    UpdatedAt     TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);

CREATE TABLE Engineers (
    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
    Name      TEXT    NOT NULL,
    GradeId   INTEGER NOT NULL REFERENCES Grades(Id),
    IsActive  INTEGER NOT NULL DEFAULT 1 CHECK(IsActive IN (0,1)),
    CreatedAt TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
    UpdatedAt TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);

CREATE TABLE MonthlyWorkDays (
    Id        INTEGER PRIMARY KEY AUTOINCREMENT,
    Year      INTEGER NOT NULL,
    Month     INTEGER NOT NULL CHECK(Month BETWEEN 1 AND 12),
    WorkDays  INTEGER NOT NULL CHECK(WorkDays >= 0),
    CreatedAt TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
    UpdatedAt TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
    UNIQUE (Year, Month)
);

CREATE TABLE EngineerMonthlyAdjustments (
    Id          INTEGER PRIMARY KEY AUTOINCREMENT,
    EngineerId  INTEGER NOT NULL REFERENCES Engineers(Id),
    Year        INTEGER NOT NULL,
    Month       INTEGER NOT NULL CHECK(Month BETWEEN 1 AND 12),
    WorkDays    INTEGER NOT NULL CHECK(WorkDays >= 0),
    Note        TEXT,
    CreatedAt   TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
    UpdatedAt   TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
    UNIQUE (EngineerId, Year, Month)
);

CREATE TABLE Themes (
    Id                      INTEGER PRIMARY KEY AUTOINCREMENT,
    Name                    TEXT    NOT NULL,
    OrderDate               TEXT    NOT NULL,
    EstimatedCompletionDate TEXT    NOT NULL,
    ActualCompletionDate    TEXT,
    OrderAmount             REAL    NOT NULL CHECK(OrderAmount > 0),
    Status                  TEXT    NOT NULL DEFAULT 'Active'
                                    CHECK(Status IN ('Active','Completed','Cancelled')),
    CreatedAt               TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
    UpdatedAt               TEXT    NOT NULL DEFAULT (datetime('now','localtime'))
);

CREATE TABLE EngineerThemeAllocations (
    Id             INTEGER PRIMARY KEY AUTOINCREMENT,
    EngineerId     INTEGER NOT NULL REFERENCES Engineers(Id),
    ThemeId        INTEGER NOT NULL REFERENCES Themes(Id),
    Year           INTEGER NOT NULL,
    Month          INTEGER NOT NULL CHECK(Month BETWEEN 1 AND 12),
    AllocatedHours REAL    NOT NULL CHECK(AllocatedHours > 0),
    CreatedAt      TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
    UpdatedAt      TEXT    NOT NULL DEFAULT (datetime('now','localtime')),
    UNIQUE (EngineerId, ThemeId, Year, Month)
);
```

---

## 4. アプリケーション設計

### 4.1 技術スタック

| 項目 | 採用技術 |
|------|----------|
| フレームワーク | ASP.NET Core 8 + Blazor Server |
| ORM | Entity Framework Core 8 |
| DB | SQLite（`Microsoft.EntityFrameworkCore.Sqlite`） |
| UIコンポーネント | MudBlazor（マテリアルデザイン系 Blazor UI ライブラリ） |
| バリデーション | FluentValidation |
| マイグレーション | EF Core Migrations |

### 4.2 プロジェクト構成

```
ThemeManagement/
├── ThemeManagement.sln
└── ThemeManagement/                     # Blazor Server プロジェクト
    ├── Program.cs
    ├── appsettings.json
    ├── Data/
    │   ├── AppDbContext.cs              # EF Core DbContext
    │   └── Migrations/                  # EF Core マイグレーション
    ├── Domain/
    │   ├── Entities/
    │   │   ├── Grade.cs
    │   │   ├── Engineer.cs
    │   │   ├── MonthlyWorkDays.cs
    │   │   ├── EngineerMonthlyAdjustment.cs
    │   │   ├── Theme.cs
    │   │   └── EngineerThemeAllocation.cs
    │   └── Exceptions/
    │       └── BusinessRuleException.cs
    ├── Services/
    │   ├── IGradeService.cs / GradeService.cs
    │   ├── IEngineerService.cs / EngineerService.cs
    │   ├── IWorkDayService.cs / WorkDayService.cs
    │   ├── IThemeService.cs / ThemeService.cs
    │   ├── IAllocationService.cs / AllocationService.cs
    │   └── IDashboardService.cs / DashboardService.cs
    ├── Components/
    │   ├── App.razor
    │   ├── Routes.razor
    │   ├── Layout/
    │   │   ├── MainLayout.razor
    │   │   └── NavMenu.razor
    │   └── Pages/
    │       ├── Dashboard/
    │       │   └── Dashboard.razor
    │       ├── Grades/
    │       │   ├── GradeList.razor
    │       │   └── GradeEdit.razor
    │       ├── Engineers/
    │       │   ├── EngineerList.razor
    │       │   ├── EngineerEdit.razor
    │       │   └── EngineerAllocation.razor    # エンジニア側割り当て管理
    │       ├── WorkDays/
    │       │   ├── MonthlyWorkDayList.razor
    │       │   └── EngineerAdjustmentEdit.razor
    │       └── Themes/
    │           ├── ThemeList.razor
    │           ├── ThemeEdit.razor
    │           └── ThemeAllocation.razor       # テーマ側割り当て管理
    └── wwwroot/
```

### 4.3 レイヤー責務

```
Blazor Pages / Components
    ↓↑ DI
Services（ビジネスロジック・バリデーション）
    ↓↑
AppDbContext（EF Core）
    ↓↑
SQLite
```

- **Pages**: UI・イベントハンドリングのみ。ビジネスロジックは持たない
- **Services**: ビジネスルール（制約チェック含む）・集計ロジック
- **DbContext**: エンティティ定義・リレーション設定のみ

---

## 5. 画面設計

### 5.1 画面一覧

| 画面名 | URL | 概要 |
|--------|-----|------|
| ダッシュボード | `/` | 稼働状況・テーマ進捗サマリ |
| 等級一覧 | `/grades` | 等級マスタ CRUD |
| エンジニア一覧 | `/engineers` | エンジニアマスタ CRUD |
| エンジニア稼働管理 | `/engineers/{id}/allocation` | エンジニア個別の月次割り当て |
| 月次稼働日管理 | `/workdays` | 全体の月次稼働日マスタ・個別調整 |
| テーマ一覧 | `/themes` | テーマ（案件）マスタ CRUD |
| テーマ稼働管理 | `/themes/{id}/allocation` | テーマ個別のエンジニア割り当て |

### 5.2 ダッシュボード

表示内容:

- **エンジニア稼働サマリ（対象月選択）**
  - エンジニア名 / 等級 / 最大開発可能時間 / 割り当て合計 / 残余時間 / 稼働率(%)
  - 残余時間がマイナスの場合は赤表示（ありえないが二重チェック用）

- **テーマ進捗サマリ**
  - テーマ名 / 受注金額 / 累計稼働金額 / 消化率(%) / 残余金額 / 完了見込み年月
  - 消化率が100%超過の場合は赤表示

### 5.3 エンジニア稼働管理画面（`/engineers/{id}/allocation`）

```
[エンジニア名] [等級]  対象月: [YYYY] 年 [MM] 月

稼働日数: XX日（全体マスタ）/ YY日（個別調整） ← 調整がある場合のみ表示
最大開発可能時間: ZZ.Z h
割り当て合計:     WW.W h  （残余: RR.R h）

┌──────────────────────────────────────────────────────────┐
│ テーマ          │ 割り当て時間(h) │ 売価小計(円) │ 操作  │
├──────────────────────────────────────────────────────────┤
│ [テーマ名▼]    │ [____]          │ 自動計算      │ [削除]│
│ [+ 追加]                                                  │
└──────────────────────────────────────────────────────────┘
```

- テーマ追加時に「エンジニア稼働制約」「テーマ金額制約」を同時チェック
- 更新時もリアルタイムバリデーション

### 5.4 テーマ稼働管理画面（`/themes/{id}/allocation`）

```
[テーマ名]  受注金額: XXX,XXX円  状態: Active
累計稼働金額: YYY,YYY円（消化率: ZZ.Z%）  残余: RRR,RRR円
完了見込み: YYYY年MM月

対象月: [YYYY] 年 [MM] 月

┌──────────────────────────────────────────────────────────┐
│ エンジニア      │ 等級  │ 割り当て(h) │ 売価小計(円) │ 操作 │
├──────────────────────────────────────────────────────────┤
│ [エンジニア名▼] │ 自動  │ [____]      │ 自動計算      │ [削除]│
│ [+ 追加]                                                   │
└──────────────────────────────────────────────────────────┘
```

---

## 6. サービス設計

### 6.1 AllocationService（中核サービス）

```csharp
// 稼働可能時間の取得
decimal GetMaxDevelopableHours(int engineerId, int year, int month);

// エンジニア×月の割り当て一覧取得
IList<AllocationDto> GetByEngineer(int engineerId, int year, int month);

// テーマ×月の割り当て一覧取得
IList<AllocationDto> GetByTheme(int themeId, int year, int month);

// 割り当て登録（エンジニア側・テーマ側どちらからでも同じメソッドを使う）
// → BusinessRuleException を投げる可能性あり
Task UpsertAllocationAsync(int engineerId, int themeId, int year, int month, decimal hours);

// 割り当て削除
Task DeleteAllocationAsync(int id);
```

### 6.2 バリデーションフロー（UpsertAllocation）

```
1. engineerId / themeId / year / month / hours のNullチェック・範囲チェック
2. 既存レコードを取得（Upsert判定）
3. エンジニアの最大開発可能時間を取得
4. そのエンジニアの同月の割り当て合計を計算
   （既存レコードがある場合は差分で計算）
5. 合計 > 最大開発可能時間 → BusinessRuleException("稼働上限を超えます")
6. テーマの累計稼働金額を計算
   （既存レコードがある場合は差分で計算）
7. 累計稼働金額 > Theme.OrderAmount → BusinessRuleException("受注金額を超えます")
8. DB 保存（INSERT or UPDATE）
```

### 6.3 DashboardService

```csharp
// エンジニア稼働サマリ（指定月）
IList<EngineerWorkSummaryDto> GetEngineerSummary(int year, int month);

// テーマ進捗サマリ（全アクティブテーマ）
IList<ThemeProgressDto> GetThemeProgress();
```

#### ThemeProgressDto

```csharp
public record ThemeProgressDto(
    int    ThemeId,
    string ThemeName,
    string Status,
    decimal OrderAmount,
    decimal TotalAllocatedCost,     // SUM(AllocatedHours × Grade.UnitSalePrice)
    decimal ProgressRate,           // TotalAllocatedCost / OrderAmount * 100
    decimal RemainingAmount,        // OrderAmount - TotalAllocatedCost
    decimal? EstimatedCompletionYear,
    decimal? EstimatedCompletionMonth
);
```

---

## 7. 制約・バリデーション一覧

| # | バリデーション項目 | チェックタイミング | エラーメッセージ例 |
|---|-------------------|------------------|------------------|
| 1 | エンジニアの月次割り当て合計 ≤ 最大開発可能時間 | 割り当てUpsert時 | 「{エンジニア名} の {年}/{月} 稼働上限（{X}h）を超えます（現在合計: {Y}h）」 |
| 2 | テーマ累計稼働金額 ≤ 受注金額 | 割り当てUpsert時 | 「{テーマ名} の受注金額（{X}円）を超えます（現在累計: {Y}円）」 |
| 3 | MonthlyWorkDays は（Year, Month）で重複不可 | マスタ登録時 | 「{年}/{月} のデータは既に登録されています」 |
| 4 | EngineerMonthlyAdjustments は（EngineerId, Year, Month）で重複不可 | 個別調整登録時 | 「{エンジニア名} の {年}/{月} は既に調整済みです」 |
| 5 | AllocatedHours > 0 | 割り当てUpsert時 | 「割り当て時間は0より大きい値を入力してください」 |
| 6 | Grade の UnitSalePrice / UnitCostPrice ≥ 0 | 等級登録時 | 「単価は0以上の値を入力してください」 |
| 7 | Theme.OrderAmount > 0 | テーマ登録時 | 「受注金額は0より大きい値を入力してください」 |

---

## 8. 将来拡張の考慮点（現時点では実装不要）

- 実績入力（計画と実績の分離管理）
- ユーザー認証・権限管理
- PDF/Excel エクスポート
- テーマのフェーズ管理（要件定義・設計・開発・テスト等）
- 工数予算のフェーズ分割
- 通知・アラート機能（消化率閾値超えアラート等）

---

*以上が詳細設計書です。実装フェーズでは本設計に基づき進めます。*
