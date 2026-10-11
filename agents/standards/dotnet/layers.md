# Layered Architecture & Boundary Rules

## 1. Directory Structure & Namespaces

```text
Expenses/Expenses/
├── Components/               # Namespace: Expenses.Components.*
│   ├── Pages/                # Routable pages (Expenses, Categories, Ingestion)
│   ├── Layout/               # Shell layout & navigation
│   ├── Account/              # Identity UI & endpoints
│   └── Shared/               # Reusable UI widgets (Charts, Uploaders, Modals)
├── Services/                 # Namespace: Expenses.Services.*
│   ├── Ingestion/            # Parsers (CSV, Bank JSON, Screenshot/OCR)
│   ├── Deduplication/        # Deduplication engine & matching algorithms
│   ├── Categories/           # Category manager & rule engine
│   └── Expenses/             # Expense aggregation, queries, and mutations
├── Models/                   # Namespace: Expenses.Models.*
│   ├── Entities/             # Domain entities (Expense, Category, Rule, IngestionBatch)
│   ├── DTOs/                 # Ingestion DTOs, request/response models
│   └── Enums/                # IngestionSource, ExpenseStatus, DuplicateMatchConfidence
├── Data/                     # Namespace: Expenses.Data.*
│   ├── ApplicationDbContext  # EF Core context
│   ├── ApplicationUser       # Identity user model
│   ├── Configurations/       # Fluent API entity configurations
│   └── Migrations/           # EF Core migration files
└── Common/                   # Namespace: Expenses.Common.*
    └── Result.cs             # Generic Result<T> pattern
```

---

## 2. Dependency Direction Rules

```text
Presentation Layer (Components/)
       │
       ▼
Business & Ingestion Services (Services/)
       │
       ▼
Domain & Data Layer (Models/, Data/)
```

### 2.1 Permitted Dependencies
- `Components/` may depend on `Services/`, `Models/`, and `Common/`.
- `Services/` may depend on `Data/`, `Models/`, and `Common/`.
- `Models/` has zero dependencies on `Services/` or `Components/` (pure POCOs, records, enums).
- `Data/` depends only on `Models/`.

### 2.2 Forbidden Boundary Violations
1. **No direct DB calls from UI**: `Components/` must not directly inject `ApplicationDbContext` or execute queries. It must communicate through services.
2. **No UI types in Domain**: `Models/` and `Services/` must never reference Blazor UI types (`Microsoft.AspNetCore.Components`, `NavigationManager`, `ComponentBase`).
3. **No direct static coupling**: Use Dependency Injection for all services.
