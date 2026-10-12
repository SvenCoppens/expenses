using System.Globalization;
using System.Security.Claims;
using AngleSharp.Dom;
using Bunit;
using Bunit.TestDoubles;
using Expenses.Components;
using Expenses.Components.Account.Shared;
using Expenses.Components.Layout;
using Expenses.Components.Pages;
using Expenses.Data;
using Expenses.Models.Entities;
using Expenses.Models.Enums;
using Expenses.Services.Categories;
using Expenses.Services.Transactions;
using Expenses.Tests.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Expenses.Tests.Integration;

public sealed class ExpenseTrackerUiIntegrationTests : TestContext, IClassFixture<SqliteTestDbFixture>
{
    private readonly SqliteTestDbFixture _fixture;
    private readonly TestAuthorizationContext _authContext;
    private readonly ICategoryService _realCategoryService;
    private readonly ITransactionService _realTransactionService;

    public ExpenseTrackerUiIntegrationTests(SqliteTestDbFixture fixture)
    {
        _fixture = fixture;

        _realCategoryService = new CategoryService(
            _fixture.Factory,
            NullLogger<CategoryService>.Instance);

        _realTransactionService = new TransactionService(
            _fixture.Factory,
            NullLogger<TransactionService>.Instance);

        Services.AddSingleton<ICategoryService>(_realCategoryService);
        Services.AddSingleton<ITransactionService>(_realTransactionService);

        _authContext = this.AddTestAuthorization();
        _authContext.SetAuthorized("testuser@example.com");
        _authContext.SetClaims(new Claim(ClaimTypes.NameIdentifier, DefaultUserId));

        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    private const string DefaultUserId = "test-integration-user-001";
    private static string CreateUserId() => $"integration_user_{Guid.NewGuid():N}";

    private void SetAuthenticatedUser(string userId, string email = "user@example.com")
    {
        _authContext.SetAuthorized(email);
        _authContext.SetClaims(new Claim(ClaimTypes.NameIdentifier, userId));
    }

    private void SetUnauthenticated()
    {
        _authContext.SetNotAuthorized();
    }

    private async Task<Category> SeedCategoryAsync(
        string userId,
        string name,
        Guid? parentCategoryId = null,
        string colorCode = "#3B82F6",
        int displayOrder = 1,
        string? icon = null)
    {
        await using var context = await _fixture.CreateContextAsync();
        var category = new Category
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            ParentCategoryId = parentCategoryId,
            ColorCode = colorCode,
            DisplayOrder = displayOrder,
            Icon = icon
        };

        context.Categories.Add(category);
        await context.SaveChangesAsync();
        return category;
    }

    private async Task<Transaction> SeedTransactionAsync(
        string userId,
        decimal amount,
        string description,
        TransactionType type = TransactionType.Expense,
        DateOnly? date = null,
        Guid? categoryId = null,
        Guid? subcategoryId = null,
        string? notes = null)
    {
        await using var context = await _fixture.CreateContextAsync();
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = amount,
            Description = description,
            Type = type,
            Date = date ?? DateOnly.FromDateTime(DateTime.UtcNow),
            CategoryId = categoryId,
            SubcategoryId = subcategoryId,
            Notes = notes,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Transactions.Add(transaction);
        await context.SaveChangesAsync();
        return transaction;
    }

    #region 1. Authorization Attributes & Routing Integration Tests

    [Fact]
    public void Categories_Component_HasAuthorizeAttribute()
    {
        // Act
        var hasAuthorize = typeof(Categories).IsDefined(typeof(AuthorizeAttribute), inherit: true);

        // Assert
        hasAuthorize.Should().BeTrue("Categories page must be protected with [Authorize]");
    }

    [Fact]
    public void Transactions_Component_HasAuthorizeAttribute()
    {
        // Act
        var hasAuthorize = typeof(Transactions).IsDefined(typeof(AuthorizeAttribute), inherit: true);

        // Assert
        hasAuthorize.Should().BeTrue("Transactions page must be protected with [Authorize]");
    }

