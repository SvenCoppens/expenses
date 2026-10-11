# Product Requirement Specification (PRS)
## Feature: Personal Expense Tracker (Blazor Server)

**Document Version:** 1.0.0  
**Status:** Under Review (HITL Gate)  
**Date:** 2026-10-11  
**Architecture Track:** Track 3 (Product Requirements Engineering)

---

## 1. Executive Summary & Vision

The **Personal Expense Tracker** is a self-hostable, portable financial management tool built on **.NET 10 Blazor Interactive Server** and **SQLite**. It allows users to track expenses, income, and savings, organize spending under a two-tier category hierarchy (Parent and Subcategories), inspect spending through monthly and yearly analytics, import and export data in a standardized CSV format with duplicate staging, and create full database snapshot backups for easy portability between machines.

Future iterations will introduce shared account collaboration (couples/group trips), automated bank JSON ingestion, and configurable bank CSV templates.

---

## 2. The 5-Pillar Product Requirements

### Pillar 1: Functional Requirements & Scope Boundaries

#### In Scope (Current Iteration & Near-Term Phasing)
1. **User Authentication & Scoped Tenancy**:
   - Built on existing ASP.NET Core Identity with Passkey (WebAuthn) and cookie support.
   - Every financial record (`Transaction`, `Category`) is scoped to the authenticated `UserId`.
   - User A cannot view, modify, or delete User B's transactions or categories.
2. **Two-Tier Category Taxonomy**:
   - Parent categories (e.g., *Food*, *Housing*, *Transportation*, *Leisure*).
   - Subcategories under parent categories (e.g., *Food > Groceries*, *Food > Restaurant*).
   - CRUD management for categories and subcategories in a dedicated UI.
3. **Transaction Management**:
   - Fields: `Date`, `Amount` (strictly `decimal`), `Currency` (EUR), `Description`, `Category` (Parent & Subcategory), `TransactionType` (`Expense`, `Income`, `Transfer`).
   - Manual transaction creation via quick-add modal/form.
   - Inline editing in the transaction data grid (edit date, amount, description, type, and category directly).
   - Bulk row selection and bulk deletion.
   - Multi-category filter and date range filtering.
4. **Standardized CSV Import & Export**:
   - Export all user transactions to a universal CSV: `Date, Amount, Description, Category, Subcategory, TransactionType`.
   - Import transactions from the exact same CSV format.
   - **Duplicate Detection & Staging**: Flag suspected duplicates (matching Date, Amount, Description within +/- 1 day window) and present a staging/review step before writing to the database.
5. **Database Snapshot Portability (Backup & Restore)**:
   - UI functionality to download a complete, consistent SQLite snapshot (`app.db`).
   - UI functionality to upload/restore a previously downloaded snapshot file.
6. **Analytics & Financial Dashboard**:
   - Monthly and yearly spending totals broken down by parent category.
   - Drill-down inside any category showing subcategory amounts and percentages (e.g., Food: 70% Cooking at home, 30% Restaurants).
   - Net totals: Total Spent, Total Income, Total Saved for month, year, or custom date ranges.
   - Monthly averages when viewing multi-month or annual timeframes.

#### Out of Scope (Explicit Non-Goals for Initial MVP)
- Multi-user joint accounts, shared wallets, and trip pools (deferred to Phase 4).
- Bank API JSON ingestion endpoints (deferred to Phase 5).
- Configurable bank statement parsers with column mapping for external bank CSV formats (deferred to Phase 5).
- Automated Regex rule categorization engine (deferred to future rule-learning phase).
- Multi-currency conversion or live FX rates (all transactions are strictly in EUR).

---

### Pillar 2: Technical Layers & System Integration

