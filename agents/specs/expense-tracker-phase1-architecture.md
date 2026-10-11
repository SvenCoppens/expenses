# Phase 1: Core Domain, Database & Entity Services - Architecture Plan

## 1. User Story

### Description

As a user of the Personal Expense Tracker, I need my financial data (transactions and two-tier categories) to be modeled with strict monetary precision, stored persistently in SQLite, and isolated to my user account so that my records remain private, consistent, and resilient to concurrency issues in the Blazor Server environment.

As a developer, I need a robust domain foundation comprising immutable DTOs, domain models (`Transaction`, `Category`, `TransactionType`), a standard `Result<T>` pipeline, thread-safe `IDbContextFactory<ApplicationDbContext>` lifecycle handling, and thoroughly tested service abstractions (`ITransactionService`, `ICategoryService`) with SQLite in-memory test fixtures before UI components are introduced.

### Acceptance Criteria

- [x] **Domain Models & Precision**:
  - `TransactionType` enum defined with `Expense = 1`, `Income = 2`, `Transfer = 3`.
  - `Category` entity defined supporting a two-tier hierarchy (`ParentCategoryId` self-referencing foreign key, navigation collection `Subcategories`).
  - `Transaction` entity defined with strict `decimal` amount (`HasPrecision(18, 2)`), `DateOnly Date` for calendar transaction date, and `DateTime CreatedAtUtc` for audit timestamp.
- [x] **Result Pattern**:
  - `Result<T>` and `Result` types implemented in `Expenses.Common` with factory methods `Success()`, `Failure(errorMessage, errorCode)`, eliminating exceptions for anticipated business and validation errors.
- [x] **EF Core & Blazor Concurrency Configuration**:
  - `CategoryConfiguration` and `TransactionConfiguration` configured via EF Core Fluent API with required indexes (`(UserId, Date)` on `Transactions`, `UserId` on `Categories`).
  - `ApplicationDbContext` updated with `DbSet<Category>` and `DbSet<Transaction>`.
  - `Program.cs` configured with `IDbContextFactory<ApplicationDbContext>` using SQLite connection string while preserving ASP.NET Core Identity authentication functionality.
  - EF Core migration generated and applied cleanly against SQLite.
- [x] **Per-User Scoped Category Service**:
  - `ICategoryService` defines methods: `GetCategoriesAsync`, `GetCategoryByIdAsync`, `CreateCategoryAsync`, `UpdateCategoryAsync`, `DeleteCategoryAsync`.
  - All operations require and enforce `userId` filtering.
  - Hierarchy validation ensures maximum depth of 2 (parent categories cannot have parents; subcategories cannot be parents).
- [x] **Per-User Scoped Transaction Service**:
  - `ITransactionService` defines methods: `GetTransactionsAsync` (with filtering and date ranges), `GetTransactionByIdAsync`, `CreateTransactionAsync`, `UpdateTransactionAsync`, `DeleteTransactionAsync`, `DeleteTransactionsBulkAsync`.
  - All operations enforce `userId` ownership.
  - Service validates that assigned `CategoryId` and `SubcategoryId` belong to the user and that `SubcategoryId` is a valid child of `CategoryId`.
  - Read queries execute with `.AsNoTracking()`.
- [x] **Test Project Setup**:
  - Test project `Expenses.Tests` added to the solution with `net10.0`, referencing `xUnit`, `FluentAssertions`, `NSubstitute`, and `Microsoft.Data.Sqlite`.
  - SQLite in-memory test fixture verifies entity mapping, per-user isolation, and service operations.

### Non-Functional Requirements

- **Performance**:
  - All read-only queries must use `.AsNoTracking()` to eliminate EF Core tracking overhead.
  - Database queries must leverage indexes on `Transactions(UserId, Date)` and `Categories(UserId)`.
- **Security & Multi-Tenancy**:
  - Strict tenant boundary: Every query and mutation must validate the calling `userId`. A user must never be able to read, modify, or delete another user's records.
- **Concurrency & Reliability**:
  - All database interactions inside domain services must create short-lived contexts via `IDbContextFactory<ApplicationDbContext>` using `await using var context = await dbContextFactory.CreateDbContextAsync(ct)` to prevent Blazor Server SignalR circuit concurrency crashes (`InvalidOperationException`).
- **Code Standards**:
  - Modern C# 13 idioms: file-scoped namespaces, primary constructors for dependency injection, collection expressions `[]`, and explicit `CancellationToken` propagation across all asynchronous methods.