    [Fact]
    public void RedirectToLogin_WhenRendered_RedirectsToAccountLoginWithReturnUrl()
    {
        // Arrange
        var nav = Services.GetRequiredService<FakeNavigationManager>();
        nav.NavigateTo("http://localhost/categories");

        // Act
        RenderComponent<RedirectToLogin>();

        // Assert
        nav.Uri.Should().Contain("Account/Login");
        nav.Uri.Should().Contain("returnUrl=");
        nav.Uri.Should().Contain(Uri.EscapeDataString("http://localhost/categories"));
    }

    [Fact]
    public void Routes_WhenAnonymousUserNavigatesToCategories_RedirectsToAccountLogin()
    {
        // Arrange
        SetUnauthenticated();
        var nav = Services.GetRequiredService<FakeNavigationManager>();
        nav.NavigateTo("http://localhost/categories");

        // Act
        RenderComponent<Routes>();

        // Assert
        nav.Uri.Should().Contain("Account/Login");
        nav.Uri.Should().Contain("returnUrl=");
        nav.Uri.Should().Contain("categories");
    }

    [Fact]
    public void Routes_WhenAnonymousUserNavigatesToTransactions_RedirectsToAccountLogin()
    {
        // Arrange
        SetUnauthenticated();
        var nav = Services.GetRequiredService<FakeNavigationManager>();
        nav.NavigateTo("http://localhost/transactions");

        // Act
        RenderComponent<Routes>();

        // Assert
        nav.Uri.Should().Contain("Account/Login");
        nav.Uri.Should().Contain("returnUrl=");
        nav.Uri.Should().Contain("transactions");
    }

    [Fact]
    public void NavMenu_WhenAnonymousUser_RendersPublicLinksAndHidesFinancialPages()
    {
        // Arrange
        SetUnauthenticated();

        // Act
        var cut = RenderComponent<NavMenu>();

        // Assert
        cut.FindAll("nav a").Where(a => a.GetAttribute("href") is "transactions" or "/transactions").Should().BeEmpty();
        cut.FindAll("nav a").Where(a => a.GetAttribute("href") is "categories" or "/categories").Should().BeEmpty();
        cut.FindAll("nav a").Where(a => a.GetAttribute("href") is "" or "/").Should().ContainSingle();
    }

    [Fact]
    public void NavMenu_WhenAuthenticatedUser_RendersTransactionsAndCategoriesLinks()
    {
        // Arrange
        SetAuthenticatedUser(CreateUserId());

        // Act
        var cut = RenderComponent<NavMenu>();

        // Assert
        cut.FindAll("nav a").Where(a => a.GetAttribute("href") is "transactions" or "/transactions").Should().ContainSingle();
        cut.FindAll("nav a").Where(a => a.GetAttribute("href") is "categories" or "/categories").Should().ContainSingle();
    }

    #endregion

    #region 2. Categories Page Integration Tests (Real SQLite DB + Real Service)

    [Fact]
    public void CategoriesPage_WhenDatabaseEmpty_RendersEmptyStateViaRealService()
    {
        // Arrange
        var userId = CreateUserId();
        SetAuthenticatedUser(userId);

        // Act
        var cut = RenderComponent<Categories>();

        // Assert
        cut.Markup.Should().Contain("No categories found");
        cut.FindAll(".category-tree .card").Should().BeEmpty();
    }

