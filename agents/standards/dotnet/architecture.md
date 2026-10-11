# .NET & Blazor Architecture Standards

## 1. Blazor Interactive Server Lifecycle

### 1.1 Render Mode & Circuit Model
- The application uses `AddInteractiveServerComponents()` and renders via `@rendermode InteractiveServer` / `.AddInteractiveServerRenderMode()`.
- State persists across interactions within the active SignalR circuit.
- Long-running async work should yield control with `await Task.Yield()` or run on background worker threads if parsing CPU-heavy data (e.g. image processing or massive CSVs) to prevent stalling SignalR heartbeats.

### 1.2 Component Lifecycle Methods
- `OnInitializedAsync()`: Load initial lightweight state. Avoid heavy blocking network calls.
- `OnParametersSetAsync()`: Update component state when parent cascading values or route parameters change.
- `StateHasChanged()`: Blazor automatically calls this for Blazor UI events (like `onclick`). Only invoke `InvokeAsync(StateHasChanged)` explicitly when reacting to external async events, timers, or background parser completion callbacks.

---

## 2. EF Core Concurrency in Blazor Server

### 2.1 The Single-Circuit Concurrency Hazard
In Blazor Server, standard scoped services (including `ApplicationDbContext`) live as long as the user's SignalR connection. If a user triggers two asynchronous operations concurrently (e.g. quick sequential button clicks or a background polling loop), EF Core will throw:
> `InvalidOperationException: A second operation was started on this context instance before a previous operation completed.`

### 2.2 Mitigation Strategy
1. **For Service-based operations**: Inject `IDbContextFactory<ApplicationDbContext>` into background services, ingestion workers, and parallel tasks.
2. **Context Lifecycles**:
   ```csharp
   public async Task<ExpenseSummary> GetSummaryAsync(string userId, CancellationToken ct)
   {
       await using var context = await dbContextFactory.CreateDbContextAsync(ct);
       return await context.Expenses
           .Where(e => e.UserId == userId)
           .GroupBy(e => e.CategoryId)
           .Select(...)
           .FirstOrDefaultAsync(ct);
   }
   ```
3. In standard Razor pages executing single synchronous or awaited operations, standard scoped services are acceptable provided operations are not run concurrently on the same context instance.

---

## 3. UI Component Separation

- Keep Razor files focused on rendering and user interaction.
- Delegate data retrieval, business validation, deduplication checks, and file parsing to dedicated domain services.
- Never write direct raw SQL or complex LINQ joins directly inside Razor `@code` blocks. Call service methods that return DTOs or ViewModels.
