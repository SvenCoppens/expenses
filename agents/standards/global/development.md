# Development Standards & Engineering Lifecycle

## 1. Dependency Injection Lifetimes

| Service Type | Recommended Lifetime | Rationale |
|---|---|---|
| EF Core `ApplicationDbContext` | Scoped | Matches the HTTP or Blazor Server circuit scope. |
| `IDbContextFactory<ApplicationDbContext>` | Singleton | Allows safe on-demand creation of isolated DbContexts in async background jobs. |
| Ingestion Parsers (`CsvExpenseParser`, etc.) | Transient or Singleton | Stateless data parsers without state persistence. |
| Domain Services (`ExpenseService`, `CategoryService`) | Scoped | Encapsulates business logic within the user session / circuit. |
| Rule Evaluation Engine | Scoped or Singleton | Evaluates category matching rules against incoming transactions. |

---

## 2. Result & Operation Outcome Pattern

Do not use exceptions for expected business errors (e.g., duplicate detected, unrecognized CSV delimiter, unparseable receipt image). Use an explicit `Result` or `OperationResult<T>` pattern:

```csharp
namespace Expenses.Common;

public sealed record Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }

    public static Result<T> Success(T value) => 
        new() { IsSuccess = true, Value = value };

    public static Result<T> Failure(string errorMessage, string? errorCode = null) => 
        new() { IsSuccess = false, ErrorMessage = errorMessage, ErrorCode = errorCode };
}

public sealed record Result
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }

    public static Result Success() => new() { IsSuccess = true };
    public static Result Failure(string errorMessage) => new() { IsSuccess = false, ErrorMessage = errorMessage };
}
```

---

## 3. Ingestion Pipeline Strategy Pattern

All transaction ingestion sources implement a uniform parser strategy:

```csharp
public interface ITransactionParser<TInput>
{
    Task<Result<IReadOnlyList<ParsedTransactionDto>>> ParseAsync(
        TInput input, 
        CancellationToken cancellationToken = default);
}
```

Where:
- `ICsvTransactionParser : ITransactionParser<Stream>`
- `IBankJsonTransactionParser : ITransactionParser<string>`
- `IScreenshotTransactionParser : ITransactionParser<Stream>`

---

## 4. Feature Implementation Checklist

Before completing any feature or bug fix:
1. **Domain & Data Design**: Define entities, DTOs, and relationships.
2. **EF Core Migrations**: If models change, create migration and verify it runs cleanly against SQLite.
3. **Service Logic**: Implement business operations with `Result<T>` and unit-testable interfaces.
4. **Presentation**: Build or update Blazor Razor components with interactive UI state and validation feedback.
5. **Self-Verification**: Run `dotnet build Expenses.sln` to guarantee zero compilation errors or breaking warnings.