1. **Host & Frontend**: ASP.NET Core 10 Blazor Web App with `InteractiveServer` render mode.
2. **Database & ORM**: SQLite (`Data/app.db`) managed via Entity Framework Core 10.0.9.
3. **Concurrency Safety**: Blazor Server SignalR circuit isolation using `IDbContextFactory<ApplicationDbContext>` (strictly avoiding shared DbContext concurrency crashes).
4. **Namespace & Layering Structure**:
   - `Expenses.Data`: DbContext, Entity Framework entity configurations, migrations.
   - `Expenses.Models`: Domain entities, Enums (`TransactionType`), DTOs.
   - `Expenses.Services`: Application services (`ITransactionService`, `ICategoryService`, `ICsvExportImportService`, `IDatabaseBackupService`).
   - `Expenses.Common`: Result types (`Result<T>`), constants.
   - `Expenses.Components.Pages`: Blazor pages (`Dashboard.razor`, `Transactions.razor`, `Categories.razor`, `BackupRestore.razor`).
   - `Expenses.Components.Shared`: Reusable UI widgets (Data grid, category picker, staging dialog).

---

### Pillar 3: UI/UX Flows & Interaction Mechanics

1. **Main Navigation**:
   - Sidebar navigation:
     - 📊 **Dashboard** (Spending summaries, category donut/bar charts, subcategory drill-downs, averages).
     - 💳 **Transactions** (Filterable, inline-editable data table with bulk actions and manual entry).
     - 🏷️ **Categories** (Tree-view / nested manager for parent and subcategories).
     - 💾 **Data & Backups** (CSV import/export with duplicate review, SQLite snapshot backup/restore).
     - 👤 **Account** (Passkeys & profile management via existing Identity UI).
2. **Transactions Table Experience**:
   - Filter bar: Date range picker (This Month, Last Month, Year-to-Date, Custom), Multi-select category dropdown, Transaction type filter (`All`, `Expense`, `Income`).
   - Grid: Checkbox column, Date, Description, Amount (EUR, color-coded red for Expense, green for Income), Parent Category, Subcategory, Transaction Type, Actions (Save/Cancel/Delete).
   - Inline edit trigger on row click or edit icon.
   - Top action bar: "+ Add Transaction", "Bulk Delete (<n> selected)".
3. **CSV Import Flow with Duplicate Staging**:
   - Step 1: User uploads standardized `.csv` file.
   - Step 2: System parses rows and evaluates against existing database records for potential duplicates.
   - Step 3: Staging Modal displays:
     - New records (ready to import).
     - Suspected duplicates (highlighted with match reason: identical date, amount, description).
     - Checkboxes to allow user to uncheck duplicates or force-import.
   - Step 4: User clicks "Confirm Import" -> atomic batch insert.
4. **Database Snapshot Flow**:
   - "Download Snapshot" streams the SQLite database file as an attachment.
   - "Restore Snapshot" accepts an `.db` file, validates schema header, safely closes connections, replaces `app.db`, and prompts user to reload the page.

---

### Pillar 4: Data Models & Schema Design

#### 1. `Transaction`
- `Guid Id` (Primary Key)
- `string UserId` (Foreign key to `AspNetUsers`, Indexed)
- `DateOnly Date` (Transaction date)
- `decimal Amount` (Monetary amount in EUR, precision 18,2)
- `string Description` (Description or payee)
- `TransactionType Type` (`Expense`, `Income`, `Transfer`)
- `Guid? CategoryId` (Nullable foreign key to parent `Category`)
- `Guid? SubcategoryId` (Nullable foreign key to child `Category`)
- `string? Notes` (Optional user notes)
- `DateTime CreatedAtUtc` (Audit timestamp)

#### 2. `Category`
- `Guid Id` (Primary Key)
- `string UserId` (Foreign key to `AspNetUsers`, Indexed)
- `string Name` (Category name, e.g. "Food")
- `Guid? ParentCategoryId` (Self-referencing foreign key for two-tier hierarchy; null = Top-level parent)
- `string? ColorCode` (HEX color for charts, e.g., `#FF5733`)
- `string? Icon` (Optional icon identifier)
- `int DisplayOrder` (Sort ordering)
- `ICollection<Category> Subcategories` (Navigation property)

#### 3. `TransactionType` (Enum)
- `Expense = 1`
- `Income = 2`
- `Transfer = 3`

---

### Pillar 5: Non-Functional & Reliability Constraints