---

## 2. Architecture Overview

### Affected Components / Layers

- [x] **Test Layer (`Expenses.Tests/`)**: New xUnit test project configured with SQLite in-memory database fixture to support TDD for domain entities, EF Core configurations, and service implementations.
- [x] **Common Layer (`Expenses/Common/`)**: Generic `Result<T>` and non-generic `Result` records for uniform operation outcome handling without exceptions.
- [x] **Domain Models (`Expenses/Models/`)**:
  - `Models/Enums/TransactionType.cs`: Transaction classification enum.
  - `Models/Entities/Category.cs`: Self-referencing hierarchical category entity.
  - `Models/Entities/Transaction.cs`: Financial transaction entity.
  - `Models/DTOs/CategoryDtos.cs`: Immutable request/response records for categories.
  - `Models/DTOs/TransactionDtos.cs`: Immutable request/response records and filter criteria for transactions.
- [x] **Data Layer (`Expenses/Data/`)**:
  - `Data/Configurations/CategoryConfiguration.cs`: Fluent API mapping, indexes, and delete behaviors for categories.
  - `Data/Configurations/TransactionConfiguration.cs`: Fluent API mapping, monetary precision (`18, 2`), and indexes for transactions.
  - `Data/ApplicationDbContext.cs`: Integration of new `DbSet` properties and assembly-level configuration loading.
  - `Data/Migrations/`: EF Core SQLite migration for schema generation.
- [x] **Services Layer (`Expenses/Services/`)**:
  - `Services/Categories/`: `ICategoryService` and `CategoryService` with two-tier validation and tenant scoping.
  - `Services/Transactions/`: `ITransactionService` and `TransactionService` with filtering, bulk operations, and tenant scoping.
- [x] **Host Configuration (`Expenses/Program.cs`)**: Registration of `IDbContextFactory<ApplicationDbContext>`, `ICategoryService`, and `ITransactionService`.

### Data Flow Diagram

```text
[Blazor Presentation Layer (Future UI) / Unit Test]
        │
        │ Invoke Service Method (userId, request, CancellationToken)
        ▼
[Domain Service: ITransactionService / ICategoryService]
        │
        ├─► Validates Request & Tenant Ownership (UserId)
        │
        ├─► dbContextFactory.CreateDbContextAsync(ct)
        │        │
        │        ▼
        │   [ApplicationDbContext (Short-lived, circuit-safe instance)]
        │        │
        │        ▼
        │   [SQLite Database (Data/app.db / In-Memory for Tests)]
        │
        ▼
Returns Result<T> / Result (Success or Failure with ErrorMessage)
```

### Key Design Decisions

1. **`IDbContextFactory` for Blazor Server Circuit Safety**:
   - *Rationale*: Blazor Server circuits maintain persistent SignalR connections. Registering `ApplicationDbContext` purely as a scoped dependency means a single context instance is shared across the circuit. Simultaneous asynchronous events (e.g., rapid user actions or background updates) cause EF Core to throw `InvalidOperationException: A second operation was started on this context instance before a previous operation completed.`
   - *Implementation*: Services inject `IDbContextFactory<ApplicationDbContext>` and create isolated, short-lived instances using `await using var context = await dbContextFactory.CreateDbContextAsync(ct)`. In `Program.cs`, `builder.Services.AddDbContextFactory<ApplicationDbContext>(...)` registers both `IDbContextFactory<ApplicationDbContext>` and scoped `ApplicationDbContext` (needed by ASP.NET Core Identity stores).

2. **Strict Two-Tier Category Taxonomy via Self-Referencing Entity**:
   - *Rationale*: Personal finances benefit from high clarity without deep arbitrary nesting. The schema models categories with `ParentCategoryId` nullable foreign key.
   - *Rule*: Parent categories have `ParentCategoryId == null`. Subcategories have `ParentCategoryId != null`. The `CategoryService` validates that a subcategory cannot have another subcategory as its parent, strictly enforcing a maximum tree depth of 2.
   - *Delete Behavior*: Deleting a parent category cascades to its subcategories, but deleting a category referenced by transactions is restricted (`DeleteBehavior.Restrict`) to prevent accidental deletion of historical records without explicit reassignment.