    [Fact]
    public async Task CategoriesPage_WhenCategoriesExistInDatabase_RendersTwoTierHierarchyWithColorSwatches()
    {
        // Arrange
        var userId = CreateUserId();
        SetAuthenticatedUser(userId);

        var parent = await SeedCategoryAsync(userId, "Housing & Living", null, "#3B82F6", 1, "bi-house");
        await SeedCategoryAsync(userId, "Rent & Mortgage", parent.Id, "#60A5FA", 1);
        await SeedCategoryAsync(userId, "Home Insurance", parent.Id, "#93C5FD", 2);

        // Act
        var cut = RenderComponent<Categories>();

        // Assert
        cut.FindAll(".category-tree .card").Should().ContainSingle();
        var categoryTree = cut.Find(".category-tree");
        categoryTree.TextContent.Should().Contain("Housing & Living");
        categoryTree.TextContent.Should().Contain("Rent & Mortgage");
        categoryTree.TextContent.Should().Contain("Home Insurance");
        cut.Markup.Should().Contain("#3B82F6");
        cut.Markup.Should().Contain("bi-house");
    }

    [Fact]
    public async Task CategoriesPage_WhenAddParentCategoryFormSubmitted_PersistsCategoryInSqliteAndUpdatesUi()
    {
        // Arrange
        var userId = CreateUserId();
        SetAuthenticatedUser(userId);
        var cut = RenderComponent<Categories>();

        // Act
        cut.Find("button.btn-primary").Click();
        cut.Find("#categoryName").Change("Healthcare & Fitness");
        cut.Find("#categoryColor").Change("#EF4444");
        cut.Find("#categoryDisplayOrder").Change("3");
        cut.Find(".modal form").Submit();

        // Assert - UI reflects new category
        cut.Find(".category-tree").TextContent.Should().Contain("Healthcare & Fitness");

        // Assert - Database persisted record
        await using var context = await _fixture.CreateContextAsync();
        var savedCategory = await context.Categories.FirstOrDefaultAsync(c => c.UserId == userId && c.Name == "Healthcare & Fitness");
        savedCategory.Should().NotBeNull();
        savedCategory!.ColorCode.Should().Be("#EF4444");
        savedCategory.DisplayOrder.Should().Be(3);
        savedCategory.ParentCategoryId.Should().BeNull();
    }

    [Fact]
    public async Task CategoriesPage_WhenAddSubcategoryFormSubmitted_PersistsSubcategoryUnderParentInSqlite()
    {
        // Arrange
        var userId = CreateUserId();
        SetAuthenticatedUser(userId);
        var parent = await SeedCategoryAsync(userId, "Food & Groceries", null, "#10B981", 1);
        var cut = RenderComponent<Categories>();

        // Act - Click "+ Add Subcategory"
        cut.Find("button.btn-outline-success").Click();
        cut.Find("#categoryName").Change("Supermarket");
        cut.Find(".modal form").Submit();

        // Assert - UI reflects subcategory
        cut.Find(".category-tree").TextContent.Should().Contain("Supermarket");

        // Assert - Database persisted subcategory linked to parent
        await using var context = await _fixture.CreateContextAsync();
        var subcategory = await context.Categories.FirstOrDefaultAsync(c => c.UserId == userId && c.Name == "Supermarket");
        subcategory.Should().NotBeNull();
        subcategory!.ParentCategoryId.Should().Be(parent.Id);
    }

    [Fact]
    public async Task CategoriesPage_WhenDeleteCategoryConfirmed_RemovesCategoryFromSqliteAndUi()
    {
        // Arrange
        var userId = CreateUserId();
        SetAuthenticatedUser(userId);
        var cat = await SeedCategoryAsync(userId, "Obsolete Subscriptions", null, "#8B5CF6", 1);
        var cut = RenderComponent<Categories>();

        // Act - Click "Delete" and confirm
        cut.Find("button.btn-outline-danger").Click();
        cut.Find(".confirm-dialog button.btn-danger").Click();

        // Assert - UI removes category
        cut.Markup.Should().NotContain("Obsolete Subscriptions");

        // Assert - SQLite database no longer contains category
        await using var context = await _fixture.CreateContextAsync();
        var existsInDb = await context.Categories.AnyAsync(c => c.Id == cat.Id);
        existsInDb.Should().BeFalse();
    }

