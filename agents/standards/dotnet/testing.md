# Testing Standards & Verification Guidelines

## 1. Test Architecture & Stack

When tests are added to the solution, adhere to the following stack:
- **Unit Testing Framework**: xUnit (`net10.0`)
- **Assertions**: `FluentAssertions`
- **Mocking**: `NSubstitute` or `Moq`
- **Blazor Component Testing**: `bUnit`
- **Database Testing**: SQLite In-Memory (`Microsoft.Data.Sqlite` with `DataSource=:memory:`)

Test projects should follow the naming pattern: `Expenses.Tests` or `Expenses.UnitTests` placed in the `Expenses/` solution folder.

---

## 2. AAA Pattern (Arrange, Act, Assert)

All unit and integration tests must strictly follow the Arrange-Act-Assert pattern with clear visual separation:

```csharp
[Fact]
public async Task DeduplicationService_WhenExactMatchExists_ReturnsDuplicateCandidate()
{
    // Arrange
    var existingExpense = new Expense
    {
        Id = Guid.NewGuid(),
        UserId = "test-user",
        Amount = 45.50m,
        TransactionDate = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        Vendor = "Supermarket"
    };

    var candidate = new ParsedTransactionDto(
        TransactionDate: new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc),
        Amount: 45.50m,
        Currency: "EUR",
        RawDescription: "Supermarket Purchase",
        MerchantName: "Supermarket"
    );

    // Act
    var result = await _sut.EvaluateAsync("test-user", [candidate], CancellationToken.None);

    // Assert
    result.Should().NotBeNull();
    result.Duplicates.Should().HaveCount(1);
    result.Duplicates.First().Confidence.Should().Be(MatchConfidence.Exact);
}
```

---

## 3. SQLite In-Memory Database Pattern

When testing EF Core queries or migrations, use an open SQLite in-memory connection rather than the EF Core in-memory provider (to ensure SQLite SQL dialect accuracy):

```csharp
public static ApplicationDbContext CreateInMemoryDbContext()
{
    var connection = new SqliteConnection("DataSource=:memory:");
    connection.Open();

    var options = new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseSqlite(connection)
        .Options;

    var context = new ApplicationDbContext(options);
    context.Database.EnsureCreated();
    return context;
}
```

---

## 4. Key Ingestion Test Scenarios to Verify
- **CSV Parser**: Delimiters (comma vs semicolon), currency symbols (`€`, `$`), decimal separators (`.` vs `,`), missing optional columns, invalid rows.
- **Bank JSON Parser**: Missing nested fields, null arrays, duplicate transaction IDs, ISO timestamp variations.
- **Deduplication Engine**:
  - Exact match (same date, amount, vendor).
  - Date window matching (same vendor, same amount, +/- 2 days).
  - Fuzzy vendor matching (e.g. `AMZN Mktp US*123` vs `Amazon`).
- **Category Engine**: Rule prioritization, wildcard regex matches, fallback to default parent category.
