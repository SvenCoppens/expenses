# Architecture Onboarding Report: Expenses Project

## 1. Executive Summary
- **Project Name**: Expenses
- **Runtime & SDK**: .NET 10.0 (C# 13)
- **Primary Framework**: ASP.NET Core Blazor Web App (Interactive Server mode)
- **Database & ORM**: SQLite (`Data/app.db`) via Entity Framework Core 10.0.9
- **Security & Identity**: ASP.NET Core Identity with Passkeys & cookie authentication
- **Domain Mission**: Personal Expense Tracking system with support for multi-source ingestion (Screenshots/OCR, Bank API JSON, and CSV bank statements), intelligent deduplication filtering, and an extensible category hierarchy.

---

## 2. Codebase Topology & Directory Structure

```text
Expenses/                           # Workspace / Git Repository Root
├── .gitignore                      # .NET gitignore template
├── README.md                       # Project overview
├── AGENTS.md                       # Root agent context & lazy-loaded standards registry
├── opencode.jsonc                  # opencode tool configuration
├── agents/                         # Agent governance & standards
│   ├── onboarding/                 # Architecture & coding guidelines reports
│   └── standards/                  # Modular standards (global & dotnet)
├── .opencode/                      # Project slash commands & agent extensions
└── Expenses/                       # Solution folder
    ├── Expenses.sln                # Visual Studio / MSBuild Solution file
    ├── compose.yaml                # Docker Compose orchestration
    ├── .dockerignore               # Docker ignore rules
    └── Expenses/                   # ASP.NET Core Blazor project
        ├── Expenses.csproj         # Target net10.0, EF Core packages, Nullable enabled
        ├── Program.cs              # Application entry point, DI container & middleware pipeline
        ├── appsettings.json        # Connection strings and logging configuration
        ├── appsettings.Development.json
        ├── Dockerfile              # Multi-stage container build
        ├── Properties/
        │   └── launchSettings.json
        ├── Data/                   # Persistence Layer
        │   ├── app.db              # SQLite database file
        │   ├── ApplicationUser.cs  # Identity user entity
        │   ├── ApplicationDbContext.cs # EF Core DbContext
        │   └── Migrations/         # EF Core migration history
        ├── Components/             # Presentation Layer (Blazor Interactive Server)
        │   ├── App.razor           # HTML host page and resource links
        │   ├── Routes.razor        # Blazor router and authentication route view
        │   ├── _Imports.razor      # Global Razor namespace imports
        │   ├── Layout/             # Layout components (MainLayout, NavMenu, ReconnectModal)
        │   ├── Pages/              # Routable Blazor pages (Home, Auth, Error, Weather, Counter)
        │   └── Account/            # ASP.NET Identity Razor pages, endpoints, and management
        └── wwwroot/                # Static assets (Bootstrap 5, app.css, favicon)
```

---

## 3. Discovered Architectural Patterns

### 3.1 Host & Runtime Configuration
- **Entry Point**: `Expenses/Expenses/Program.cs` uses modern C# top-level statements with `WebApplication.CreateBuilder(args)`.
- **Blazor Render Mode**: Configured with `builder.Services.AddRazorComponents().AddInteractiveServerComponents()` and rendered via `app.MapRazorComponents<App>().AddInteractiveServerRenderMode()`.
- **Authentication**: ASP.NET Identity core with Passkey support, cookie authentication scheme (`IdentityConstants.ApplicationScheme`), and custom identity endpoints mapped via `MapAdditionalIdentityEndpoints()`.
- **Database Access**: `ApplicationDbContext` registered with SQLite connection string `Data/app.db`.

### 3.2 Planned Layered Architecture for Expense Domain

To support the expense tracking application requirements, the application extends the current structure into clean architectural layers:

```text
┌──────────────────────────────────────────────────────────┐
│                   Blazor UI Components                   │
│      (Pages, Modals, Forms, Ingestion Review Panels)      │
└──────────────────────────┬───────────────────────────────┘
                           │ Invokes Services
                           ▼
┌──────────────────────────────────────────────────────────┐
│                     Service Layer                        │
│  ┌──────────────────────┐      ┌──────────────────────┐  │
│  │   Expense Service    │      │   Category Service   │  │
│  └──────────────────────┘      └──────────────────────┘  │
│  ┌──────────────────────┐      ┌──────────────────────┐  │
│  │ Deduplication Engine │      │  Ingestion Pipeline  │  │
│  └──────────────────────┘      └──────────────────────┘  │
└──────────────────────────┬───────────────────────────────┘
                           │ Interacts with Data Layer & Domain
                           ▼
┌──────────────────────────────────────────────────────────┐
│                  Domain & Data Models                    │
│   • Expense, Category, IngestionBatch, Rule Entities     │
│   • Enums, Value Objects (Money, Currency)               │
│   • Ingestion DTOs (RawTransaction, NormalizedExpense)   │
└──────────────────────────┬───────────────────────────────┘
                           │ Persists via EF Core
                           ▼
┌──────────────────────────────────────────────────────────┐
│                     Persistence                          │
│     ApplicationDbContext (SQLite) & Database Migrations   │
└──────────────────────────────────────────────────────────┘
```

---

## 4. Ingestion & Domain Pipeline Design

### 4.1 Ingestion Sources
1. **Screenshots / Receipt Images**:
   - Accepts image uploads (`.png`, `.jpg`, `.jpeg`, `.webp`).
   - OCR/Vision parser extracts transaction metadata: transaction date, amount, currency, merchant/vendor, and raw OCR text.
2. **Bank API JSON**:
   - Structured JSON payloads from bank APIs / Open Banking webhooks.
   - Normalizer extracts transaction ID, posted date, transaction amount, merchant name, category hints, and account ID.
3. **CSV Bank Statements**:
   - Delimiter detection (comma, semicolon, tab).
   - Configurable or auto-detected column mappings (Date, Description, Amount, Debit/Credit).
   - Robust number and date format culture parsing.

### 4.2 Deduplication Engine
- **Deterministic Fingerprinting**: Computes SHA256 hashes based on `(UserId + NormalizedDate + NormalizedAmount + NormalizedVendor)`.
- **Fuzzy / Window Matching**: Flags potential duplicates if:
  - Transaction date matches within +/- 3 days window.
  - Absolute amount matches exactly.
  - Vendor name matches with high similarity (Levenshtein / token matching).
- **Review Workflow**: Potential duplicates are marked as `PendingReview` with side-by-side comparison before confirmation.

### 4.3 Extensible Category System
- **Two-Tier Hierarchy**: Supports parent categories (e.g. `Food & Dining`, `Housing`, `Transportation`, `Utilities`) and subcategories (e.g. `Groceries`, `Restaurants`, `Coffee`).
- **Developer vs User Extensibility**:
  - Seed categories initialized via database migration / startup seed.
  - User can create, rename, archive, and nest custom categories dynamically.
- **Rule-Based Auto-Categorization**:
  - Keyword and regex pattern matching rules associated with categories.
  - Auto-learning: When a user manually categorizes a vendor, a rule can be created automatically for future transactions.

---

## 5. Architectural Quality Attributes & Constraints
- **Concurrency in Blazor Server**: Blazor Server components must never share a single `DbContext` instance across simultaneous async operations. In background pipelines or parallel tasks, use `IDbContextFactory<ApplicationDbContext>`.
- **Money Representation**: Always use `decimal` for monetary values. Never use `float` or `double`.
- **Time Zones**: Store all dates/timestamps in UTC (`DateTimeOffset` or `DateTime` with UTC kind). Format using user locale in Blazor components.