3. **`Result<T>` Pattern for Flow Control**:
   - *Rationale*: Avoid using exceptions for predictable business validation failures (e.g., category not found, mismatched subcategory, unauthorized access attempt).
   - *Implementation*: Use `Result<T>` and `Result` records with `IsSuccess`, `Value`, `ErrorMessage`, and `ErrorCode`. Exceptions are reserved strictly for unexpected infrastructure failures.

4. **Multi-Tenancy Enforced at Service Boundary**:
   - *Rationale*: In an ASP.NET Core Identity multi-user system, all financial records must be isolated to the authenticated user.
   - *Implementation*: Every service method requires `userId`. Queries unconditionally apply `.Where(e => e.UserId == userId)`. Mutation methods (update, delete) first query for the entity matching both `Id` and `UserId`. If not found, a failure result (`"Record not found or access denied"`) is returned.

5. **SQLite In-Memory Testing Architecture**:
   - *Rationale*: EF Core InMemory provider does not enforce relational constraints, foreign keys, or SQLite SQL syntax.
   - *Implementation*: The test suite creates a dedicated `Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:")`, opens it for the fixture lifetime, and uses `IDbContextFactory` or `DbContextOptionsBuilder` to execute real SQLite queries and schema creation (`context.Database.EnsureCreated()`).

---

## 3. Implementation Steps

_Note: The Test_Engineer will write failing tests against these steps and touchpoints before the Software_Engineer begins implementation._

### Step 1: Test Project Setup (`Expenses.Tests`)

**Status**: ✅ Done

#### Objectives

- Create the `Expenses.Tests` xUnit project targeting `net10.0`.
- Add test dependencies: `xUnit`, `FluentAssertions`, `NSubstitute`, `Microsoft.Data.Sqlite`, and project reference to `Expenses.csproj`.
- Add the test project to `Expenses.sln`.
- Implement a reusable SQLite in-memory test fixture helper (`SqliteTestDbFixture`).

#### Touchpoints (Files to Create/Modify)

- `Expenses.Tests/Expenses.Tests.csproj` (create)
- `Expenses.sln` (modify)
- `Expenses.Tests/Fixtures/SqliteTestDbFixture.cs` (create)

#### Pseudo-code / Technical Notes

```csharp
// Project: Expenses.Tests/Expenses.Tests.csproj
// TargetFramework: net10.0
// PackageReferences:
//   - xunit (v2.9+)
//   - xunit.runner.visualstudio (v2.8+)
//   - FluentAssertions (v7.x or v8.x)
//   - NSubstitute (v5.x+)
//   - Microsoft.Data.Sqlite (v10.0.x)
//   - Microsoft.EntityFrameworkCore.Sqlite (v10.0.x)
//   - Microsoft.NET.Test.Sdk

namespace Expenses.Tests.Fixtures;

public sealed class SqliteTestDbFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    public IDbContextFactory<ApplicationDbContext> Factory { get; }

    public SqliteTestDbFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Custom implementation of IDbContextFactory returning new context with same open connection
        Factory = new TestDbContextFactory(options);

        using var context = new ApplicationDbContext(options);
        context.Database.EnsureCreated();
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }
}
```

#### Definition of Done

- [x] `Expenses.Tests` project builds and is recognized by `dotnet test Expenses.sln`.
- [x] `SqliteTestDbFixture` initializes an in-memory SQLite schema and provides a working `IDbContextFactory<ApplicationDbContext>`.
- [x] Code compiles without errors.

---

### Step 2: Common Result Types (`Expenses.Common`)

**Status**: ✅ Done

#### Objectives

- Implement the `Result<T>` and `Result` immutable records in `Expenses/Common/Result.cs`.
- Provide factory methods `Success(T value)`, `Failure(string errorMessage, string? errorCode = null)`, and implicit operators or status properties.

#### Touchpoints (Files to Create/Modify)

- `Expenses/Common/Result.cs` (create)