    [Fact]
    public async Task CategoriesPage_WhenDeletingCategoryWithLinkedTransactions_DisplaysCategoryInUseErrorAndRetainsCategory()
    {
        // Arrange
        var userId = CreateUserId();
        SetAuthenticatedUser(userId);
        var cat = await SeedCategoryAsync(userId, "Dining & Takeout", null, "#F59E0B", 1);
        await SeedTransactionAsync(
            userId,
            28.50m,
            "Pizzeria",
            TransactionType.Expense,
            DateOnly.FromDateTime(DateTime.UtcNow),
            cat.Id);

        var cut = RenderComponent<Categories>();

        // Act - Attempt to delete category in use
        cut.Find("button.btn-outline-danger").Click();
        cut.Find(".confirm-dialog button.btn-danger").Click();

        // Assert - Error banner rendered with expected spec message
        cut.Find(".alert-danger").TextContent.Should().Contain(
            "Cannot delete category because it is currently assigned to existing transactions. Please reassign those transactions before deleting.");

        // Assert - Category retained in SQLite database
        await using var context = await _fixture.CreateContextAsync();
        var existsInDb = await context.Categories.AnyAsync(c => c.Id == cat.Id);
        existsInDb.Should().BeTrue();
    }

    #endregion

    #region 3. Transactions Page Integration Tests (Real SQLite DB + Real Services)

    [Fact]
    public void TransactionsPage_WhenDatabaseEmpty_RendersEmptyStateAndZeroMetrics()
    {
        // Arrange
        var userId = CreateUserId();
        SetAuthenticatedUser(userId);

        // Act
        var cut = RenderComponent<Transactions>();

        // Assert
        cut.Markup.Should().Contain("0 transactions");
        cut.Markup.Should().Contain("+€0.00");
        cut.Markup.Should().Contain("-€0.00");
        cut.Markup.Should().Contain("€0.00");
        cut.Markup.Should().Contain("No transactions found");
    }

