# Expenses - Agent Context & Guidelines

This file serves as the root context file for all agents working on this project. 
To preserve context and maximize prompt efficiency, DO NOT preemptively load all standards files. Load them dynamically ONLY when editing files under their specific domains.

## Project Overview
- **Tech Stack**: C# 13 / .NET 10.0, ASP.NET Core Blazor Web App (`InteractiveServer` render mode), Entity Framework Core 10.0.9 (SQLite at `Data/app.db`), ASP.NET Core Identity with Passkey support.
- **Domain Purpose**: Personal Expense Tracker supporting multi-channel ingestion (Screenshots/Vision OCR, Bank API JSON, and CSV bank statements), duplicate detection & filtering engine, and an extensible category hierarchy (developer-seeded and user-managed).
- **Core Patterns**: Layered Architecture (`Components/` -> `Services/` -> `Models/` -> `Data/`), Explicit `Result<T>` pattern, Strategy/Pipeline pattern for transaction parsers, `IDbContextFactory` for Blazor Server concurrency safety.
- **Build Commands**:
  - Repo root: `dotnet build Expenses/Expenses.sln`
  - Solution root: `dotnet build Expenses.sln`
- **Database Migrations**:
  - `dotnet ef database update --project Expenses/Expenses/Expenses.csproj --startup-project Expenses/Expenses/Expenses.csproj`

## Lazy-Loaded Standards Registry (Load on demand)
- `@agents/standards/global/coding-rules.md` — Safety-critical Golden Rules: stream and file resource cleanup, N+1 query elimination in batch imports, strict `decimal` type for monetary amounts, immutable record DTOs, and UTC timestamp enforcement.
- `@agents/standards/global/development.md` — Service dependency injection lifetimes, Result/OperationResult pattern, uniform transaction parser strategy (`ITransactionParser<TInput>`), and feature implementation checklist.
- `@agents/standards/global/cross-cutting.md` — Semantic structured logging (`ILogger`), sensitive bank PII masking (last-4-digit display), Blazor user feedback/toasts, and secure file upload validation for receipts and CSVs.
- `@agents/standards/dotnet/architecture.md` — Blazor Interactive Server lifecycle, SignalR circuit responsiveness, preventing EF Core concurrency exceptions via `IDbContextFactory`, and strict presentation-to-service decoupling.
- `@agents/standards/dotnet/layers.md` — Directory structure, namespace hierarchy (`Expenses.*`), and strict boundary enforcement prohibiting direct DbContext access from Blazor components.
- `@agents/standards/dotnet/testing.md` — Testing standards using xUnit, FluentAssertions, bUnit for Blazor component rendering, SQLite in-memory database testing, and AAA test pattern.
- `@agents/standards/dotnet/build-and-generation.md` — CLI commands for compilation, running the application, creating and applying EF Core SQLite migrations, and Docker workflows.
- `@agents/standards/dotnet/expenses-domain.md` — Comprehensive domain blueprint covering multi-source ingestion (Screenshots/OCR, Bank JSON, CSV), deduplication algorithms (SHA256 fingerprinting & +/- 3 day fuzzy window matching), and extensible two-tier category taxonomy with auto-learning rules.