#### Pseudo-code / Technical Notes

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
    public string? ErrorCode { get; init; }

    public static Result Success() =>
        new() { IsSuccess = true };

    public static Result Failure(string errorMessage, string? errorCode = null) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage, ErrorCode = errorCode };
}
```

#### Definition of Done

- [x] `Result<T>` and `Result` types match project standards defined in `agents/standards/global/development.md`.
- [x] Unit tests in `Expenses.Tests/Common/ResultTests.cs` pass.
- [x] Code compiles without errors.

---

### Step 3: Domain Models, Enums, and DTOs (`Expenses.Models`)

**Status**: ✅ Done

#### Objectives

- Define `TransactionType` enum (`Expense = 1`, `Income = 2`, `Transfer = 3`).
- Define `Category` domain entity with self-referencing relationship for two-tier hierarchy.
- Define `Transaction` domain entity with explicit `decimal` amount, `DateOnly Date`, and UTC audit timestamp.
- Define request and response DTO records for categories and transactions.

#### Touchpoints (Files to Create/Modify)

- `Expenses/Models/Enums/TransactionType.cs` (create)
- `Expenses/Models/Entities/Category.cs` (create)
- `Expenses/Models/Entities/Transaction.cs` (create)
- `Expenses/Models/DTOs/CategoryDtos.cs` (create)
- `Expenses/Models/DTOs/TransactionDtos.cs` (create)

#### Pseudo-code / Technical Notes

```csharp
namespace Expenses.Models.Enums;

public enum TransactionType
{
    Expense = 1,
    Income = 2,
    Transfer = 3
}

namespace Expenses.Models.Entities;

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? ParentCategoryId { get; set; }
    public string? ColorCode { get; set; }
    public string? Icon { get; set; }
    public int DisplayOrder { get; set; }

    // Navigation properties
    public Category? ParentCategory { get; set; }
    public ICollection<Category> Subcategories { get; set; } = [];
    public ICollection<Transaction> Transactions { get; set; } = [];
}

public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public TransactionType Type { get; set; } = TransactionType.Expense;
    public Guid? CategoryId { get; set; }
    public Guid? SubcategoryId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Category? Category { get; set; }
    public Category? Subcategory { get; set; }
}

namespace Expenses.Models.DTOs;

public sealed record CategoryDto(
    Guid Id,
    string Name,
    Guid? ParentCategoryId,
    string? ColorCode,
    string? Icon,
    int DisplayOrder,
    IReadOnlyList<CategoryDto> Subcategories
);

public sealed record CreateCategoryRequest(
    string Name,
    Guid? ParentCategoryId = null,
    string? ColorCode = null,
    string? Icon = null,
    int DisplayOrder = 0
);

public sealed record UpdateCategoryRequest(
    string Name,
    Guid? ParentCategoryId = null,
    string? ColorCode = null,
    string? Icon = null,
    int DisplayOrder = 0
);

public sealed record TransactionDto(
    Guid Id,
    DateOnly Date,
    decimal Amount,
    string Description,
    TransactionType Type,
    Guid? CategoryId,
    string? CategoryName,
    Guid? SubcategoryId,
    string? SubcategoryName,
    string? Notes,
    DateTime CreatedAtUtc
);

public sealed record CreateTransactionRequest(
    DateOnly Date,
    decimal Amount,
    string Description,
    TransactionType Type,
    Guid? CategoryId = null,
    Guid? SubcategoryId = null,
    string? Notes = null
);

public sealed record UpdateTransactionRequest(
    DateOnly Date,
    decimal Amount,
    string Description,
    TransactionType Type,
    Guid? CategoryId = null,
    Guid? SubcategoryId = null,
    string? Notes = null
);

