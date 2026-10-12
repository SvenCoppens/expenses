using System.ComponentModel.DataAnnotations;
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

public sealed class TransactionsBulkActionsTests : BunitTestContext
{
    public TransactionsBulkActionsTests()
    {
        MockCategoryService.GetCategoriesAsync(DefaultUserId, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<CategoryDto>>.Success([]));

        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([]));
    }

    #region TransactionFormModel Validation & Mapping Unit Tests

    [Theory]
    [InlineData(0, false, "Amount must be greater than zero.")]
    [InlineData(-10.50, false, "Amount must be greater than zero.")]
    [InlineData(-0.01, false, "Amount must be greater than zero.")]
    [InlineData(0.01, true, null)]
    [InlineData(45.50, true, null)]
    [InlineData(1000000.00, true, null)]
    public void TransactionFormModel_AmountValidation_EnforcesGreaterThanZero(double amountDouble, bool isValid, string? expectedError)
    {
        // Arrange
        var amount = (decimal)amountDouble;
        var model = new TransactionFormModel
        {
            Date = new DateOnly(2026, 10, 12),
            Amount = amount,
            Description = "Dinner with friends",
            Type = TransactionType.Expense
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        if (isValid)
        {
            results.Should().NotContain(r => r.MemberNames.Contains(nameof(TransactionFormModel.Amount)));
        }
        else
        {
            results.Should().Contain(r => r.ErrorMessage == expectedError);
        }
    }

    [Theory]
    [InlineData("", false, "Description is required.")]
    [InlineData("   ", false, "Description is required.")]
    [InlineData("Weekly Groceries", true, null)]
    public void TransactionFormModel_DescriptionValidation_EnforcesRequired(string description, bool isValid, string? expectedError)
    {
        // Arrange
        var model = new TransactionFormModel
        {
            Date = new DateOnly(2026, 10, 12),
            Amount = 25.00m,
            Description = description,
            Type = TransactionType.Expense
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        if (isValid)
        {
            results.Should().NotContain(r => r.MemberNames.Contains(nameof(TransactionFormModel.Description)));
        }
        else
        {
            results.Should().Contain(r => r.ErrorMessage == expectedError);
        }
    }

    [Fact]
    public void TransactionFormModel_DescriptionLength_Exceeding250Chars_FailsValidation()
    {
        // Arrange
        var model = new TransactionFormModel
        {
            Date = new DateOnly(2026, 10, 12),
            Amount = 25.00m,
            Description = new string('A', 251),
            Type = TransactionType.Expense
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        results.Should().Contain(r => r.ErrorMessage == "Description cannot exceed 250 characters.");
    }

    [Fact]
    public void TransactionFormModel_NotesLength_Exceeding1000Chars_FailsValidation()
    {
        // Arrange
        var model = new TransactionFormModel
        {
            Date = new DateOnly(2026, 10, 12),
            Amount = 25.00m,
            Description = "Business Lunch",
            Type = TransactionType.Expense,
            Notes = new string('N', 1001)
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        results.Should().Contain(r => r.ErrorMessage == "Notes cannot exceed 1000 characters.");
    }

    [Fact]
    public void TransactionFormModel_ToRequest_MapsAllPropertiesAccuratelyAndTrimsStrings()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var subcategoryId = Guid.NewGuid();
        var model = new TransactionFormModel
        {
            Date = new DateOnly(2026, 10, 15),
            Amount = 120.50m,
            Description = "  Consulting Service  ",
            Type = TransactionType.Income,
            CategoryId = categoryId,
            SubcategoryId = subcategoryId,
            Notes = "  Invoice #2026-99  "
        };

        // Act
        var request = model.ToRequest();

        // Assert
        request.Date.Should().Be(new DateOnly(2026, 10, 15));
        request.Amount.Should().Be(120.50m);
        request.Description.Should().Be("Consulting Service");
        request.Type.Should().Be(TransactionType.Income);
        request.CategoryId.Should().Be(categoryId);
        request.SubcategoryId.Should().Be(subcategoryId);
        request.Notes.Should().Be("Invoice #2026-99");
    }

    [Fact]
    public void TransactionFormModel_Reset_RestoresDefaultValues()
    {
        // Arrange
        var model = new TransactionFormModel
        {
            Amount = 99.99m,
            Description = "Custom Item",
            Type = TransactionType.Income,
            CategoryId = Guid.NewGuid(),
            SubcategoryId = Guid.NewGuid(),
            Notes = "Some note"
        };

        // Act
        model.Reset();

        // Assert
        model.Amount.Should().Be(0m);
        model.Description.Should().BeEmpty();
        model.Type.Should().Be(TransactionType.Expense);
        model.CategoryId.Should().BeNull();
        model.SubcategoryId.Should().BeNull();
        model.Notes.Should().BeNull();
    }

    #endregion

    #region Manual Creation Modal Trigger Tests

    [Fact]
    public void Transactions_AddTransactionButton_RendersOnPage()
    {
        // Arrange & Act
        var cut = RenderComponent<Transactions>();

        // Assert
        var addButton = FindAddTransactionButton(cut);
        addButton.Should().NotBeNull("'+ Add Transaction' button must be rendered on the transactions page");
        addButton!.TextContent.Should().Contain("Add Transaction");
    }

    [Fact]
    public void Transactions_ClickingAddTransactionButton_OpensCreationModal()
    {
        // Arrange
        var cut = RenderComponent<Transactions>();
        var addButton = FindAddTransactionButton(cut);
        addButton.Should().NotBeNull("'+ Add Transaction' button must be rendered on the transactions page");

        // Act
        addButton!.Click();

        // Assert
        var modalOrDialog = cut.FindAll(".modal, [role='dialog'], form");
        modalOrDialog.Should().NotBeEmpty("clicking '+ Add Transaction' button must open the creation modal dialog");
        cut.Markup.Should().Match(m =>
            m.Contains("modal") ||
            m.Contains("Add Transaction") ||
            m.Contains("Create") ||
            m.Contains("Save"));
    }

    #endregion

    #region Checkbox Selection & Toggle Tests

    [Fact]
    public void Transactions_RowCheckbox_TogglesSelectionStateForSingleTransaction()
    {
        // Arrange
        var tx1 = CreateSampleTransaction("Grocery 1", 25.00m, TransactionType.Expense);
        var tx2 = CreateSampleTransaction("Grocery 2", 35.00m, TransactionType.Expense);

        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([tx1, tx2]));

        var cut = RenderComponent<Transactions>();
        var rowCheckboxes = cut.FindAll("tbody input[type='checkbox']");
        rowCheckboxes.Should().HaveCount(2);

        // Act 1: Check first row
        rowCheckboxes[0].Change(true);

        // Assert 1: First row is checked, second row is unchecked
        var checkboxesAfterCheck = cut.FindAll("tbody input[type='checkbox']");
        checkboxesAfterCheck[0].HasAttribute("checked").Should().BeTrue("first row checkbox should be checked");
        checkboxesAfterCheck[1].HasAttribute("checked").Should().BeFalse("second row checkbox should remain unchecked");

        // Act 2: Uncheck first row
        checkboxesAfterCheck[0].Change(false);

        // Assert 2: Both rows unchecked
        var checkboxesAfterUncheck = cut.FindAll("tbody input[type='checkbox']");
        checkboxesAfterUncheck[0].HasAttribute("checked").Should().BeFalse("first row checkbox should now be unchecked");
    }

    [Fact]
    public void Transactions_HeaderSelectAllCheckbox_TogglesSelectionStateAcrossAllRows()
    {
        // Arrange
        var tx1 = CreateSampleTransaction("Item 1", 10.00m, TransactionType.Expense);
        var tx2 = CreateSampleTransaction("Item 2", 20.00m, TransactionType.Expense);
        var tx3 = CreateSampleTransaction("Item 3", 30.00m, TransactionType.Expense);

        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([tx1, tx2, tx3]));

        var cut = RenderComponent<Transactions>();
        var headerCheckbox = cut.Find("thead input[type='checkbox']");

        // Act 1: Check header "Select All"
        headerCheckbox.Change(true);

        // Assert 1: All row checkboxes are checked
        var rowCheckboxesAfterSelectAll = cut.FindAll("tbody input[type='checkbox']");
        rowCheckboxesAfterSelectAll.Should().HaveCount(3);
        rowCheckboxesAfterSelectAll.All(cb => cb.HasAttribute("checked")).Should().BeTrue("all rows must be checked when header checkbox is checked");
        cut.Find("thead input[type='checkbox']").HasAttribute("checked").Should().BeTrue("header checkbox must be checked");

        // Act 2: Uncheck header "Select All"
        cut.Find("thead input[type='checkbox']").Change(false);

        // Assert 2: All row checkboxes are unchecked
        var rowCheckboxesAfterDeselectAll = cut.FindAll("tbody input[type='checkbox']");
        rowCheckboxesAfterDeselectAll.All(cb => cb.HasAttribute("checked")).Should().BeFalse("all rows must be unchecked when header checkbox is unchecked");
    }

    [Fact]
    public void Transactions_WhenSelectingAllRowsIndividually_HeaderCheckboxBecomesChecked()
    {
        // Arrange
        var tx1 = CreateSampleTransaction("Item 1", 10.00m, TransactionType.Expense);
        var tx2 = CreateSampleTransaction("Item 2", 20.00m, TransactionType.Expense);

        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([tx1, tx2]));

        var cut = RenderComponent<Transactions>();

        // Act 1: Check first row
        cut.FindAll("tbody input[type='checkbox']")[0].Change(true);

        // Assert 1: Header checkbox should not be checked yet (partial selection)
        cut.Find("thead input[type='checkbox']").HasAttribute("checked").Should().BeFalse("header should not be checked when partial rows are selected");

        // Act 2: Check second row
        cut.FindAll("tbody input[type='checkbox']")[1].Change(true);

        // Assert 2: Header checkbox should now be checked (all rows selected)
        cut.Find("thead input[type='checkbox']").HasAttribute("checked").Should().BeTrue("header must be checked when all rows are selected");
    }

    #endregion

    #region Bulk Delete Button Visibility & Count Tests

    [Fact]
    public void Transactions_DeleteSelectedButton_AppearsAndDisplaysCountWhenRowsSelected()
    {
        // Arrange
        var tx1 = CreateSampleTransaction("Item 1", 15.00m, TransactionType.Expense);
        var tx2 = CreateSampleTransaction("Item 2", 25.00m, TransactionType.Expense);
        var tx3 = CreateSampleTransaction("Item 3", 35.00m, TransactionType.Expense);

        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([tx1, tx2, tx3]));

        var cut = RenderComponent<Transactions>();

        // Initial state: no selection -> no Delete Selected button
        FindBulkDeleteButton(cut).Should().BeNull("Delete Selected button should not appear when no rows are selected");

        // Act 1: Select 1 row
        cut.FindAll("tbody input[type='checkbox']")[0].Change(true);

        // Assert 1: Button appears with count 1
        var bulkDeleteBtn1 = FindBulkDeleteButton(cut);
        bulkDeleteBtn1.Should().NotBeNull("Delete Selected button must appear when 1 row is selected");
        bulkDeleteBtn1!.TextContent.Should().Contain("1", "button text must show selected count 1");

        // Act 2: Select 2nd row
        cut.FindAll("tbody input[type='checkbox']")[1].Change(true);

        // Assert 2: Button count updates to 2
        var bulkDeleteBtn2 = FindBulkDeleteButton(cut);
        bulkDeleteBtn2.Should().NotBeNull("Delete Selected button must remain visible when multiple rows selected");
        bulkDeleteBtn2!.TextContent.Should().Contain("2", "button text must show updated count 2");
    }

    [Fact]
    public void Transactions_DeleteSelectedButton_NotDisplayedWhenNoRowsSelected()
    {
        // Arrange
        var tx1 = CreateSampleTransaction("Item 1", 10.00m, TransactionType.Expense);

        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([tx1]));

        var cut = RenderComponent<Transactions>();

        // Assert: Initially null
        FindBulkDeleteButton(cut).Should().BeNull();

        // Select then deselect
        cut.FindAll("tbody input[type='checkbox']")[0].Change(true);
        FindBulkDeleteButton(cut).Should().NotBeNull();

        cut.FindAll("tbody input[type='checkbox']")[0].Change(false);
        FindBulkDeleteButton(cut).Should().BeNull("Delete Selected button must disappear when selection is cleared");
    }

    #endregion

    #region Bulk Delete Execution & Grid Updates

    [Fact]
    public void Transactions_ConfirmingBulkDelete_DispatchesDeleteTransactionsBulkAsyncWithSelectedIds()
    {
        // Arrange
        var tx1 = CreateSampleTransaction("Item 1", 10.00m, TransactionType.Expense);
        var tx2 = CreateSampleTransaction("Item 2", 20.00m, TransactionType.Expense);
        var tx3 = CreateSampleTransaction("Item 3", 30.00m, TransactionType.Expense);

        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([tx1, tx2, tx3]));

        MockTransactionService.DeleteTransactionsBulkAsync(DefaultUserId, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(2));

        var cut = RenderComponent<Transactions>();

        // Act: Select first and second rows
        cut.FindAll("tbody input[type='checkbox']")[0].Change(true);
        cut.FindAll("tbody input[type='checkbox']")[1].Change(true);

        // Click "Delete Selected"
        var bulkDeleteBtn = FindBulkDeleteButton(cut);
        bulkDeleteBtn.Should().NotBeNull("Delete Selected button must be visible");
        bulkDeleteBtn!.Click();

        // Confirm dialog if shown
        ConfirmDialogIfPresent(cut);

        // Assert: Service invoked with tx1.Id and tx2.Id
        MockTransactionService.Received(1).DeleteTransactionsBulkAsync(
            DefaultUserId,
            Arg.Is<IReadOnlyList<Guid>>(ids =>
                ids.Count == 2 &&
                ids.Contains(tx1.Id) &&
                ids.Contains(tx2.Id)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Transactions_SuccessfulBulkDelete_RemovesTransactionsFromGridAndClearsSelection()
    {
        // Arrange
        var tx1 = CreateSampleTransaction("Bulk Delete 1", 50.00m, TransactionType.Expense);
        var tx2 = CreateSampleTransaction("Bulk Delete 2", 70.00m, TransactionType.Expense);
        var txToKeep = CreateSampleTransaction("Keep This Item", 120.00m, TransactionType.Expense);

        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([tx1, tx2, txToKeep]));

        MockTransactionService.DeleteTransactionsBulkAsync(DefaultUserId, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Success(2));

        var cut = RenderComponent<Transactions>();
        cut.FindAll("tbody tr").Should().HaveCount(3);

        // Act: Select tx1 and tx2 and bulk delete
        cut.FindAll("tbody input[type='checkbox']")[0].Change(true);
        cut.FindAll("tbody input[type='checkbox']")[1].Change(true);

        var bulkDeleteBtn = FindBulkDeleteButton(cut);
        bulkDeleteBtn.Should().NotBeNull();
        bulkDeleteBtn!.Click();

        ConfirmDialogIfPresent(cut);

        // Assert: tx1 and tx2 removed, txToKeep retained
        var remainingRows = cut.FindAll("tbody tr");
        remainingRows.Should().HaveCount(1, "only 1 transaction should remain after deleting 2");
        cut.Markup.Should().NotContain("Bulk Delete 1");
        cut.Markup.Should().NotContain("Bulk Delete 2");
        cut.Markup.Should().Contain("Keep This Item");

        // Selection must be cleared
        FindBulkDeleteButton(cut).Should().BeNull("Delete Selected button should disappear after bulk delete");
        cut.Find("thead input[type='checkbox']").HasAttribute("checked").Should().BeFalse("header checkbox should be unchecked");
    }

    [Fact]
    public void Transactions_WhenBulkDeleteFails_DisplaysErrorMessageAndRetainsTransactionsInGrid()
    {
        // Arrange
        var tx1 = CreateSampleTransaction("Failed Item 1", 20.00m, TransactionType.Expense);
        var tx2 = CreateSampleTransaction("Failed Item 2", 40.00m, TransactionType.Expense);

        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([tx1, tx2]));

        MockTransactionService.DeleteTransactionsBulkAsync(DefaultUserId, Arg.Any<IReadOnlyList<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(Result<int>.Failure("Bulk deletion failed due to database timeout."));

        var cut = RenderComponent<Transactions>();

        // Act: Select rows and click Delete Selected
        cut.FindAll("tbody input[type='checkbox']")[0].Change(true);
        cut.FindAll("tbody input[type='checkbox']")[1].Change(true);

        var bulkDeleteBtn = FindBulkDeleteButton(cut);
        bulkDeleteBtn.Should().NotBeNull();
        bulkDeleteBtn!.Click();

        ConfirmDialogIfPresent(cut);

        // Assert: Error message displayed
        var alertBanners = cut.FindAll(".alert-danger, [role='alert'], .alert");
        alertBanners.Should().NotBeEmpty("an error alert must be rendered when bulk delete fails");
        cut.Markup.Should().Contain("Bulk deletion failed due to database timeout.");

        // Grid retains both transactions
        var remainingRows = cut.FindAll("tbody tr");
        remainingRows.Should().HaveCount(2, "transactions should remain in the grid when bulk deletion fails");
        cut.Markup.Should().Contain("Failed Item 1");
        cut.Markup.Should().Contain("Failed Item 2");
    }

    #endregion

    #region Helper Methods

    private static TransactionDto CreateSampleTransaction(string description, decimal amount, TransactionType type) =>
        new(
            Id: Guid.NewGuid(),
            Date: new DateOnly(2026, 10, 12),
            Amount: amount,
            Description: description,
            Type: type,
            CategoryId: null,
            CategoryName: null,
            SubcategoryId: null,
            SubcategoryName: null,
            Notes: null,
            CreatedAtUtc: DateTime.UtcNow
        );

    private static IElement? FindBulkDeleteButton(IRenderedComponent<Transactions> cut) =>
        cut.FindAll("button")
            .FirstOrDefault(b => b.TextContent.Contains("Delete Selected", StringComparison.OrdinalIgnoreCase) ||
                                b.GetAttribute("title")?.Contains("Delete Selected", StringComparison.OrdinalIgnoreCase) == true);

    private static IElement? FindAddTransactionButton(IRenderedComponent<Transactions> cut) =>
        cut.FindAll("button")
            .FirstOrDefault(b => b.TextContent.Contains("Add Transaction", StringComparison.OrdinalIgnoreCase) ||
                                b.GetAttribute("title")?.Contains("Add Transaction", StringComparison.OrdinalIgnoreCase) == true);

    private static void ConfirmDialogIfPresent(IRenderedComponent<Transactions> cut)
    {
        var confirmButton = cut.FindAll(".modal button, [role='dialog'] button, .confirm-dialog button")
            .FirstOrDefault(b => (b.TextContent.Trim() is "Confirm" or "Delete" or "Yes" ||
                                 b.TextContent.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
                                 b.TextContent.Contains("Confirm", StringComparison.OrdinalIgnoreCase)) &&
                                !b.TextContent.Contains("Delete Selected", StringComparison.OrdinalIgnoreCase));
        confirmButton?.Click();
    }

    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, serviceProvider: null, items: null);
        Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true);
        return validationResults;
    }

    #endregion
}