1. **Monetary Precision**: All currency calculations must strictly use `decimal`, never `double` or `float`. All currency displays are formatted in EUR (`€#,##0.00`).
2. **Thread Concurrency**: Every database operation in Blazor Server must use `IDbContextFactory<ApplicationDbContext>` with scoped or short-lived `using var context = await dbContextFactory.CreateDbContextAsync()` to prevent EF Core concurrency exceptions.
3. **Data Integrity**: Batch operations (CSV imports and bulk deletes) must run in isolated database transactions.
4. **Portability**: Database paths must use relative project data paths (`Data/app.db`) to enable seamless execution across macOS, Linux, and Windows.

---

## 3. Phased Implementation Roadmap

```
┌────────────────────────────────────────────────────────────────────────┐
│ Phase 1: Core Domain, EF Core Migration & Entity Services               │
│ - Category & Transaction entities with two-tier hierarchy             │
│ - IDbContextFactory migration & SQLite configuration                   │
│ - User-scoped TransactionService & CategoryService with Result<T>      │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 2: Transaction Grid & Category Management UI                     │
│ - Categories tree management page                                      │
│ - Transactions table with inline editing, multi-filters, bulk delete   │
│ - Add transaction dialog / drawer                                      │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 3: CSV Import/Export, Duplicate Staging & DB Snapshots           │
│ - Standardized CSV parser & exporter (CsvHelper)                       │
│ - Duplicate detection algorithm & UI review staging dialog             │
│ - Backup & Restore UI for SQLite snapshots                             │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 4: Analytics & Financial Dashboard                               │
│ - Monthly/Yearly totals (Income, Spent, Saved)                         │
│ - Parent category spending breakdown & averages                        │
│ - Subcategory percentage drilldowns                                    │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 5 (Future Milestone): Shared Spaces & Collaboration               │
│ - Account / Workspace models (joint accounts, group trips)             │
│ - Multi-user shared visibility and permission checks                   │
└───────────────────────────────────┬────────────────────────────────────┘
                                    │
┌───────────────────────────────────▼────────────────────────────────────┐
│ Phase 6 (Future Milestone): Advanced Ingestion & Auto-Categorization   │
│ - Bank API JSON ingestion endpoint                                     │
│ - Multi-bank CSV configurable mapping templates                        │
│ - Machine-learned / rule-based auto-categorization                     │
└────────────────────────────────────────────────────────────────────────┘
```

---

## 4. User Stories Breakdown (Phased)

### Phase 1: Core Domain & Data Layer
- **US-1.1**: *As a user, my financial data is stored securely in SQLite with strict per-user data isolation.*
- **US-1.2**: *As a developer, I can create, update, and query transactions and two-tier categories using `IDbContextFactory` without threading conflicts.*

### Phase 2: Transaction & Category UI
- **US-2.1**: *As a user, I can create parent categories and subcategories (e.g. Food -> Restaurant) with custom colors.*
- **US-2.2**: *As a user, I can view my transactions in an interactive data table with multi-select category filters and date ranges.*
- **US-2.3**: *As a user, I can edit any transaction field inline and immediately persist changes.*
- **US-2.4**: *As a user, I can manually add new transactions and select multiple rows to bulk delete them.*

### Phase 3: CSV Ingestion, Staging & DB Snapshot
- **US-3.1**: *As a user, I can export all my transactions to a standardized CSV format (`Date, Amount, Description, Category, Subcategory, TransactionType`).*
- **US-3.2**: *As a user, I can import a standardized CSV and review suspected duplicates in a staging dialog before finalizing the import.*
- **US-3.3**: *As a user, I can download a complete SQLite database snapshot and upload it to another computer to restore my exact state.*

### Phase 4: Financial Analytics & Insights
- **US-4.1**: *As a user, I can see my total income, expenses, and net savings for any selected month, year, or custom period.*
- **US-4.2**: *As a user, I can view my spending aggregated by parent category and drill down to see subcategory amounts and percentage shares.*
- **US-4.3**: *As a user, I can see monthly spending averages when inspecting a multi-month or annual timeframe.*

### Phase 5 & 6 (Future Backlog)
- **US-5.1**: *As a user, I can create a shared budget space with another user (e.g. spouse, trip group) while keeping personal expenses private.*
- **US-6.1**: *As an external service, I can POST bank JSON payloads to ingest transactions.*
- **US-6.2**: *As a user, I can import diverse bank statement CSV files using custom column mapping templates.*