public sealed record TransactionFilter(
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    IReadOnlyList<Guid>? CategoryIds = null,
    TransactionType? Type = null,
    string? SearchTerm = null
);
```

#### Definition of Done

- [x] All entity models and DTOs match PRS Pillar 4 definitions.
- [x] Properties adhere to strict naming, file-scoped namespaces, and collection expressions `[]`.
- [x] Code compiles without errors.

---

### Step 4: EF Core Data Layer & Migration (`Expenses.Data` & `Program.cs`)

**Status**: ✅ Done

#### Objectives

- Create EF Core configuration classes `CategoryConfiguration` and `TransactionConfiguration` under `Expenses/Data/Configurations/`.
- Configure indexes: `Transactions(UserId, Date)` and `Categories(UserId)`.
- Configure monetary decimal precision `HasPrecision(18, 2)` for `Transaction.Amount`.
- Update `ApplicationDbContext` to expose `DbSet<Category>` and `DbSet<Transaction>` and apply configurations via `ApplyConfigurationsFromAssembly`.
- Update `Program.cs` to register `IDbContextFactory<ApplicationDbContext>` using SQLite.
- Generate and apply initial EF Core SQLite migration for the core domain.

#### Touchpoints (Files to Create/Modify)

- `Expenses/Data/Configurations/CategoryConfiguration.cs` (create)
- `Expenses/Data/Configurations/TransactionConfiguration.cs` (create)
- `Expenses/Data/ApplicationDbContext.cs` (modify)
- `Expenses/Program.cs` (modify)
- `Expenses/Data/Migrations/*_AddExpenseTrackerCoreDomain.cs` (generate via EF Core CLI)

#### Pseudo-code / Technical Notes

```csharp
// Expenses/Data/Configurations/CategoryConfiguration.cs
namespace Expenses.Data.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.ColorCode)
            .HasMaxLength(7);

        builder.Property(c => c.Icon)
            .HasMaxLength(50);

        builder.HasIndex(c => c.UserId);

        builder.HasOne(c => c.ParentCategory)
            .WithMany(c => c.Subcategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

// Expenses/Data/Configurations/TransactionConfiguration.cs
namespace Expenses.Data.Configurations;

public class TransactionConfiguration : IEntityTypeConfiguration<Transaction>
{
    public void Configure(EntityTypeBuilder<Transaction> builder)
    {
        builder.ToTable("Transactions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(t => t.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(t => t.Description)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(t => t.Notes)
            .HasMaxLength(1000);

        builder.Property(t => t.Type)
            .IsRequired();

        builder.HasIndex(t => new { t.UserId, t.Date });

        builder.HasOne(t => t.Category)
            .WithMany(c => c.Transactions)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.Subcategory)
            .WithMany()
            .HasForeignKey(t => t.SubcategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

// Expenses/Data/ApplicationDbContext.cs
public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Transaction> Transactions => Set<Transaction>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}

// Expenses/Program.cs (Replace AddDbContext with AddDbContextFactory)
builder.Services.AddDbContextFactory<ApplicationDbContext>(options =>
    options.UseSqlite(connectionString));
```

#### Definition of Done

- [x] EF Core entity configurations correctly map precision, indexes, foreign keys, and delete behaviors.
- [x] `AddDbContextFactory<ApplicationDbContext>` is registered in `Program.cs`.
- [x] Migration is generated and verified with `dotnet ef database update`.
- [x] Code compiles without errors.

---

### Step 5: Category Service Implementation (`Expenses.Services.Categories`)

**Status**: ✅ Done

#### Objectives

- Define `ICategoryService` interface with asynchronous methods accepting `CancellationToken`.
- Implement `CategoryService` using `IDbContextFactory<ApplicationDbContext>`.
- Enforce strict per-user tenant scoping (`UserId`).
- Enforce two-tier hierarchy validation (subcategories cannot have subcategories; parent category must belong to user).
- Prevent category deletion if it has associated transactions, or return an explicit business error `Result.Failure`.

#### Touchpoints (Files to Create/Modify)

- `Expenses/Services/Categories/ICategoryService.cs` (create)
- `Expenses/Services/Categories/CategoryService.cs` (create)
- `Expenses.Tests/Services/CategoryServiceTests.cs` (create)

#### Pseudo-code / Technical Notes

```csharp
namespace Expenses.Services.Categories;

public interface ICategoryService
{
    Task<Result<IReadOnlyList<CategoryDto>>> GetCategoriesAsync(
        string userId, 
        CancellationToken cancellationToken = default);

    Task<Result<CategoryDto>> GetCategoryByIdAsync(
        string userId, 
        Guid id, 
        CancellationToken cancellationToken = default);

    Task<Result<CategoryDto>> CreateCategoryAsync(
        string userId, 
        CreateCategoryRequest request, 
        CancellationToken cancellationToken = default);

    Task<Result<CategoryDto>> UpdateCategoryAsync(
        string userId, 
        Guid id, 
        UpdateCategoryRequest request, 
        CancellationToken cancellationToken = default);

    Task<Result> DeleteCategoryAsync(
        string userId, 
        Guid id, 
        CancellationToken cancellationToken = default);
}

public class CategoryService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ILogger<CategoryService> logger) : ICategoryService
{
    public async Task<Result<IReadOnlyList<CategoryDto>>> GetCategoriesAsync(
        string userId, 
        CancellationToken ct = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);
        
        // Fetch top-level categories and include subcategories, sorted by DisplayOrder
        var categories = await context.Categories
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.ParentCategoryId == null)
            .Include(c => c.Subcategories)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(ct);

        var dtos = categories.Select(MapToDto).ToList();
        return Result<IReadOnlyList<CategoryDto>>.Success(dtos);
    }

    public async Task<Result<CategoryDto>> CreateCategoryAsync(
        string userId, 
        CreateCategoryRequest request, 
        CancellationToken ct = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);

        if (request.ParentCategoryId.HasValue)
        {
            var parent = await context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ParentCategoryId.Value && c.UserId == userId, ct);

            if (parent is null)
                return Result<CategoryDto>.Failure("Parent category not found.", "PARENT_NOT_FOUND");

            if (parent.ParentCategoryId.HasValue)
                return Result<CategoryDto>.Failure("Cannot nest categories more than 2 levels deep.", "MAX_DEPTH_EXCEEDED");
        }

        var category = new Category
        {
            UserId = userId,
            Name = request.Name.Trim(),
            ParentCategoryId = request.ParentCategoryId,
            ColorCode = request.ColorCode,
            Icon = request.Icon,
            DisplayOrder = request.DisplayOrder
        };

        context.Categories.Add(category);
        await context.SaveChangesAsync(ct);

        return Result<CategoryDto>.Success(MapToDto(category));
    }
}
```

#### Definition of Done

- [x] All methods in `ICategoryService` are implemented and return `Result<T>` or `Result`.
- [x] Two-tier hierarchy rules are strictly enforced.
- [x] Multi-tenant isolation verified with unit tests in `CategoryServiceTests.cs`.
- [x] Code compiles without errors.

---

### Step 6: Transaction Service Implementation (`Expenses.Services.Transactions`)

**Status**: ✅ Done

#### Objectives

- Define `ITransactionService` interface with methods for filtered retrieval, single get, creation, update, deletion, and bulk deletion.
- Implement `TransactionService` using `IDbContextFactory<ApplicationDbContext>`.
- Enforce per-user scoping and ensure read operations use `.AsNoTracking()`.
- Validate that `CategoryId` and `SubcategoryId` belong to the user, and `SubcategoryId` belongs to `CategoryId`.
- Support bulk transaction deletion within a single database operation.

#### Touchpoints (Files to Create/Modify)

- `Expenses/Services/Transactions/ITransactionService.cs` (create)
- `Expenses/Services/Transactions/TransactionService.cs` (create)
- `Expenses.Tests/Services/TransactionServiceTests.cs` (create)

#### Pseudo-code / Technical Notes

```csharp
namespace Expenses.Services.Transactions;

public interface ITransactionService
{
    Task<Result<IReadOnlyList<TransactionDto>>> GetTransactionsAsync(
        string userId, 
        TransactionFilter? filter = null, 
        CancellationToken cancellationToken = default);

    Task<Result<TransactionDto>> GetTransactionByIdAsync(
        string userId, 
        Guid id, 
        CancellationToken cancellationToken = default);

    Task<Result<TransactionDto>> CreateTransactionAsync(
        string userId, 
        CreateTransactionRequest request, 
        CancellationToken cancellationToken = default);

    Task<Result<TransactionDto>> UpdateTransactionAsync(
        string userId, 
        Guid id, 
        UpdateTransactionRequest request, 
        CancellationToken cancellationToken = default);

    Task<Result> DeleteTransactionAsync(
        string userId, 
        Guid id, 
        CancellationToken cancellationToken = default);

    Task<Result<int>> DeleteTransactionsBulkAsync(
        string userId, 
        IReadOnlyList<Guid> transactionIds, 
        CancellationToken cancellationToken = default);
}

public class TransactionService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ILogger<TransactionService> logger) : ITransactionService
{
    public async Task<Result<IReadOnlyList<TransactionDto>>> GetTransactionsAsync(
        string userId, 
        TransactionFilter? filter = null, 
        CancellationToken ct = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ct);

        var query = context.Transactions
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Subcategory)
            .Where(t => t.UserId == userId);

        if (filter is not null)
        {
            if (filter.StartDate.HasValue)
                query = query.Where(t => t.Date >= filter.StartDate.Value);
            if (filter.EndDate.HasValue)
                query = query.Where(t => t.Date <= filter.EndDate.Value);
            if (filter.Type.HasValue)
                query = query.Where(t => t.Type == filter.Type.Value);
            if (filter.CategoryIds is { Count: > 0 })
                query = query.Where(t => (t.CategoryId.HasValue && filter.CategoryIds.Contains(t.CategoryId.Value)) ||
                                         (t.SubcategoryId.HasValue && filter.CategoryIds.Contains(t.SubcategoryId.Value)));
            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim().ToLower();
                query = query.Where(t => t.Description.ToLower().Contains(term));
            }
        }

        var results = await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAtUtc)
            .ToListAsync(ct);

        return Result<IReadOnlyList<TransactionDto>>.Success(results.Select(MapToDto).ToList());
    }

    public async Task<Result<int>> DeleteTransactionsBulkAsync(
        string userId, 
        IReadOnlyList<Guid> transactionIds, 
        CancellationToken ct = default)
    {
        if (transactionIds.Count == 0)
            return Result<int>.Success(0);

        await using var context = await dbContextFactory.CreateDbContextAsync(ct);

        // Delete all matching records owned by this user
        var affected = await context.Transactions
            .Where(t => t.UserId == userId && transactionIds.Contains(t.Id))
            .ExecuteDeleteAsync(ct);

        return Result<int>.Success(affected);
    }
}
```

#### Definition of Done

- [x] All methods in `ITransactionService` are implemented and adhere to Result pattern.
- [x] Category and subcategory validation prevents mismatched or cross-user category assignments.
- [x] Bulk deletion executes safely without N+1 query loop.
- [x] Unit and integration tests in `TransactionServiceTests.cs` pass.
- [x] Code compiles without errors.

---

### Step 7: Service Registration & Verification

**Status**: ✅ Done

#### Objectives

- Register `ICategoryService` and `ITransactionService` in `Expenses/Program.cs` as scoped services.
- Verify full solution compilation (`dotnet build Expenses.sln`).
- Verify complete test suite execution (`dotnet test Expenses.sln`).

#### Touchpoints (Files to Create/Modify)

- `Expenses/Program.cs` (modify)

#### Pseudo-code / Technical Notes

```csharp
// In Expenses/Program.cs:
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
```

#### Definition of Done

- [x] Dependency injection registrations completed.
- [x] Solution compiles with zero warnings or errors (`dotnet build Expenses.sln`).
- [x] All unit and integration tests pass cleanly (`dotnet test Expenses.sln`).

---

## 4. Progress Tracking (State Machine)

| Step | Status     | Task Description                                                  | Target File(s)                                                                                                                                      |
| ---- | ---------- | ----------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1    | [✅ Done]  | Create `Expenses.Tests` project and SQLite in-memory test fixture | `Expenses.Tests/Expenses.Tests.csproj`, `Expenses.sln`, `Expenses.Tests/Fixtures/SqliteTestDbFixture.cs`                                             |
| 2    | [✅ Done]  | Implement `Result<T>` and `Result` pattern records                | `Expenses/Common/Result.cs`                                                                                                                         |
| 3    | [✅ Done]  | Define domain enums, entities, and immutable DTOs                 | `Expenses/Models/Enums/TransactionType.cs`, `Expenses/Models/Entities/Category.cs`, `Expenses/Models/Entities/Transaction.cs`, `Expenses/Models/DTOs/CategoryDtos.cs`, `Expenses/Models/DTOs/TransactionDtos.cs` |
| 4    | [✅ Done]  | Configure EF Core mappings, DbContextFactory, and migration       | `Expenses/Data/Configurations/CategoryConfiguration.cs`, `Expenses/Data/Configurations/TransactionConfiguration.cs`, `Expenses/Data/ApplicationDbContext.cs`, `Expenses/Program.cs`, `Expenses/Data/Migrations/` |
| 5    | [✅ Done]  | Implement `ICategoryService` with two-tier validation and tests   | `Expenses/Services/Categories/ICategoryService.cs`, `Expenses/Services/Categories/CategoryService.cs`, `Expenses.Tests/Services/CategoryServiceTests.cs` |
| 6    | [✅ Done]  | Implement `ITransactionService` with filters, bulk ops, and tests | `Expenses/Services/Transactions/ITransactionService.cs`, `Expenses/Services/Transactions/TransactionService.cs`, `Expenses.Tests/Services/TransactionServiceTests.cs` |
| 7    | [✅ Done]  | Register services in DI and verify end-to-end tests               | `Expenses/Program.cs`                                                                                                                               |

**Status Legend:**

- `[⏳ Pending]` - Not started.
- `[🔄 In Progress]` - Software_Engineer is currently executing.
- `[✅ Done]` - Compiled, tests passed, and code reviewed.
- `[⏸️ Blocked]` - Halted due to technical blockers.