    [Fact]
    public async Task TransactionsPage_WhenTransactionsExistInDatabase_RendersRowsWithFormattedEurAndCalculatesAccurateMetrics()
    {
        // Arrange
        var userId = CreateUserId();
        SetAuthenticatedUser(userId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var incomeCat = await SeedCategoryAsync(userId, "Work Income", null, "#10B981");
        var expenseCat = await SeedCategoryAsync(userId, "Groceries", null, "#EF4444");

        await SeedTransactionAsync(userId, 3200.00m, "Monthly Paycheck", TransactionType.Income, today, incomeCat.Id);
        await SeedTransactionAsync(userId, 84.50m, "Bio Supermarket", TransactionType.Expense, today, expenseCat.Id);

        // Act
        var cut = RenderComponent<Transactions>();

        // Assert - Summary bar calculations
        cut.Markup.Should().Contain("2 transactions");
        cut.Markup.Should().Contain("+€3,200.00");
        cut.Markup.Should().Contain("-€84.50");
        cut.Markup.Should().Contain("€3,115.50");

        // Assert - Grid rows and formatting
        cut.Markup.Should().Contain("Monthly Paycheck");
        cut.Markup.Should().Contain("Bio Supermarket");
        cut.Markup.Should().Contain("Work Income");
        cut.Markup.Should().Contain("Groceries");
        cut.Markup.Should().Contain("+€3,200.00");
        cut.Markup.Should().Contain("-€84.50");
    }

    [Fact]
    public async Task TransactionsPage_WhenFilterByTypeApplied_QueriesRealDatabaseAndUpdatesGridAndSummary()
    {
        // Arrange
        var userId = CreateUserId();
        SetAuthenticatedUser(userId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        await SeedTransactionAsync(userId, 1500.00m, "Freelance Gig", TransactionType.Income, today);
        await SeedTransactionAsync(userId, 45.00m, "Dinner with Friends", TransactionType.Expense, today);

        var cut = RenderComponent<Transactions>();
        cut.Markup.Should().Contain("2 transactions");

        // Act - Select Type = Expense
        cut.Find("#type-select").Change(TransactionType.Expense.ToString());

        // Assert - Filtered grid updates via real service query
        cut.Markup.Should().Contain("1 transactions");
        cut.Markup.Should().Contain("Dinner with Friends");
        cut.Markup.Should().NotContain("Freelance Gig");
        cut.Markup.Should().Contain("-€45.00");
        cut.Markup.Should().Contain("+€0.00");
    }

    [Fact]
    public async Task TransactionsPage_WhenInlineRowEditedAndSaved_PersistsUpdatesInSqliteAndRefreshesRow()
    {
        // Arrange
        var userId = CreateUserId();
        SetAuthenticatedUser(userId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var tx = await SeedTransactionAsync(userId, 25.00m, "Book Store", TransactionType.Expense, today);

        var cut = RenderComponent<Transactions>();

        // Act - Click "Edit" and update values
        cut.Find("button[title='Edit']").Click();
        var activeRow = cut.FindAll("tbody tr")[0];
        var textInputs = activeRow.QuerySelectorAll("input[type='text'], input:not([type])").ToList();
        textInputs.First().Change("Technical Books");
        var amountInput = activeRow.QuerySelector("input[type='number'], input[step='0.01']");
        amountInput?.Change("49.99");
        cut.Find("button[title='Save']").Click();

        // Assert - UI reflects updated row
        cut.Markup.Should().Contain("Technical Books");
        cut.Markup.Should().Contain("-€49.99");

        // Assert - SQLite database has updated values
        await using var context = await _fixture.CreateContextAsync();
        var updated = await context.Transactions.FindAsync(tx.Id);
        updated.Should().NotBeNull();
        updated!.Description.Should().Be("Technical Books");
        updated.Amount.Should().Be(49.99m);
    }

    [Fact]
    public async Task TransactionsPage_WhenAddTransactionModalSubmitted_PersistsNewTransactionToSqliteAndUpdatesUi()
    {
        // Arrange
        var userId = CreateUserId();
        SetAuthenticatedUser(userId);
        var cat = await SeedCategoryAsync(userId, "Travel & Transit", null, "#3B82F6");
        var cut = RenderComponent<Transactions>();

        // Act - Open "+ Add Transaction" modal and submit valid entry
        cut.FindAll("button").First(b => b.TextContent.Contains("Add Transaction")).Click();
        cut.Find("#add-description").Change("Train Ticket Intercity");
        cut.Find("#add-amount").Change("36.50");
        cut.Find("#add-category").Change(cat.Id.ToString());
        cut.Find(".modal form").Submit();

        // Assert - UI displays new transaction
        cut.Markup.Should().Contain("Train Ticket Intercity");
        cut.Markup.Should().Contain("-€36.50");

        // Assert - SQLite database persisted record
        await using var context = await _fixture.CreateContextAsync();
        var created = await context.Transactions.FirstOrDefaultAsync(t => t.UserId == userId && t.Description == "Train Ticket Intercity");
        created.Should().NotBeNull();
        created!.Amount.Should().Be(36.50m);
        created.CategoryId.Should().Be(cat.Id);
    }

    [Fact]
    public async Task TransactionsPage_WhenBulkDeleteConfirmed_DeletesSelectedFromSqliteAndRefreshesGrid()
    {
        // Arrange
        var userId = CreateUserId();
        SetAuthenticatedUser(userId);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var tx1 = await SeedTransactionAsync(userId, 15.00m, "Delete Candidate 1", TransactionType.Expense, today);
        var tx2 = await SeedTransactionAsync(userId, 25.00m, "Delete Candidate 2", TransactionType.Expense, today);
        var txKeep = await SeedTransactionAsync(userId, 35.00m, "Preserve Candidate", TransactionType.Expense, today);

        var cut = RenderComponent<Transactions>();

        // Act - Select tx1 and tx2 and confirm bulk deletion
        cut.FindAll("tbody tr").First(r => r.TextContent.Contains("Delete Candidate 1"))
            .QuerySelector("input[type='checkbox']")!.Change(true);
        cut.FindAll("tbody tr").First(r => r.TextContent.Contains("Delete Candidate 2"))
            .QuerySelector("input[type='checkbox']")!.Change(true);

        var deleteSelectedBtn = cut.FindAll("button").First(b => b.TextContent.Contains("Delete Selected"));
        deleteSelectedBtn.Click();

        cut.Find(".confirm-dialog button.btn-danger").Click();

        // Assert - UI updates and retains only txKeep
        cut.Markup.Should().NotContain("Delete Candidate 1");
        cut.Markup.Should().NotContain("Delete Candidate 2");
        cut.Markup.Should().Contain("Preserve Candidate");
        cut.Markup.Should().Contain("1 transactions");

        // Assert - SQLite database records deleted
        await using var context = await _fixture.CreateContextAsync();
        var remaining = await context.Transactions.Where(t => t.UserId == userId).ToListAsync();
        remaining.Should().ContainSingle();
        remaining.Single().Id.Should().Be(txKeep.Id);
    }

    #endregion

    #region 4. Multi-Tenant Data Isolation & Anonymous Access Integration Tests

    [Fact]
    public async Task MultiTenantIsolation_RealDatabaseQueries_EnsureStrictDataIsolationBetweenUsers()
    {
        // Arrange
        var userA = CreateUserId();
        var userB = CreateUserId();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var catA = await SeedCategoryAsync(userA, "Tenant A Private Category");
        await SeedTransactionAsync(userA, 120.00m, "Tenant A Private Expense", TransactionType.Expense, today, catA.Id);

        var catB = await SeedCategoryAsync(userB, "Tenant B Private Category");
        await SeedTransactionAsync(userB, 240.00m, "Tenant B Private Expense", TransactionType.Expense, today, catB.Id);

        // Act & Assert 1: Authenticated as Tenant A
        SetAuthenticatedUser(userA);
        var categoriesA = RenderComponent<Categories>();
        categoriesA.Find(".category-tree").TextContent.Should().Contain("Tenant A Private Category");
        categoriesA.Find(".category-tree").TextContent.Should().NotContain("Tenant B Private Category");

        var transactionsA = RenderComponent<Transactions>();
        transactionsA.Markup.Should().Contain("Tenant A Private Expense");
        transactionsA.Markup.Should().NotContain("Tenant B Private Expense");

        // Act & Assert 2: Authenticated as Tenant B
        SetAuthenticatedUser(userB);
        var categoriesB = RenderComponent<Categories>();
        categoriesB.Find(".category-tree").TextContent.Should().Contain("Tenant B Private Category");
        categoriesB.Find(".category-tree").TextContent.Should().NotContain("Tenant A Private Category");

        var transactionsB = RenderComponent<Transactions>();
        transactionsB.Markup.Should().Contain("Tenant B Private Expense");
        transactionsB.Markup.Should().NotContain("Tenant A Private Expense");
    }

    [Fact]
    public async Task UnauthenticatedAccess_WhenRenderedDirectlyWithoutAuth_DoesNotLoadAnyUserData()
    {
        // Arrange
        var user = CreateUserId();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await SeedCategoryAsync(user, "Confidential Category");
        await SeedTransactionAsync(user, 500m, "Confidential Transaction", TransactionType.Expense, today);

        SetUnauthenticated();

        // Act
        var cutCat = RenderComponent<Categories>();
        var cutTx = RenderComponent<Transactions>();

        // Assert - Zero data leaked when user identity is null/unauthenticated
        cutCat.Markup.Should().NotContain("Confidential Category");
        cutTx.Markup.Should().NotContain("Confidential Transaction");
    }

    #endregion
}
