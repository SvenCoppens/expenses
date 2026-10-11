using Expenses.Common;
using Expenses.Models.DTOs;
using Expenses.Models.Entities;
using Expenses.Models.Enums;
using Expenses.Services.Transactions;
using Expenses.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Expenses.Tests.Services;

public sealed class TransactionServiceTests : IClassFixture<SqliteTestDbFixture>
{
    private readonly SqliteTestDbFixture _fixture;
    private readonly ILogger<TransactionService> _logger = NullLogger<TransactionService>.Instance;

    public TransactionServiceTests(SqliteTestDbFixture fixture)
    {
        _fixture = fixture;
    }

    private ITransactionService CreateSut() => new TransactionService(_fixture.Factory, _logger);

    private static string CreateUserId() => $"user_{Guid.NewGuid():N}";

    private async Task<Category> SeedCategoryAsync(
        string userId,
        string name,
        Guid? parentCategoryId = null)
    {
        await using var context = await _fixture.CreateContextAsync();
        var category = new Category
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            ParentCategoryId = parentCategoryId
        };
        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category;
    }

    private async Task<Transaction> SeedTransactionAsync(
        string userId,
        decimal amount = 50m,
        string description = "Test Transaction",
        DateOnly? date = null,
        TransactionType type = TransactionType.Expense,
        Guid? categoryId = null,
        Guid? subcategoryId = null,
        string? notes = null,
        DateTime? createdAtUtc = null)
    {
        await using var context = await _fixture.CreateContextAsync();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = amount,
            Description = description,
            Date = date ?? DateOnly.FromDateTime(DateTime.UtcNow),
            Type = type,
            CategoryId = categoryId,
            SubcategoryId = subcategoryId,
            Notes = notes,
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow
        };
        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();
        return transaction;
    }

    #region 1. Transaction Creation Tests

    [Fact]
    public async Task CreateTransactionAsync_WithValidRequestWithoutCategory_CreatesAndReturnsTransaction()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var transactionDate = new DateOnly(2026, 10, 11);
        var request = new CreateTransactionRequest(
            Date: transactionDate,
            Amount: 42.75m,
            Description: "Pharmacy Supplies",
            Type: TransactionType.Expense,
            CategoryId: null,
            SubcategoryId: null,
            Notes: "Cold medicine"
        );

        // Act
        var result = await sut.CreateTransactionAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().NotBeEmpty();
        result.Value.Date.Should().Be(transactionDate);
        result.Value.Amount.Should().Be(42.75m);
        result.Value.Description.Should().Be("Pharmacy Supplies");
        result.Value.Type.Should().Be(TransactionType.Expense);
        result.Value.CategoryId.Should().BeNull();
        result.Value.CategoryName.Should().BeNull();
        result.Value.SubcategoryId.Should().BeNull();
        result.Value.SubcategoryName.Should().BeNull();
        result.Value.Notes.Should().Be("Cold medicine");
        result.Value.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        await using var context = await _fixture.CreateContextAsync();
        var entity = await context.Transactions.FirstOrDefaultAsync(t => t.Id == result.Value.Id);
        entity.Should().NotBeNull();
        entity!.UserId.Should().Be(userId);
        entity.Amount.Should().Be(42.75m);
        entity.Description.Should().Be("Pharmacy Supplies");
        entity.Type.Should().Be(TransactionType.Expense);
        entity.Date.Should().Be(transactionDate);
        entity.CategoryId.Should().BeNull();
        entity.SubcategoryId.Should().BeNull();
        entity.Notes.Should().Be("Cold medicine");
    }

    [Fact]
    public async Task CreateTransactionAsync_WithValidParentCategory_CreatesTransactionWithCategoryNamePopulated()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var category = await SeedCategoryAsync(userId, "Groceries");
        var request = new CreateTransactionRequest(
            Date: new DateOnly(2026, 10, 11),
            Amount: 115.20m,
            Description: "Weekly supermarket run",
            Type: TransactionType.Expense,
            CategoryId: category.Id,
            SubcategoryId: null
        );

        // Act
        var result = await sut.CreateTransactionAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.CategoryId.Should().Be(category.Id);
        result.Value.CategoryName.Should().Be("Groceries");
        result.Value.SubcategoryId.Should().BeNull();
        result.Value.SubcategoryName.Should().BeNull();

        await using var context = await _fixture.CreateContextAsync();
        var entity = await context.Transactions.FirstOrDefaultAsync(t => t.Id == result.Value.Id);
        entity.Should().NotBeNull();
        entity!.CategoryId.Should().Be(category.Id);
        entity.SubcategoryId.Should().BeNull();
    }

    [Fact]
    public async Task CreateTransactionAsync_WithValidParentAndSubcategory_CreatesTransactionWithBothCategoryNamesPopulated()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var parentCategory = await SeedCategoryAsync(userId, "Food & Dining");
        var subcategory = await SeedCategoryAsync(userId, "Restaurants", parentCategory.Id);

        var request = new CreateTransactionRequest(
            Date: new DateOnly(2026, 10, 11),
            Amount: 78.50m,
            Description: "Dinner at Bistro",
            Type: TransactionType.Expense,
            CategoryId: parentCategory.Id,
            SubcategoryId: subcategory.Id
        );

        // Act
        var result = await sut.CreateTransactionAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.CategoryId.Should().Be(parentCategory.Id);
        result.Value.CategoryName.Should().Be("Food & Dining");
        result.Value.SubcategoryId.Should().Be(subcategory.Id);
        result.Value.SubcategoryName.Should().Be("Restaurants");

        await using var context = await _fixture.CreateContextAsync();
        var entity = await context.Transactions.FirstOrDefaultAsync(t => t.Id == result.Value.Id);
        entity.Should().NotBeNull();
        entity!.CategoryId.Should().Be(parentCategory.Id);
        entity.SubcategoryId.Should().Be(subcategory.Id);
    }

    [Fact]
    public async Task CreateTransactionAsync_WithIncomeType_CreatesTransactionCorrectly()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var category = await SeedCategoryAsync(userId, "Salary");
        var request = new CreateTransactionRequest(
            Date: new DateOnly(2026, 10, 1),
            Amount: 3500.00m,
            Description: "Monthly Salary",
            Type: TransactionType.Income,
            CategoryId: category.Id
        );

        // Act
        var result = await sut.CreateTransactionAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value!.Type.Should().Be(TransactionType.Income);
        result.Value.Amount.Should().Be(3500.00m);
    }

    [Fact]
    public async Task CreateTransactionAsync_WithTransferType_CreatesTransactionCorrectly()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var request = new CreateTransactionRequest(
            Date: new DateOnly(2026, 10, 5),
            Amount: 500.00m,
            Description: "Transfer to Savings",
            Type: TransactionType.Transfer
        );

        // Act
        var result = await sut.CreateTransactionAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value!.Type.Should().Be(TransactionType.Transfer);
        result.Value.Amount.Should().Be(500.00m);
    }

    [Fact]
    public async Task CreateTransactionAsync_WithWhitespaceDescription_TrimsDescription()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var request = new CreateTransactionRequest(
            Date: new DateOnly(2026, 10, 11),
            Amount: 25.00m,
            Description: "   Coffee & Pastry   ",
            Type: TransactionType.Expense
        );

        // Act
        var result = await sut.CreateTransactionAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value!.Description.Should().Be("Coffee & Pastry");

        await using var context = await _fixture.CreateContextAsync();
        var entity = await context.Transactions.FirstOrDefaultAsync(t => t.Id == result.Value.Id);
        entity.Should().NotBeNull();
        entity!.Description.Should().Be("Coffee & Pastry");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateTransactionAsync_WithEmptyOrWhitespaceDescription_ReturnsValidationFailure(string invalidDescription)
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var request = new CreateTransactionRequest(
            Date: new DateOnly(2026, 10, 11),
            Amount: 25.00m,
            Description: invalidDescription,
            Type: TransactionType.Expense
        );

        // Act
        var result = await sut.CreateTransactionAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();

        await using var context = await _fixture.CreateContextAsync();
        var count = await context.Transactions.CountAsync(t => t.UserId == userId);
        count.Should().Be(0);
    }

    #endregion

    #region 2. Category & Subcategory Validation Tests

    [Fact]
    public async Task CreateTransactionAsync_WhenCategoryBelongsToDifferentUser_ReturnsFailureAndDoesNotCreateTransaction()
    {
        // Arrange
        var sut = CreateSut();
        var userA = CreateUserId();
        var userB = CreateUserId();
        var userACategory = await SeedCategoryAsync(userA, "User A Groceries");

        var request = new CreateTransactionRequest(
            Date: new DateOnly(2026, 10, 11),
            Amount: 50.00m,
            Description: "Unauthorized Category Attempt",
            Type: TransactionType.Expense,
            CategoryId: userACategory.Id
        );

        // Act: User B attempts to use User A's category
        var result = await sut.CreateTransactionAsync(userB, request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();

        await using var context = await _fixture.CreateContextAsync();
        var exists = await context.Transactions.AnyAsync(t => t.UserId == userB);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTransactionAsync_WhenSubcategoryBelongsToDifferentUser_ReturnsFailureAndDoesNotCreateTransaction()
    {
        // Arrange
        var sut = CreateSut();
        var userA = CreateUserId();
        var userB = CreateUserId();

        var userAParent = await SeedCategoryAsync(userA, "User A Food");
        var userASubcategory = await SeedCategoryAsync(userA, "User A Restaurants", userAParent.Id);
        var userBParent = await SeedCategoryAsync(userB, "User B Food");

        var request = new CreateTransactionRequest(
            Date: new DateOnly(2026, 10, 11),
            Amount: 40.00m,
            Description: "Cross user subcategory",
            Type: TransactionType.Expense,
            CategoryId: userBParent.Id,
            SubcategoryId: userASubcategory.Id
        );

        // Act: User B uses their own parent but User A's subcategory
        var result = await sut.CreateTransactionAsync(userB, request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();

        await using var context = await _fixture.CreateContextAsync();
        var exists = await context.Transactions.AnyAsync(t => t.UserId == userB);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTransactionAsync_WhenSubcategorySpecifiedWithoutParentCategory_ReturnsFailure()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var parent = await SeedCategoryAsync(userId, "Utilities");
        var subcategory = await SeedCategoryAsync(userId, "Electricity", parent.Id);

        var request = new CreateTransactionRequest(
            Date: new DateOnly(2026, 10, 11),
            Amount: 90.00m,
            Description: "Electric bill missing parent category",
            Type: TransactionType.Expense,
            CategoryId: null,
            SubcategoryId: subcategory.Id
        );

        // Act
        var result = await sut.CreateTransactionAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();

        await using var context = await _fixture.CreateContextAsync();
        var exists = await context.Transactions.AnyAsync(t => t.UserId == userId);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTransactionAsync_WhenSubcategoryDoesNotBelongToSpecifiedParentCategory_ReturnsFailure()
    {
        // Arrange: Two distinct parent categories and a subcategory belonging to the second parent
        var sut = CreateSut();
        var userId = CreateUserId();
        var parentA = await SeedCategoryAsync(userId, "Parent A - Food");
        var parentB = await SeedCategoryAsync(userId, "Parent B - Utilities");
        var subcategoryB = await SeedCategoryAsync(userId, "Subcategory under Utilities", parentB.Id);

        // Attempt to create transaction with CategoryId = parentA, but SubcategoryId = subcategoryB (child of parentB)
        var request = new CreateTransactionRequest(
            Date: new DateOnly(2026, 10, 11),
            Amount: 30.00m,
            Description: "Mismatched hierarchy transaction",
            Type: TransactionType.Expense,
            CategoryId: parentA.Id,
            SubcategoryId: subcategoryB.Id
        );

        // Act
        var result = await sut.CreateTransactionAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();

        await using var context = await _fixture.CreateContextAsync();
        var exists = await context.Transactions.AnyAsync(t => t.UserId == userId);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTransactionAsync_WhenSubcategoryIsActuallyAParentCategory_ReturnsFailure()
    {
        // Arrange: User has two top-level parent categories
        var sut = CreateSut();
        var userId = CreateUserId();
        var parentA = await SeedCategoryAsync(userId, "Parent A");
        var parentB = await SeedCategoryAsync(userId, "Parent B");

        // Attempt to assign parentB as the SubcategoryId
        var request = new CreateTransactionRequest(
            Date: new DateOnly(2026, 10, 11),
            Amount: 50.00m,
            Description: "Parent assigned as subcategory",
            Type: TransactionType.Expense,
            CategoryId: parentA.Id,
            SubcategoryId: parentB.Id
        );

        // Act
        var result = await sut.CreateTransactionAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();

        await using var context = await _fixture.CreateContextAsync();
        var exists = await context.Transactions.AnyAsync(t => t.UserId == userId);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task CreateTransactionAsync_WhenCategoryDoesNotExist_ReturnsFailure()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var nonExistentCategoryId = Guid.NewGuid();

        var request = new CreateTransactionRequest(
            Date: new DateOnly(2026, 10, 11),
            Amount: 25.00m,
            Description: "Non-existent Category",
            Type: TransactionType.Expense,
            CategoryId: nonExistentCategoryId
        );

        // Act
        var result = await sut.CreateTransactionAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CreateTransactionAsync_WhenSubcategoryDoesNotExist_ReturnsFailure()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var parent = await SeedCategoryAsync(userId, "Groceries");
        var nonExistentSubcategoryId = Guid.NewGuid();

        var request = new CreateTransactionRequest(
            Date: new DateOnly(2026, 10, 11),
            Amount: 25.00m,
            Description: "Non-existent Subcategory",
            Type: TransactionType.Expense,
            CategoryId: parent.Id,
            SubcategoryId: nonExistentSubcategoryId
        );

        // Act
        var result = await sut.CreateTransactionAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UpdateTransactionAsync_WhenCategoryBelongsToDifferentUser_ReturnsFailureAndDoesNotMutate()
    {
        // Arrange
        var sut = CreateSut();
        var userA = CreateUserId();
        var userB = CreateUserId();

        var userATransaction = await SeedTransactionAsync(userA, amount: 50m, description: "Original Transaction");
        var userBCategory = await SeedCategoryAsync(userB, "User B Category");

        var updateRequest = new UpdateTransactionRequest(
            Date: userATransaction.Date,
            Amount: 50m,
            Description: "Updated with foreign category",
            Type: TransactionType.Expense,
            CategoryId: userBCategory.Id
        );

        // Act: User A tries to assign User B's category
        var result = await sut.UpdateTransactionAsync(userA, userATransaction.Id, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();

        await using var context = await _fixture.CreateContextAsync();
        var entity = await context.Transactions.FirstOrDefaultAsync(t => t.Id == userATransaction.Id);
        entity.Should().NotBeNull();
        entity!.CategoryId.Should().BeNull();
        entity.Description.Should().Be("Original Transaction");
    }

    [Fact]
    public async Task UpdateTransactionAsync_WhenSubcategoryBelongsToDifferentUser_ReturnsFailureAndDoesNotMutate()
    {
        // Arrange
        var sut = CreateSut();
        var userA = CreateUserId();
        var userB = CreateUserId();

        var userAParent = await SeedCategoryAsync(userA, "User A Parent");
        var userATransaction = await SeedTransactionAsync(userA, amount: 50m, categoryId: userAParent.Id);

        var userBParent = await SeedCategoryAsync(userB, "User B Parent");
        var userBSubcategory = await SeedCategoryAsync(userB, "User B Subcategory", userBParent.Id);

        var updateRequest = new UpdateTransactionRequest(
            Date: userATransaction.Date,
            Amount: 50m,
            Description: "Updated with foreign subcategory",
            Type: TransactionType.Expense,
            CategoryId: userAParent.Id,
            SubcategoryId: userBSubcategory.Id
        );

        // Act: User A tries to assign User B's subcategory
        var result = await sut.UpdateTransactionAsync(userA, userATransaction.Id, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();

        await using var context = await _fixture.CreateContextAsync();
        var entity = await context.Transactions.FirstOrDefaultAsync(t => t.Id == userATransaction.Id);
        entity.Should().NotBeNull();
        entity!.SubcategoryId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTransactionAsync_WhenSubcategoryDoesNotBelongToSpecifiedParentCategory_ReturnsFailure()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        var parentA = await SeedCategoryAsync(userId, "Parent A");
        var parentB = await SeedCategoryAsync(userId, "Parent B");
        var subcategoryB = await SeedCategoryAsync(userId, "Subcategory B", parentB.Id);

        var transaction = await SeedTransactionAsync(userId, amount: 40m, description: "Test Transaction");

        var updateRequest = new UpdateTransactionRequest(
            Date: transaction.Date,
            Amount: 40m,
            Description: "Updated Mismatched Subcategory",
            Type: TransactionType.Expense,
            CategoryId: parentA.Id,
            SubcategoryId: subcategoryB.Id
        );

        // Act
        var result = await sut.UpdateTransactionAsync(userId, transaction.Id, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();

        await using var context = await _fixture.CreateContextAsync();
        var entity = await context.Transactions.FirstOrDefaultAsync(t => t.Id == transaction.Id);
        entity.Should().NotBeNull();
        entity!.CategoryId.Should().BeNull();
        entity.SubcategoryId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTransactionAsync_WhenSubcategorySpecifiedWithoutParentCategory_ReturnsFailure()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        var parent = await SeedCategoryAsync(userId, "Parent");
        var subcategory = await SeedCategoryAsync(userId, "Subcategory", parent.Id);
        var transaction = await SeedTransactionAsync(userId, amount: 60m);

        var updateRequest = new UpdateTransactionRequest(
            Date: transaction.Date,
            Amount: 60m,
            Description: "Missing Parent Category",
            Type: TransactionType.Expense,
            CategoryId: null,
            SubcategoryId: subcategory.Id
        );

        // Act
        var result = await sut.UpdateTransactionAsync(userId, transaction.Id, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();

        await using var context = await _fixture.CreateContextAsync();
        var entity = await context.Transactions.FirstOrDefaultAsync(t => t.Id == transaction.Id);
        entity.Should().NotBeNull();
        entity!.SubcategoryId.Should().BeNull();
    }

    #endregion

    #region 3. Strict Per-User Isolation Tests

    [Fact]
    public async Task GetTransactionsAsync_StrictlyIsolatesDataToSpecifiedUser()
    {
        // Arrange
        var sut = CreateSut();
        var userA = CreateUserId();
        var userB = CreateUserId();

        await SeedTransactionAsync(userA, amount: 10m, description: "User A Tx 1");
        await SeedTransactionAsync(userA, amount: 20m, description: "User A Tx 2");
        await SeedTransactionAsync(userB, amount: 99m, description: "User B Secret Tx");

        // Act
        var resultA = await sut.GetTransactionsAsync(userA);
        var resultB = await sut.GetTransactionsAsync(userB);

        // Assert
        resultA.Should().NotBeNull();
        resultA.IsSuccess.Should().BeTrue();
        resultA.Value.Should().HaveCount(2);
        resultA.Value!.Select(t => t.Description).Should().Contain(["User A Tx 1", "User A Tx 2"]);
        resultA.Value.Select(t => t.Description).Should().NotContain("User B Secret Tx");

        resultB.Should().NotBeNull();
        resultB.IsSuccess.Should().BeTrue();
        resultB.Value.Should().HaveCount(1);
        resultB.Value![0].Description.Should().Be("User B Secret Tx");
    }

    [Fact]
    public async Task GetTransactionByIdAsync_WhenTransactionBelongsToDifferentUser_ReturnsNotFoundFailure()
    {
        // Arrange
        var sut = CreateSut();
        var userA = CreateUserId();
        var userB = CreateUserId();

        var userATransaction = await SeedTransactionAsync(userA, amount: 100m, description: "User A Private Transaction");

        // Act: User B attempts to access User A's transaction
        var result = await sut.GetTransactionByIdAsync(userB, userATransaction.Id);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UpdateTransactionAsync_WhenTransactionBelongsToDifferentUser_ReturnsNotFoundFailureAndDoesNotMutate()
    {
        // Arrange
        var sut = CreateSut();
        var userA = CreateUserId();
        var userB = CreateUserId();

        var userATransaction = await SeedTransactionAsync(userA, amount: 75m, description: "User A Original");

        var maliciousRequest = new UpdateTransactionRequest(
            Date: new DateOnly(2026, 10, 11),
            Amount: 999.99m,
            Description: "Hacked by User B",
            Type: TransactionType.Expense
        );

        // Act: User B attempts to update User A's transaction
        var result = await sut.UpdateTransactionAsync(userB, userATransaction.Id, maliciousRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();

        await using var context = await _fixture.CreateContextAsync();
        var entity = await context.Transactions.FirstOrDefaultAsync(t => t.Id == userATransaction.Id);
        entity.Should().NotBeNull();
        entity!.Amount.Should().Be(75m);
        entity.Description.Should().Be("User A Original");
        entity.UserId.Should().Be(userA);
    }

    [Fact]
    public async Task DeleteTransactionAsync_WhenTransactionBelongsToDifferentUser_ReturnsNotFoundFailureAndDoesNotDelete()
    {
        // Arrange
        var sut = CreateSut();
        var userA = CreateUserId();
        var userB = CreateUserId();

        var userATransaction = await SeedTransactionAsync(userA, amount: 50m, description: "User A Protected");

        // Act: User B attempts to delete User A's transaction
        var result = await sut.DeleteTransactionAsync(userB, userATransaction.Id);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();

        await using var context = await _fixture.CreateContextAsync();
        var exists = await context.Transactions.AnyAsync(t => t.Id == userATransaction.Id);
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteTransactionsBulkAsync_WhenTargetingAnotherUsersTransactions_DoesNotDeleteOtherUsersTransactions()
    {
        // Arrange
        var sut = CreateSut();
        var userA = CreateUserId();
        var userB = CreateUserId();

        var userATx = await SeedTransactionAsync(userA, amount: 20m, description: "User A Tx");
        var userBTx = await SeedTransactionAsync(userB, amount: 30m, description: "User B Tx");

        // Act: User A requests bulk delete for User B's transaction ID
        var result = await sut.DeleteTransactionsBulkAsync(userA, [userBTx.Id]);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0); // 0 records deleted for User A

        await using var context = await _fixture.CreateContextAsync();
        var userBExists = await context.Transactions.AnyAsync(t => t.Id == userBTx.Id);
        userBExists.Should().BeTrue();
        var userAExists = await context.Transactions.AnyAsync(t => t.Id == userATx.Id);
        userAExists.Should().BeTrue();
    }

    #endregion

    #region 4. Retrieval & Filtering Tests

    [Fact]
    public async Task GetTransactionsAsync_WhenUserHasNoTransactions_ReturnsEmptyList()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        // Act
        var result = await sut.GetTransactionsAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTransactionsAsync_WhenNoFilterProvided_ReturnsAllTransactionsOrderedByDateDescThenCreatedAtDesc()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        var date1 = new DateOnly(2026, 10, 1);
        var date2 = new DateOnly(2026, 10, 5);
        var date3 = new DateOnly(2026, 10, 5);

        var timeEarly = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var timeLate = new DateTime(2026, 10, 5, 14, 0, 0, DateTimeKind.Utc);

        var txOld = await SeedTransactionAsync(userId, date: date1, description: "Old Date");
        var txMiddle = await SeedTransactionAsync(userId, date: date2, description: "Same Date Earlier", createdAtUtc: timeEarly);
        var txNewest = await SeedTransactionAsync(userId, date: date3, description: "Same Date Later", createdAtUtc: timeLate);

        // Act
        var result = await sut.GetTransactionsAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);

        var descriptions = result.Value!.Select(t => t.Description).ToList();
        descriptions[0].Should().Be("Same Date Later");
        descriptions[1].Should().Be("Same Date Earlier");
        descriptions[2].Should().Be("Old Date");
    }

    [Fact]
    public async Task GetTransactionsAsync_WithStartDateFilter_ReturnsTransactionsOnOrAfterDate()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        await SeedTransactionAsync(userId, date: new DateOnly(2026, 10, 1), description: "Before Start");
        await SeedTransactionAsync(userId, date: new DateOnly(2026, 10, 5), description: "On Start Date");
        await SeedTransactionAsync(userId, date: new DateOnly(2026, 10, 10), description: "After Start Date");

        var filter = new TransactionFilter(StartDate: new DateOnly(2026, 10, 5));

        // Act
        var result = await sut.GetTransactionsAsync(userId, filter);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.Select(t => t.Description).Should().Contain(["On Start Date", "After Start Date"]);
        result.Value.Select(t => t.Description).Should().NotContain("Before Start");
    }

    [Fact]
    public async Task GetTransactionsAsync_WithEndDateFilter_ReturnsTransactionsOnOrBeforeDate()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        await SeedTransactionAsync(userId, date: new DateOnly(2026, 10, 1), description: "Before End Date");
        await SeedTransactionAsync(userId, date: new DateOnly(2026, 10, 5), description: "On End Date");
        await SeedTransactionAsync(userId, date: new DateOnly(2026, 10, 10), description: "After End Date");

        var filter = new TransactionFilter(EndDate: new DateOnly(2026, 10, 5));

        // Act
        var result = await sut.GetTransactionsAsync(userId, filter);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.Select(t => t.Description).Should().Contain(["Before End Date", "On End Date"]);
        result.Value.Select(t => t.Description).Should().NotContain("After End Date");
    }

    [Fact]
    public async Task GetTransactionsAsync_WithDateRangeFilter_ReturnsTransactionsWithinWindow()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        await SeedTransactionAsync(userId, date: new DateOnly(2026, 9, 30), description: "September");
        await SeedTransactionAsync(userId, date: new DateOnly(2026, 10, 5), description: "October Window");
        await SeedTransactionAsync(userId, date: new DateOnly(2026, 10, 15), description: "Later October");

        var filter = new TransactionFilter(
            StartDate: new DateOnly(2026, 10, 1),
            EndDate: new DateOnly(2026, 10, 10)
        );

        // Act
        var result = await sut.GetTransactionsAsync(userId, filter);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Description.Should().Be("October Window");
    }

    [Fact]
    public async Task GetTransactionsAsync_WithTransactionTypeFilter_ReturnsOnlyMatchingType()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        await SeedTransactionAsync(userId, amount: 20m, description: "Grocery Store", type: TransactionType.Expense);
        await SeedTransactionAsync(userId, amount: 2000m, description: "Salary Deposit", type: TransactionType.Income);
        await SeedTransactionAsync(userId, amount: 100m, description: "Checking to Savings", type: TransactionType.Transfer);

        var filter = new TransactionFilter(Type: TransactionType.Expense);

        // Act
        var result = await sut.GetTransactionsAsync(userId, filter);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Description.Should().Be("Grocery Store");
        result.Value[0].Type.Should().Be(TransactionType.Expense);
    }

    [Fact]
    public async Task GetTransactionsAsync_WithCategoryIdsFilter_MatchesDirectCategory()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        var catA = await SeedCategoryAsync(userId, "Category A");
        var catB = await SeedCategoryAsync(userId, "Category B");

        await SeedTransactionAsync(userId, description: "Tx Cat A", categoryId: catA.Id);
        await SeedTransactionAsync(userId, description: "Tx Cat B", categoryId: catB.Id);
        await SeedTransactionAsync(userId, description: "Tx Uncategorized");

        var filter = new TransactionFilter(CategoryIds: [catA.Id]);

        // Act
        var result = await sut.GetTransactionsAsync(userId, filter);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Description.Should().Be("Tx Cat A");
    }

    [Fact]
    public async Task GetTransactionsAsync_WithCategoryIdsFilter_MatchesSubcategory()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        var parent = await SeedCategoryAsync(userId, "Food");
        var subcategory = await SeedCategoryAsync(userId, "Fast Food", parent.Id);

        await SeedTransactionAsync(userId, description: "Tx Parent Only", categoryId: parent.Id);
        await SeedTransactionAsync(userId, description: "Tx Subcategory", categoryId: parent.Id, subcategoryId: subcategory.Id);

        var filter = new TransactionFilter(CategoryIds: [subcategory.Id]);

        // Act
        var result = await sut.GetTransactionsAsync(userId, filter);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Description.Should().Be("Tx Subcategory");
    }

    [Fact]
    public async Task GetTransactionsAsync_WithSearchTermFilter_PerformsCaseInsensitiveMatchOnDescription()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        await SeedTransactionAsync(userId, description: "Starbucks Coffee Berlin");
        await SeedTransactionAsync(userId, description: "COFFEE beans roastery");
        await SeedTransactionAsync(userId, description: "Supermarket groceries");

        var filter = new TransactionFilter(SearchTerm: "coffee");

        // Act
        var result = await sut.GetTransactionsAsync(userId, filter);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value!.Select(t => t.Description).Should().Contain(["Starbucks Coffee Berlin", "COFFEE beans roastery"]);
        result.Value.Select(t => t.Description).Should().NotContain("Supermarket groceries");
    }

    [Fact]
    public async Task GetTransactionsAsync_WithCombinedFilters_AppliesAllCriteriaSimultaneously()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        var foodCategory = await SeedCategoryAsync(userId, "Food");
        var leisureCategory = await SeedCategoryAsync(userId, "Leisure");

        // Match: Date in window, Type=Expense, Category=Food, SearchTerm="bistro"
        await SeedTransactionAsync(
            userId,
            date: new DateOnly(2026, 10, 8),
            type: TransactionType.Expense,
            categoryId: foodCategory.Id,
            description: "French Bistro dinner"
        );

        // Mismatches:
        // Different date
        await SeedTransactionAsync(
            userId,
            date: new DateOnly(2026, 9, 20),
            type: TransactionType.Expense,
            categoryId: foodCategory.Id,
            description: "Italian Bistro lunch"
        );
        // Different type (Income)
        await SeedTransactionAsync(
            userId,
            date: new DateOnly(2026, 10, 8),
            type: TransactionType.Income,
            categoryId: foodCategory.Id,
            description: "Bistro tips refund"
        );
        // Different category
        await SeedTransactionAsync(
            userId,
            date: new DateOnly(2026, 10, 8),
            type: TransactionType.Expense,
            categoryId: leisureCategory.Id,
            description: "Bistro event ticket"
        );
        // Missing search term
        await SeedTransactionAsync(
            userId,
            date: new DateOnly(2026, 10, 8),
            type: TransactionType.Expense,
            categoryId: foodCategory.Id,
            description: "Supermarket run"
        );

        var filter = new TransactionFilter(
            StartDate: new DateOnly(2026, 10, 1),
            EndDate: new DateOnly(2026, 10, 10),
            Type: TransactionType.Expense,
            CategoryIds: [foodCategory.Id],
            SearchTerm: "bistro"
        );

        // Act
        var result = await sut.GetTransactionsAsync(userId, filter);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        result.Value![0].Description.Should().Be("French Bistro dinner");
    }

    [Fact]
    public async Task GetTransactionsAsync_PopulatesCategoryAndSubcategoryNamesInDtos()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        var parent = await SeedCategoryAsync(userId, "Automotive");
        var subcategory = await SeedCategoryAsync(userId, "Fuel", parent.Id);

        await SeedTransactionAsync(
            userId,
            amount: 70m,
            description: "Gas station",
            categoryId: parent.Id,
            subcategoryId: subcategory.Id
        );

        // Act
        var result = await sut.GetTransactionsAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);

        var dto = result.Value![0];
        dto.CategoryId.Should().Be(parent.Id);
        dto.CategoryName.Should().Be("Automotive");
        dto.SubcategoryId.Should().Be(subcategory.Id);
        dto.SubcategoryName.Should().Be("Fuel");
    }

    [Fact]
    public async Task GetTransactionByIdAsync_WhenTransactionExists_ReturnsTransactionDtoWithCategoryNames()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        var parent = await SeedCategoryAsync(userId, "Health & Fitness");
        var subcategory = await SeedCategoryAsync(userId, "Gym Membership", parent.Id);

        var transaction = await SeedTransactionAsync(
            userId,
            amount: 45m,
            description: "Monthly gym dues",
            categoryId: parent.Id,
            subcategoryId: subcategory.Id,
            notes: "Direct debit"
        );

        // Act
        var result = await sut.GetTransactionByIdAsync(userId, transaction.Id);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(transaction.Id);
        result.Value.Amount.Should().Be(45m);
        result.Value.Description.Should().Be("Monthly gym dues");
        result.Value.CategoryId.Should().Be(parent.Id);
        result.Value.CategoryName.Should().Be("Health & Fitness");
        result.Value.SubcategoryId.Should().Be(subcategory.Id);
        result.Value.SubcategoryName.Should().Be("Gym Membership");
        result.Value.Notes.Should().Be("Direct debit");
    }

    [Fact]
    public async Task GetTransactionByIdAsync_WhenTransactionDoesNotExist_ReturnsNotFoundFailure()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await sut.GetTransactionByIdAsync(userId, nonExistentId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region 5. Update Transaction Tests

    [Fact]
    public async Task UpdateTransactionAsync_WithValidRequest_UpdatesAllFieldsSuccessfully()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        var initialCategory = await SeedCategoryAsync(userId, "General");
        var transaction = await SeedTransactionAsync(
            userId,
            amount: 50m,
            description: "Initial description",
            type: TransactionType.Expense,
            categoryId: initialCategory.Id,
            notes: "Old notes"
        );

        var newParent = await SeedCategoryAsync(userId, "Home");
        var newSubcategory = await SeedCategoryAsync(userId, "Furniture", newParent.Id);
        var newDate = new DateOnly(2026, 10, 15);

        var updateRequest = new UpdateTransactionRequest(
            Date: newDate,
            Amount: 189.99m,
            Description: "Desk Chair",
            Type: TransactionType.Expense,
            CategoryId: newParent.Id,
            SubcategoryId: newSubcategory.Id,
            Notes: "Ergonomic chair from store"
        );

        // Act
        var result = await sut.UpdateTransactionAsync(userId, transaction.Id, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(transaction.Id);
        result.Value.Date.Should().Be(newDate);
        result.Value.Amount.Should().Be(189.99m);
        result.Value.Description.Should().Be("Desk Chair");
        result.Value.Type.Should().Be(TransactionType.Expense);
        result.Value.CategoryId.Should().Be(newParent.Id);
        result.Value.CategoryName.Should().Be("Home");
        result.Value.SubcategoryId.Should().Be(newSubcategory.Id);
        result.Value.SubcategoryName.Should().Be("Furniture");
        result.Value.Notes.Should().Be("Ergonomic chair from store");

        await using var context = await _fixture.CreateContextAsync();
        var entity = await context.Transactions.FirstOrDefaultAsync(t => t.Id == transaction.Id);
        entity.Should().NotBeNull();
        entity!.Amount.Should().Be(189.99m);
        entity.Description.Should().Be("Desk Chair");
        entity.CategoryId.Should().Be(newParent.Id);
        entity.SubcategoryId.Should().Be(newSubcategory.Id);
        entity.Notes.Should().Be("Ergonomic chair from store");
        entity.Date.Should().Be(newDate);
    }

    [Fact]
    public async Task UpdateTransactionAsync_WhenClearingCategories_UpdatesToNullCategoriesSuccessfully()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        var parent = await SeedCategoryAsync(userId, "Shopping");
        var subcategory = await SeedCategoryAsync(userId, "Electronics", parent.Id);

        var transaction = await SeedTransactionAsync(
            userId,
            amount: 99.00m,
            description: "Headphones",
            categoryId: parent.Id,
            subcategoryId: subcategory.Id
        );

        var updateRequest = new UpdateTransactionRequest(
            Date: transaction.Date,
            Amount: 99.00m,
            Description: "Headphones",
            Type: TransactionType.Expense,
            CategoryId: null,
            SubcategoryId: null,
            Notes: null
        );

        // Act
        var result = await sut.UpdateTransactionAsync(userId, transaction.Id, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value!.CategoryId.Should().BeNull();
        result.Value.CategoryName.Should().BeNull();
        result.Value.SubcategoryId.Should().BeNull();
        result.Value.SubcategoryName.Should().BeNull();

        await using var context = await _fixture.CreateContextAsync();
        var entity = await context.Transactions.FirstOrDefaultAsync(t => t.Id == transaction.Id);
        entity.Should().NotBeNull();
        entity!.CategoryId.Should().BeNull();
        entity.SubcategoryId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateTransactionAsync_WhenTransactionDoesNotExist_ReturnsNotFoundFailure()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var nonExistentId = Guid.NewGuid();

        var request = new UpdateTransactionRequest(
            Date: new DateOnly(2026, 10, 11),
            Amount: 50m,
            Description: "Update Non Existent",
            Type: TransactionType.Expense
        );

        // Act
        var result = await sut.UpdateTransactionAsync(userId, nonExistentId, request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateTransactionAsync_WithEmptyDescription_ReturnsValidationFailure(string invalidDescription)
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var transaction = await SeedTransactionAsync(userId, amount: 20m, description: "Valid Description");

        var updateRequest = new UpdateTransactionRequest(
            Date: transaction.Date,
            Amount: 20m,
            Description: invalidDescription,
            Type: TransactionType.Expense
        );

        // Act
        var result = await sut.UpdateTransactionAsync(userId, transaction.Id, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();

        await using var context = await _fixture.CreateContextAsync();
        var entity = await context.Transactions.FirstOrDefaultAsync(t => t.Id == transaction.Id);
        entity.Should().NotBeNull();
        entity!.Description.Should().Be("Valid Description");
    }

    #endregion

    #region 6. Single & Bulk Deletion Tests

    [Fact]
    public async Task DeleteTransactionAsync_WhenTransactionExists_DeletesSuccessfully()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var transaction = await SeedTransactionAsync(userId, description: "To be deleted");

        // Act
        var result = await sut.DeleteTransactionAsync(userId, transaction.Id);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        await using var context = await _fixture.CreateContextAsync();
        var exists = await context.Transactions.AnyAsync(t => t.Id == transaction.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteTransactionAsync_WhenTransactionDoesNotExist_ReturnsNotFoundFailure()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await sut.DeleteTransactionAsync(userId, nonExistentId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DeleteTransactionsBulkAsync_WithUserOwnedTransactions_DeletesAllAndReturnsCount()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        var tx1 = await SeedTransactionAsync(userId, amount: 10m, description: "Tx 1");
        var tx2 = await SeedTransactionAsync(userId, amount: 20m, description: "Tx 2");
        var tx3 = await SeedTransactionAsync(userId, amount: 30m, description: "Tx 3");

        var idsToDelete = new List<Guid> { tx1.Id, tx2.Id, tx3.Id };

        // Act
        var result = await sut.DeleteTransactionsBulkAsync(userId, idsToDelete);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(3);

        await using var context = await _fixture.CreateContextAsync();
        var remainingCount = await context.Transactions.CountAsync(t => t.UserId == userId);
        remainingCount.Should().Be(0);
    }

    [Fact]
    public async Task DeleteTransactionsBulkAsync_WithMixedUserOwnership_DeletesOnlyCallingUsersTransactions()
    {
        // Arrange
        var sut = CreateSut();
        var userA = CreateUserId();
        var userB = CreateUserId();

        var userATx1 = await SeedTransactionAsync(userA, amount: 15m, description: "User A Tx 1");
        var userATx2 = await SeedTransactionAsync(userA, amount: 25m, description: "User A Tx 2");
        var userBTx1 = await SeedTransactionAsync(userB, amount: 35m, description: "User B Tx 1");
        var userBTx2 = await SeedTransactionAsync(userB, amount: 45m, description: "User B Tx 2");

        var mixedIds = new List<Guid> { userATx1.Id, userATx2.Id, userBTx1.Id, userBTx2.Id };

        // Act: User A requests deletion of mixed IDs
        var result = await sut.DeleteTransactionsBulkAsync(userA, mixedIds);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2); // Only User A's 2 transactions deleted

        await using var context = await _fixture.CreateContextAsync();
        // Verify User A transactions are removed
        var userAExists1 = await context.Transactions.AnyAsync(t => t.Id == userATx1.Id);
        var userAExists2 = await context.Transactions.AnyAsync(t => t.Id == userATx2.Id);
        userAExists1.Should().BeFalse();
        userAExists2.Should().BeFalse();

        // Verify User B transactions remain untouched
        var userBExists1 = await context.Transactions.AnyAsync(t => t.Id == userBTx1.Id);
        var userBExists2 = await context.Transactions.AnyAsync(t => t.Id == userBTx2.Id);
        userBExists1.Should().BeTrue();
        userBExists2.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteTransactionsBulkAsync_WithEmptyList_ReturnsZeroWithoutModifyingData()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        await SeedTransactionAsync(userId, amount: 10m, description: "Existing Transaction");

        // Act
        var result = await sut.DeleteTransactionsBulkAsync(userId, []);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);

        await using var context = await _fixture.CreateContextAsync();
        var count = await context.Transactions.CountAsync(t => t.UserId == userId);
        count.Should().Be(1);
    }

    [Fact]
    public async Task DeleteTransactionsBulkAsync_WithNonExistentIds_ReturnsZero()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        var nonExistentIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        // Act
        var result = await sut.DeleteTransactionsBulkAsync(userId, nonExistentIds);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }

    #endregion
}
