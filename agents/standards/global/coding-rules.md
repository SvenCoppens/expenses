# Global Coding Rules & Golden Standards

## 1. Safety-Critical Golden Rules

### 1.1 Memory Leak & Resource Cleanup
- **Disposal of Streams and Files**: Always dispose of image file streams, CSV file streams, and byte buffers using `await using` or `using` blocks. Never leave file upload streams unclosed.
- **Blazor Event Subscriptions**: Any Blazor component that subscribes to external events, services, or message brokers must implement `IDisposable` or `IAsyncDisposable` and unsubscribe cleanly in `Dispose()` / `DisposeAsync()`.
- **CancellationToken Propagation**: Every asynchronous method must accept and pass down a `CancellationToken` (defaulting to `default` or `CancellationToken.None` at boundaries).

### 1.2 Database Performance & N+1 Query Elimination
- **No Database Calls in Loops**: Never execute an EF Core query inside a `foreach`, `for`, or `.Select()` enumeration loop.
- **Bulk Ingestion Operations**: When importing multiple transactions from a CSV or bank API, query existing duplicate fingerprints in a single batch query (`.Where(e => fingerprints.Contains(e.Fingerprint))`) and insert new records with `AddRangeAsync()`.
- **Read-Only Projections**: Always use `.AsNoTracking()` for read operations. Do not track entities when only displaying or calculating aggregates.

### 1.3 Financial Accuracy & Decimal Precision
- **Strictly `decimal` for Monetary Amounts**: NEVER use `float` or `double` for currency amounts, balances, tax, or fees. Floating point arithmetic introduces precision loss.
- **Explicit Rounding Rules**: When performing currency calculations, use explicit rounding rules (e.g., `decimal.Round(amount, 2, MidpointRounding.AwayFromZero)`).
- **ISO 4217 Currency Codes**: Always store currencies as standard 3-letter ISO codes (e.g., `"EUR"`, `"USD"`), not ambiguous currency symbols (`"$"`, `"€"`).

### 1.4 Immutability & CopyWith / Patch Safety
- **Immutable DTOs**: Use C# `record` types with `init`-only properties for ingestion payloads and data transfer.
- **Explicit Patch Handling**: When patching or partially updating category rules or expense fields, avoid accidental nullification by differentiating between "field omitted from update" and "field explicitly cleared to null".

### 1.5 Temporal Consistency (UTC Rules)
- **UTC Timestamps**: Persist all system timestamps and transaction dates in UTC (`DateTimeOffset` or `DateTime` with `DateTimeKind.Utc`).
- **User Local Presentation**: Convert UTC timestamps to the user's localized timezone only in the Blazor presentation layer.

---

## 2. Formatting & Clean Code Standards

### 2.1 File Structure
- File-scoped namespaces only: `namespace Expenses.Services;`
- Clean `using` directives at top of file, relying on implicit global usings for common namespaces (`System`, `System.Collections.Generic`, `System.Threading.Tasks`).
- One primary class or record per file matching the filename.

### 2.2 Modern C# 13 Idioms
- **Primary Constructors**: Use primary constructors for dependency injection.
- **Pattern Matching**: Use expressive pattern matching (`is not null`, `switch` expressions) over deep nested `if-else` blocks.
- **Collection Expressions**: Use C# 12/13 collection expressions `[]` for empty or initialized lists and arrays:
  ```csharp
  List<ExpenseItem> items = [];
  ```
