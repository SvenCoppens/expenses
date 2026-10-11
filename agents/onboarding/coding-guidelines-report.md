# Coding Guidelines & Conventions Report: Expenses Project

## 1. Language & Compiler Environment
- **C# Version**: C# 13 (.NET 10.0 runtime)
- **Nullable Context**: `<Nullable>enable</Nullable>` is enabled across the project.
- **Implicit Usings**: `<ImplicitUsings>enable</ImplicitUsings>` is active.
- **File-Scoped Namespaces**: Required across all C# source files (`namespace Expenses.Data;`).

---

## 2. Naming Conventions

| Artifact | Convention | Example |
|---|---|---|
| Class / Struct / Record | PascalCase | `ExpenseItem`, `ApplicationDbContext`, `NormalizedTransaction` |
| Interface | `I` + PascalCase | `IExpenseService`, `IIngestionParser` |
| Method | PascalCase | `GetExpensesAsync()`, `CategorizeTransaction()` |
| Property | PascalCase | `Amount`, `CreatedAtUtc`, `CategoryName` |
| Method Parameter | camelCase | `userId`, `cancellationToken`, `fileStream` |
| Local Variable | camelCase | `duplicateCandidate`, `totalAmount` |
| Private Field (if used) | `_` + camelCase | `_logger`, `_dbContextFactory` |
| Constant | PascalCase | `DefaultCurrency`, `MaxBatchSize` |
| Enum Type & Values | PascalCase | `enum IngestionSource { Screenshot, BankApi, Csv }` |
| Blazor Component File | PascalCase (`.razor`) | `ExpenseList.razor`, `CategoryManager.razor` |
| Razor Code-Behind / CSS | PascalCase | `ExpenseList.razor.cs`, `ExpenseList.razor.css` |
| C# Source Files | PascalCase (`.cs`) | `ExpenseService.cs`, `ApplicationUser.cs` |
| Folders / Directories | PascalCase | `Components/`, `Data/`, `Services/`, `Models/` |

---

## 3. C# 13 & .NET 10 Idiomatic Conventions

### 3.1 Primary Constructors
Prefer primary constructors for dependency injection in classes and services:
```csharp
// Recommended
public class ExpenseService(
    ApplicationDbContext dbContext,
    ILogger<ExpenseService> logger) : IExpenseService
{
    // dbContext and logger are available directly in methods
}
```

### 3.2 Records for DTOs and Ingestion Contracts
Use `record` or `readonly record struct` for immutable data transfer objects and parsed data:
```csharp
public sealed record RawTransactionDto(
    DateTime TransactionDate,
    decimal Amount,
    string Currency,
    string RawDescription,
    string? MerchantName);
```

### 3.3 Strict Nullable Reference Types
- Never suppress nullable warnings with `!` unless guaranteed by framework contract (e.g. EF Core navigation properties with appropriate configuration).
- Use `?` explicitly for nullable fields (`string? Notes`, `Guid? CategoryId`).
- For update operations or partial patches, avoid ambiguous null writes; differentiate omitted values from intentional nulls.

---

## 4. Blazor Component Best Practices

### 4.1 Component Structure
Organize Razor components in a clear section order:
1. Directives: `@page`, `@attribute`, `@rendermode`, `@inject`, `@using`
2. Component markup (HTML & Razor elements)
3. `@code { ... }` block (or code-behind `.razor.cs` for complex components)

### 4.2 Lifecycle & Async Execution
- All asynchronous calls in lifecycle methods must be properly awaited (`OnInitializedAsync`, `OnParametersSetAsync`).
- Always pass `CancellationToken` where available.
- If subscribing to events or external timers, implement `IDisposable` or `IAsyncDisposable` and unsubscribe.

### 4.3 Interactive Server State Safety
- Avoid triggering long-running CPU-intensive parsing directly on the Blazor UI thread. Delegate CPU or OCR tasks to background queues or async services to keep Blazor SignalR circuits responsive.
- Handle circuit disconnections gracefully (`ReconnectModal` is active in `App.razor`).

---

## 5. Persistence & Entity Framework Core Guidelines

### 5.1 Query Performance & Read Operations
- Always use `.AsNoTracking()` for queries feeding read-only views, reports, and UI grids.
- Project directly to DTOs or ViewModels using `.Select(...)` when displaying expense summaries, avoiding loading unnecessary entity graphs.
- Never execute queries inside loops (avoid N+1). Fetch bulk data with `.Where(e => ids.Contains(e.Id))` or join upfront.

### 5.2 DbContext Concurrency in Blazor
- In standard Scoped service lifecycles, Blazor Server shares the scoped DbContext across component renderings within the circuit.
- If multiple concurrent async operations are needed (e.g., streaming file import while browsing expenses), inject `IDbContextFactory<ApplicationDbContext>` and create isolated DbContext scopes:
  ```csharp
  await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
  ```

---

## 6. Error Handling & Result Pattern

- Avoid using exceptions for expected domain outcomes (e.g., duplicate transaction detected, malformed CSV row, unsupported image format).
- Use a `Result<T>` or `OperationResult` pattern:
```csharp
public sealed record Result<T>(bool IsSuccess, T? Value, string? ErrorMessage)
{
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(string errorMessage) => new(false, default, errorMessage);
}
```
- Reserve exceptions for unexpected system crashes, database outages, or fatal internal bugs.
