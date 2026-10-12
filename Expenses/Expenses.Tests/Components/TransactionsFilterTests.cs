using AngleSharp.Dom;
using Bunit;
using Expenses.Common;
using Expenses.Components.Pages;
using Expenses.Models.DTOs;
using Expenses.Models.Enums;
using Expenses.Models.ViewModels;
using Expenses.Tests.Fixtures;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Expenses.Tests.Components;

public sealed class TransactionsFilterTests : BunitTestContext
{
    public TransactionsFilterTests()
    {
        // Default mock service behaviors
        MockCategoryService.GetCategoriesAsync(DefaultUserId, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<CategoryDto>>.Success([]));

        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([]));
    }

    #region TransactionFilterViewModel Unit Tests

    [Fact]
    public void TransactionFilterViewModel_DefaultConstructor_SetsPresetToThisMonthAndCalculatesMonthBounds()
    {
        // Arrange
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var expectedStart = new DateOnly(today.Year, today.Month, 1);
        var expectedEnd = expectedStart.AddMonths(1).AddDays(-1);

        // Act
        var vm = new TransactionFilterViewModel();

        // Assert
        vm.DatePreset.Should().Be(TransactionFilterViewModel.PresetThisMonth);
        vm.StartDate.Should().Be(expectedStart);
        vm.EndDate.Should().Be(expectedEnd);
        vm.Type.Should().BeNull();
        vm.SelectedCategoryId.Should().BeNull();
        vm.SearchTerm.Should().BeNull();
    }

    [Fact]
    public void TransactionFilterViewModel_ApplyPreset_ThisMonth_CalculatesFirstAndLastDayOfMonth()
    {
        // Arrange
        var referenceDate = new DateOnly(2026, 10, 12);
        var vm = new TransactionFilterViewModel();

        // Act
        vm.ApplyPreset(TransactionFilterViewModel.PresetThisMonth, referenceDate);

        // Assert
        vm.DatePreset.Should().Be(TransactionFilterViewModel.PresetThisMonth);
        vm.StartDate.Should().Be(new DateOnly(2026, 10, 1));
        vm.EndDate.Should().Be(new DateOnly(2026, 10, 31));
    }

    [Fact]
    public void TransactionFilterViewModel_ApplyPreset_LastMonth_CalculatesPreviousMonthBounds()
    {
        // Arrange
        var referenceDate = new DateOnly(2026, 10, 12);
        var vm = new TransactionFilterViewModel();

        // Act
        vm.ApplyPreset(TransactionFilterViewModel.PresetLastMonth, referenceDate);

        // Assert
        vm.DatePreset.Should().Be(TransactionFilterViewModel.PresetLastMonth);
        vm.StartDate.Should().Be(new DateOnly(2026, 9, 1));
        vm.EndDate.Should().Be(new DateOnly(2026, 9, 30));
    }

    [Fact]
    public void TransactionFilterViewModel_ApplyPreset_LastMonth_WhenJanuary_WrapsToDecemberOfPreviousYear()
    {
        // Arrange
        var referenceDate = new DateOnly(2026, 1, 15);
        var vm = new TransactionFilterViewModel();

        // Act
        vm.ApplyPreset(TransactionFilterViewModel.PresetLastMonth, referenceDate);

        // Assert
        vm.DatePreset.Should().Be(TransactionFilterViewModel.PresetLastMonth);
        vm.StartDate.Should().Be(new DateOnly(2025, 12, 1));
        vm.EndDate.Should().Be(new DateOnly(2025, 12, 31));
    }

    [Fact]
    public void TransactionFilterViewModel_ApplyPreset_LastMonth_WhenLeapYearFebruary_HandlesLeapDayCorrectly()
    {
        // Arrange (March 2024 -> previous month is February 2024 with 29 days)
        var referenceDate = new DateOnly(2024, 3, 10);
        var vm = new TransactionFilterViewModel();

        // Act
        vm.ApplyPreset(TransactionFilterViewModel.PresetLastMonth, referenceDate);

        // Assert
        vm.DatePreset.Should().Be(TransactionFilterViewModel.PresetLastMonth);
        vm.StartDate.Should().Be(new DateOnly(2024, 2, 1));
        vm.EndDate.Should().Be(new DateOnly(2024, 2, 29));
    }

    [Fact]
    public void TransactionFilterViewModel_ApplyPreset_YearToDate_CalculatesFromJan1ToCurrentDate()
    {
        // Arrange
        var referenceDate = new DateOnly(2026, 10, 12);
        var vm = new TransactionFilterViewModel();

        // Act
        vm.ApplyPreset(TransactionFilterViewModel.PresetYearToDate, referenceDate);

        // Assert
        vm.DatePreset.Should().Be(TransactionFilterViewModel.PresetYearToDate);
        vm.StartDate.Should().Be(new DateOnly(2026, 1, 1));
        vm.EndDate.Should().Be(referenceDate);
    }

    [Fact]
    public void TransactionFilterViewModel_ApplyPreset_YtdAlias_CalculatesFromJan1ToCurrentDate()
    {
        // Arrange
        var referenceDate = new DateOnly(2026, 10, 12);
        var vm = new TransactionFilterViewModel();

        // Act
        vm.ApplyPreset("YTD", referenceDate);

        // Assert
        vm.DatePreset.Should().Be("YTD");
        vm.StartDate.Should().Be(new DateOnly(2026, 1, 1));
        vm.EndDate.Should().Be(referenceDate);
    }

    [Fact]
    public void TransactionFilterViewModel_ApplyPreset_AllTime_ClearsStartAndEndDates()
    {
        // Arrange
        var vm = new TransactionFilterViewModel();
        vm.StartDate = new DateOnly(2026, 1, 1);
        vm.EndDate = new DateOnly(2026, 12, 31);

        // Act
        vm.ApplyPreset(TransactionFilterViewModel.PresetAllTime);

        // Assert
        vm.DatePreset.Should().Be(TransactionFilterViewModel.PresetAllTime);
        vm.StartDate.Should().BeNull();
        vm.EndDate.Should().BeNull();
    }

    [Fact]
    public void TransactionFilterViewModel_ApplyPreset_Custom_PreservesExistingStartAndEndDates()
    {
        // Arrange
        var vm = new TransactionFilterViewModel();
        var customStart = new DateOnly(2026, 4, 15);
        var customEnd = new DateOnly(2026, 5, 20);
        vm.StartDate = customStart;
        vm.EndDate = customEnd;

        // Act
        vm.ApplyPreset(TransactionFilterViewModel.PresetCustom);

        // Assert
        vm.DatePreset.Should().Be(TransactionFilterViewModel.PresetCustom);
        vm.StartDate.Should().Be(customStart);
        vm.EndDate.Should().Be(customEnd);
    }

    [Fact]
    public void TransactionFilterViewModel_ToFilter_MapsAllPropertiesAccurately()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var vm = new TransactionFilterViewModel
        {
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 30),
            Type = TransactionType.Expense,
            SelectedCategoryId = categoryId,
            SearchTerm = " Groceries "
        };

        // Act
        var filter = vm.ToFilter();

        // Assert
        filter.StartDate.Should().Be(new DateOnly(2026, 6, 1));
        filter.EndDate.Should().Be(new DateOnly(2026, 6, 30));
        filter.Type.Should().Be(TransactionType.Expense);
        filter.CategoryIds.Should().NotBeNull().And.ContainSingle().Which.Should().Be(categoryId);
        filter.SearchTerm.Should().Be("Groceries");
    }

    [Fact]
    public void TransactionFilterViewModel_ToFilter_WhenSearchTermIsWhitespace_NormalizesToNull()
    {
        // Arrange
        var vm = new TransactionFilterViewModel
        {
            SearchTerm = "   "
        };

        // Act
        var filter = vm.ToFilter();

        // Assert
        filter.SearchTerm.Should().BeNull();
    }

    [Fact]
    public void TransactionFilterViewModel_Reset_RestoresDefaultStateAndRecalculatesThisMonthDates()
    {
        // Arrange
        var referenceDate = new DateOnly(2026, 10, 12);
        var vm = new TransactionFilterViewModel
        {
            DatePreset = TransactionFilterViewModel.PresetAllTime,
            StartDate = null,
            EndDate = null,
            Type = TransactionType.Income,
            SelectedCategoryId = Guid.NewGuid(),
            SearchTerm = "Salary"
        };

        // Act
        vm.Reset(referenceDate);

        // Assert
        vm.DatePreset.Should().Be(TransactionFilterViewModel.PresetThisMonth);
        vm.StartDate.Should().Be(new DateOnly(2026, 10, 1));
        vm.EndDate.Should().Be(new DateOnly(2026, 10, 31));
        vm.Type.Should().BeNull();
        vm.SelectedCategoryId.Should().BeNull();
        vm.SearchTerm.Should().BeNull();
    }

    #endregion

    #region Transactions Component Filter Toolbar Rendering Tests

    [Fact]
    public void Transactions_FilterToolbar_RendersAllFilterControls()
    {
        // Arrange & Act
        var cut = RenderComponent<Transactions>();

        // Assert: Date Preset dropdown with presets
        var presetSelect = FindDatePresetSelect(cut);
        presetSelect.Should().NotBeNull("toolbar must contain a Date Preset selector");
        var presetOptions = presetSelect.QuerySelectorAll("option").Select(o => o.TextContent.Trim()).ToList();
        presetOptions.Should().Contain(o => o.Contains("This Month", StringComparison.OrdinalIgnoreCase));
        presetOptions.Should().Contain(o => o.Contains("Last Month", StringComparison.OrdinalIgnoreCase));
        presetOptions.Should().Contain(o => o.Contains("Year to Date", StringComparison.OrdinalIgnoreCase) || o.Contains("YTD", StringComparison.OrdinalIgnoreCase));
        presetOptions.Should().Contain(o => o.Contains("All Time", StringComparison.OrdinalIgnoreCase));
        presetOptions.Should().Contain(o => o.Contains("Custom", StringComparison.OrdinalIgnoreCase));

        // Assert: Start & End date inputs
        var dateInputs = cut.FindAll("input[type='date']");
        dateInputs.Should().HaveCountGreaterThanOrEqualTo(2, "toolbar must contain From and To date inputs");

        // Assert: Type selector
        var typeSelect = FindTypeSelect(cut);
        typeSelect.Should().NotBeNull("toolbar must contain a Transaction Type filter");
        var typeOptions = typeSelect.QuerySelectorAll("option").Select(o => o.TextContent.Trim()).ToList();
        typeOptions.Should().Contain(o => o.Contains("All", StringComparison.OrdinalIgnoreCase));
        typeOptions.Should().Contain(o => o.Contains("Expense", StringComparison.OrdinalIgnoreCase));
        typeOptions.Should().Contain(o => o.Contains("Income", StringComparison.OrdinalIgnoreCase));
        typeOptions.Should().Contain(o => o.Contains("Transfer", StringComparison.OrdinalIgnoreCase));

        // Assert: Category selector
        var categorySelect = FindCategorySelect(cut);
        categorySelect.Should().NotBeNull("toolbar must contain a Category selector");

        // Assert: Search input
        var searchInput = FindSearchInput(cut);
        searchInput.Should().NotBeNull("toolbar must contain a description/payee search input");

        // Assert: Reset button
        var resetButton = FindResetButton(cut);
        resetButton.Should().NotBeNull("toolbar must contain a Reset button");
    }

    [Fact]
    public void Transactions_InitialRender_LoadsCategoriesAndInvokesTransactionServiceWithDefaultFilter()
    {
        // Act
        var cut = RenderComponent<Transactions>();

        // Assert
        MockCategoryService.Received(1).GetCategoriesAsync(DefaultUserId, Arg.Any<CancellationToken>());
        MockTransactionService.Received(1).GetTransactionsAsync(
            DefaultUserId,
            Arg.Is<TransactionFilter>(f =>
                f.StartDate.HasValue &&
                f.EndDate.HasValue &&
                f.Type == null &&
                f.CategoryIds == null &&
                f.SearchTerm == null),
            Arg.Any<CancellationToken>());
    }

    #endregion

    #region Transactions Summary Metrics Bar Rendering Tests

    [Fact]
    public void Transactions_SummaryMetricsBar_WhenTransactionsExist_RendersFormattedMetricsAndCounts()
    {
        // Arrange
        var t1 = new TransactionDto(
            Id: Guid.NewGuid(),
            Date: new DateOnly(2026, 10, 5),
            Amount: 3500.00m,
            Description: "Monthly Salary",
            Type: TransactionType.Income,
            CategoryId: Guid.NewGuid(),
            CategoryName: "Income",
            SubcategoryId: null,
            SubcategoryName: null,
            Notes: null,
            CreatedAtUtc: DateTime.UtcNow);

        var t2 = new TransactionDto(
            Id: Guid.NewGuid(),
            Date: new DateOnly(2026, 10, 6),
            Amount: 1200.50m,
            Description: "Apartment Rent",
            Type: TransactionType.Expense,
            CategoryId: Guid.NewGuid(),
            CategoryName: "Housing",
            SubcategoryId: null,
            SubcategoryName: null,
            Notes: null,
            CreatedAtUtc: DateTime.UtcNow);

        var t3 = new TransactionDto(
            Id: Guid.NewGuid(),
            Date: new DateOnly(2026, 10, 7),
            Amount: 79.50m,
            Description: "Weekly Groceries",
            Type: TransactionType.Expense,
            CategoryId: Guid.NewGuid(),
            CategoryName: "Food",
            SubcategoryId: null,
            SubcategoryName: null,
            Notes: null,
            CreatedAtUtc: DateTime.UtcNow);

        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([t1, t2, t3]));

        // Act
        var cut = RenderComponent<Transactions>();

        // Assert: Filtered count (3 records)
        cut.Markup.Should().Match(m =>
            m.Contains("3 transactions") ||
            m.Contains("Showing 3") ||
            m.Contains("Count: 3") ||
            m.Contains("Count") && m.Contains("3") ||
            m.Contains("3 items"));

        // Assert: Total Income (+€3,500.00, green/success styling)
        cut.Markup.Should().Contain("3,500.00", "Total income amount must be formatted as 3,500.00");
        cut.Markup.Should().Match(m =>
            m.Contains("€3,500.00") ||
            m.Contains("+€3,500.00") ||
            m.Contains("€ 3,500.00"),
            "Total income must be formatted in EUR");
        cut.FindAll(".text-success, .border-success, .bg-success, .income").Should().NotBeEmpty(
            "Income metrics card should have green/success styling");

        // Assert: Total Expenses (-€1,280.00, red/danger styling)
        cut.Markup.Should().Contain("1,280.00", "Total expense amount must sum 1200.50 + 79.50 = 1,280.00");
        cut.Markup.Should().Match(m =>
            m.Contains("€1,280.00") ||
            m.Contains("-€1,280.00") ||
            m.Contains("€ 1,280.00"),
            "Total expense must be formatted in EUR");
        cut.FindAll(".text-danger, .border-danger, .bg-danger, .expense").Should().NotBeEmpty(
            "Expense metrics card should have red/danger styling");

        // Assert: Net Balance (€2,220.00 = 3500 - 1280)
        cut.Markup.Should().Contain("2,220.00", "Net balance must compute Income minus Expenses: 3500 - 1280 = 2,220.00");
        cut.Markup.Should().Match(m =>
            m.Contains("€2,220.00") ||
            m.Contains("+€2,220.00") ||
            m.Contains("€ 2,220.00"),
            "Net balance must be formatted in EUR");
    }

    [Fact]
    public void Transactions_SummaryMetricsBar_WhenNoTransactions_RendersZeroValues()
    {
        // Arrange
        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([]));

        // Act
        var cut = RenderComponent<Transactions>();

        // Assert: 0 amounts
        cut.Markup.Should().Contain("0.00");
        cut.Markup.Should().Match(m =>
            m.Contains("€0.00") ||
            m.Contains("€ 0.00") ||
            m.Contains("+€0.00") ||
            m.Contains("-€0.00"));
    }

    [Fact]
    public void Transactions_SummaryMetricsBar_WhenExpensesExceedIncome_RendersNegativeNetBalance()
    {
        // Arrange: Income = 500, Expenses = 800 -> Net = -300
        var income = new TransactionDto(
            Id: Guid.NewGuid(),
            Date: new DateOnly(2026, 10, 1),
            Amount: 500.00m,
            Description: "Bonus",
            Type: TransactionType.Income,
            CategoryId: null,
            CategoryName: null,
            SubcategoryId: null,
            SubcategoryName: null,
            Notes: null,
            CreatedAtUtc: DateTime.UtcNow);

        var expense = new TransactionDto(
            Id: Guid.NewGuid(),
            Date: new DateOnly(2026, 10, 2),
            Amount: 800.00m,
            Description: "Car Repair",
            Type: TransactionType.Expense,
            CategoryId: null,
            CategoryName: null,
            SubcategoryId: null,
            SubcategoryName: null,
            Notes: null,
            CreatedAtUtc: DateTime.UtcNow);

        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([income, expense]));

        // Act
        var cut = RenderComponent<Transactions>();

        // Assert: -300.00
        cut.Markup.Should().Contain("300.00");
        cut.Markup.Should().Match(m =>
            m.Contains("-€300.00") ||
            m.Contains("€-300.00") ||
            m.Contains("-300.00") ||
            m.Contains("(€300.00)"));
    }

    #endregion

    #region Transactions Filter Change & Reset Interactions Tests

    [Fact]
    public void Transactions_ChangingDatePreset_UpdatesDatesAndRequeriesTransactions()
    {
        // Arrange
        var cut = RenderComponent<Transactions>();
        var presetSelect = FindDatePresetSelect(cut);

        // Act: Change preset to LastMonth
        presetSelect.Change(TransactionFilterViewModel.PresetLastMonth);

        // Assert
        MockTransactionService.Received().GetTransactionsAsync(
            DefaultUserId,
            Arg.Is<TransactionFilter>(f =>
                f.StartDate.HasValue &&
                f.EndDate.HasValue &&
                f.StartDate.Value < f.EndDate.Value),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Transactions_ChangingTypeFilter_RequeriesTransactionsWithType()
    {
        // Arrange
        var cut = RenderComponent<Transactions>();
        var typeSelect = FindTypeSelect(cut);

        // Act: Change type to Expense
        typeSelect.Change(nameof(TransactionType.Expense));

        // Assert
        MockTransactionService.Received().GetTransactionsAsync(
            DefaultUserId,
            Arg.Is<TransactionFilter>(f => f.Type == TransactionType.Expense),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Transactions_ChangingCategoryFilter_RequeriesTransactionsWithSelectedCategoryId()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var category = new CategoryDto(
            Id: categoryId,
            Name: "Groceries",
            ParentCategoryId: null,
            ColorCode: "#10B981",
            Icon: null,
            DisplayOrder: 1,
            Subcategories: []);

        MockCategoryService.GetCategoriesAsync(DefaultUserId, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<CategoryDto>>.Success([category]));

        var cut = RenderComponent<Transactions>();
        var categorySelect = FindCategorySelect(cut);

        // Act: Select the category
        categorySelect.Change(categoryId.ToString());

        // Assert
        MockTransactionService.Received().GetTransactionsAsync(
            DefaultUserId,
            Arg.Is<TransactionFilter>(f => f.CategoryIds != null && f.CategoryIds.Contains(categoryId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Transactions_ChangingSearchTerm_RequeriesTransactionsWithSearchTerm()
    {
        // Arrange
        var cut = RenderComponent<Transactions>();
        var searchInput = FindSearchInput(cut);

        // Act: Type search query
        searchInput.Change("Supermarket");

        // Assert
        MockTransactionService.Received().GetTransactionsAsync(
            DefaultUserId,
            Arg.Is<TransactionFilter>(f => f.SearchTerm == "Supermarket"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Transactions_ClickingResetFilters_RestoresDefaultsAndQueriesTransactionsWithDefaultFilter()
    {
        // Arrange
        var cut = RenderComponent<Transactions>();

        // Set type and search filters
        var typeSelect = FindTypeSelect(cut);
        typeSelect.Change(nameof(TransactionType.Expense));

        var searchInput = FindSearchInput(cut);
        searchInput.Change("Amazon");

        // Act: Click Reset
        var resetButton = FindResetButton(cut);
        resetButton.Click();

        // Assert: Filter query called with reset values
        MockTransactionService.Received().GetTransactionsAsync(
            DefaultUserId,
            Arg.Is<TransactionFilter>(f =>
                f.Type == null &&
                f.CategoryIds == null &&
                f.SearchTerm == null &&
                f.StartDate.HasValue &&
                f.EndDate.HasValue),
            Arg.Any<CancellationToken>());

        // Verify inputs in UI reflect reset state
        var recheckSearchInput = FindSearchInput(cut);
        recheckSearchInput.GetAttribute("value").Should().BeNullOrEmpty();
    }

    #endregion

    #region Helper Methods

    private static IElement FindDatePresetSelect(IRenderedComponent<Transactions> cut) =>
        cut.FindAll("select")
            .First(s => s.QuerySelectorAll("option").Any(o =>
                o.GetAttribute("value") == TransactionFilterViewModel.PresetThisMonth ||
                o.TextContent.Contains("This Month", StringComparison.OrdinalIgnoreCase)));

    private static IElement FindTypeSelect(IRenderedComponent<Transactions> cut) =>
        cut.FindAll("select")
            .First(s => s.QuerySelectorAll("option").Any(o =>
                o.GetAttribute("value") == nameof(TransactionType.Expense) ||
                o.TextContent.Contains("Expense", StringComparison.OrdinalIgnoreCase)));

    private static IElement FindCategorySelect(IRenderedComponent<Transactions> cut) =>
        cut.FindAll("select")
            .First(s => s.QuerySelectorAll("option").Any(o =>
                o.TextContent.Contains("All Categories", StringComparison.OrdinalIgnoreCase) ||
                o.TextContent.Contains("Category", StringComparison.OrdinalIgnoreCase)));

    private static IElement FindSearchInput(IRenderedComponent<Transactions> cut) =>
        cut.FindAll("input")
            .First(i => i.GetAttribute("type") is "text" or "search" &&
                        (i.GetAttribute("placeholder")?.Contains("search", StringComparison.OrdinalIgnoreCase) == true ||
                         i.GetAttribute("name")?.Contains("search", StringComparison.OrdinalIgnoreCase) == true ||
                         (i.Id != null && i.Id.Contains("search", StringComparison.OrdinalIgnoreCase))));

    private static IElement FindResetButton(IRenderedComponent<Transactions> cut) =>
        cut.FindAll("button")
            .First(b => b.TextContent.Trim().Equals("Reset", StringComparison.OrdinalIgnoreCase) ||
                        b.TextContent.Contains("Reset", StringComparison.OrdinalIgnoreCase));

    #endregion
}
