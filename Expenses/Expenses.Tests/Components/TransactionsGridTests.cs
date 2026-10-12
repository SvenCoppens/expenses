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

public sealed class TransactionsGridTests : BunitTestContext
{
    public TransactionsGridTests()
    {
        MockCategoryService.GetCategoriesAsync(DefaultUserId, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<CategoryDto>>.Success([]));

        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([]));
    }

    #region TransactionRowEditModel Validation & Mapping Unit Tests

    [Theory]
    [InlineData(0, false, "Amount must be greater than zero.")]
    [InlineData(-10.50, false, "Amount must be greater than zero.")]
    [InlineData(-0.01, false, "Amount must be greater than zero.")]
    [InlineData(0.01, true, null)]
    [InlineData(45.50, true, null)]
    [InlineData(1000000.00, true, null)]
    public void TransactionRowEditModel_AmountValidation_EnforcesGreaterThanZero(double amountDouble, bool isValid, string? expectedError)
    {
        // Arrange
        var amount = (decimal)amountDouble;
        var model = new TransactionRowEditModel
        {
            Date = new DateOnly(2026, 10, 12),
            Amount = amount,
            Description = "Groceries",
            Type = TransactionType.Expense
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        if (isValid)
        {
            results.Should().NotContain(r => r.MemberNames.Contains(nameof(TransactionRowEditModel.Amount)));
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
    public void TransactionRowEditModel_DescriptionValidation_EnforcesRequired(string description, bool isValid, string? expectedError)
    {
        // Arrange
        var model = new TransactionRowEditModel
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
            results.Should().NotContain(r => r.MemberNames.Contains(nameof(TransactionRowEditModel.Description)));
        }
        else
        {
            results.Should().Contain(r => r.ErrorMessage == expectedError);
        }
    }

    [Fact]
    public void TransactionRowEditModel_DescriptionLength_Exceeding250Chars_FailsValidation()
    {
        // Arrange
        var model = new TransactionRowEditModel
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
    public void TransactionRowEditModel_NotesLength_Exceeding1000Chars_FailsValidation()
    {
        // Arrange
        var model = new TransactionRowEditModel
        {
            Date = new DateOnly(2026, 10, 12),
            Amount = 25.00m,
            Description = "Dinner with friends",
            Type = TransactionType.Expense,
            Notes = new string('N', 1001)
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        results.Should().Contain(r => r.ErrorMessage == "Notes cannot exceed 1000 characters.");
    }

    [Fact]
    public void TransactionRowEditModel_FromDto_MapsAllPropertiesAccurately()
    {
        // Arrange
        var id = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var subcategoryId = Guid.NewGuid();
        var dto = new TransactionDto(
            Id: id,
            Date: new DateOnly(2026, 10, 12),
            Amount: 145.75m,
            Description: "Train tickets to Brussels",
            Type: TransactionType.Expense,
            CategoryId: categoryId,
            CategoryName: "Transportation",
            SubcategoryId: subcategoryId,
            SubcategoryName: "Trains",
            Notes: "Eurostar booked online",
            CreatedAtUtc: DateTime.UtcNow
        );

        // Act
        var model = TransactionRowEditModel.FromDto(dto);

        // Assert
        model.Id.Should().Be(id);
        model.Date.Should().Be(new DateOnly(2026, 10, 12));
        model.Amount.Should().Be(145.75m);
        model.Description.Should().Be("Train tickets to Brussels");
        model.Type.Should().Be(TransactionType.Expense);
        model.CategoryId.Should().Be(categoryId);
        model.SubcategoryId.Should().Be(subcategoryId);
        model.Notes.Should().Be("Eurostar booked online");
    }

    [Fact]
    public void TransactionRowEditModel_ToRequest_MapsAllPropertiesAccuratelyAndTrimsStrings()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var subcategoryId = Guid.NewGuid();
        var model = new TransactionRowEditModel
        {
            Id = Guid.NewGuid(),
            Date = new DateOnly(2026, 10, 15),
            Amount = 89.90m,
            Description = "  Consulting Fee  ",
            Type = TransactionType.Income,
            CategoryId = categoryId,
            SubcategoryId = subcategoryId,
            Notes = "  Invoice #2026-101  "
        };

        // Act
        var request = model.ToRequest();

        // Assert
        request.Date.Should().Be(new DateOnly(2026, 10, 15));
        request.Amount.Should().Be(89.90m);
        request.Description.Should().Be("Consulting Fee");
        request.Type.Should().Be(TransactionType.Income);
        request.CategoryId.Should().Be(categoryId);
        request.SubcategoryId.Should().Be(subcategoryId);
        request.Notes.Should().Be("Invoice #2026-101");
    }

    #endregion

    #region Transactions Grid Display & Row Rendering Tests

    [Fact]
    public void Transactions_RendersTransactionRowsWithDateDescriptionCategoryBadgesTypeBadgeAndFormattedEurAmount()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var subcategoryId = Guid.NewGuid();

        var category = new CategoryDto(
            Id: categoryId,
            Name: "Food & Dining",
            ParentCategoryId: null,
            ColorCode: "#10B981",
            Icon: "bi-basket",
            DisplayOrder: 1,
            Subcategories: [
                new CategoryDto(subcategoryId, "Groceries", categoryId, "#34D399", null, 1, [])
            ]
        );

        var expenseTx = new TransactionDto(
            Id: Guid.NewGuid(),
            Date: new DateOnly(2026, 10, 10),
            Amount: 45.50m,
            Description: "Weekly Supermarket",
            Type: TransactionType.Expense,
            CategoryId: categoryId,
            CategoryName: "Food & Dining",
            SubcategoryId: subcategoryId,
            SubcategoryName: "Groceries",
            Notes: "Bought organic produce",
            CreatedAtUtc: DateTime.UtcNow
        );

        var incomeTx = new TransactionDto(
            Id: Guid.NewGuid(),
            Date: new DateOnly(2026, 10, 1),
            Amount: 3200.00m,
            Description: "Monthly Salary",
            Type: TransactionType.Income,
            CategoryId: null,
            CategoryName: null,
            SubcategoryId: null,
            SubcategoryName: null,
            Notes: null,
            CreatedAtUtc: DateTime.UtcNow
        );

        var transferTx = new TransactionDto(
            Id: Guid.NewGuid(),
            Date: new DateOnly(2026, 10, 5),
            Amount: 150.00m,
            Description: "Transfer to Savings",
            Type: TransactionType.Transfer,
            CategoryId: null,
            CategoryName: null,
            SubcategoryId: null,
            SubcategoryName: null,
            Notes: null,
            CreatedAtUtc: DateTime.UtcNow
        );

        MockCategoryService.GetCategoriesAsync(DefaultUserId, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<CategoryDto>>.Success([category]));

        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([expenseTx, incomeTx, transferTx]));

        // Act
        var cut = RenderComponent<Transactions>();

        // Assert: Rows rendered
        var rows = cut.FindAll("tbody tr");
        rows.Should().HaveCount(3, "three transaction rows must be rendered in table body");

        // Assert: Dates rendered
        cut.Markup.Should().Contain("2026-10-10");
        cut.Markup.Should().Contain("2026-10-01");
        cut.Markup.Should().Contain("2026-10-05");

        // Assert: Descriptions rendered
        cut.Markup.Should().Contain("Weekly Supermarket");
        cut.Markup.Should().Contain("Monthly Salary");
        cut.Markup.Should().Contain("Transfer to Savings");

        // Assert: Category and Subcategory badges rendered
        var firstRow = rows[0];
        firstRow.TextContent.Should().Contain("Food & Dining");
        firstRow.TextContent.Should().Contain("Groceries");

        // Assert: Type badges rendered
        cut.Markup.Should().Contain("Expense");
        cut.Markup.Should().Contain("Income");
        cut.Markup.Should().Contain("Transfer");

        // Assert: Formatted EUR amounts rendered
        cut.Markup.Should().Match(m =>
            m.Contains("€45.50") || m.Contains("-€45.50") || m.Contains("45.50"),
            "Expense amount must be rendered in EUR format");
        cut.Markup.Should().Match(m =>
            m.Contains("€3,200.00") || m.Contains("+€3,200.00") || m.Contains("3,200.00"),
            "Income amount must be rendered in EUR format");
        cut.Markup.Should().Match(m =>
            m.Contains("€150.00") || m.Contains("150.00"),
            "Transfer amount must be rendered in EUR format");
    }

    #endregion

    #region Inline Row Editing Tests

    [Fact]
    public void Transactions_ClickingEditButton_ActivatesInlineEditingInputsForThatRow()
    {
        // Arrange
        var tx1 = CreateSampleTransaction("Bakeries", 12.50m, TransactionType.Expense);
        var tx2 = CreateSampleTransaction("Pharmacy", 34.00m, TransactionType.Expense);

        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([tx1, tx2]));

        var cut = RenderComponent<Transactions>();
        var rowsBeforeEdit = cut.FindAll("tbody tr");
        rowsBeforeEdit.Should().HaveCount(2);

        // Act: Click Edit button on the first row
        var editButton = FindRowEditButton(rowsBeforeEdit[0]);
        editButton.Click();

        // Assert: First row is transformed into editable state
        var rowsAfterEdit = cut.FindAll("tbody tr");
        var activeRow = rowsAfterEdit[0];

        // Should contain input controls for inline editing
        var dateInput = activeRow.QuerySelector("input[type='date']");
        dateInput.Should().NotBeNull("inline edit row must provide a date input");

        var textInputs = activeRow.QuerySelectorAll("input[type='text'], input:not([type])");
        textInputs.Should().NotBeEmpty("inline edit row must provide text input controls for description/notes");

        var amountInput = activeRow.QuerySelector("input[type='number'], input[step='0.01']");
        amountInput.Should().NotBeNull("inline edit row must provide an amount input");

        // Should render Save and Cancel action buttons
        var saveButton = FindRowSaveButton(activeRow);
        saveButton.Should().NotBeNull("inline edit row must provide a save button");

        var cancelButton = FindRowCancelButton(activeRow);
        cancelButton.Should().NotBeNull("inline edit row must provide a cancel button");

        // The second row must remain in view mode (not editing)
        var secondRowInputs = rowsAfterEdit[1].QuerySelectorAll("input[type='date'], input[type='number']");
        secondRowInputs.Should().BeEmpty("second row should remain in read-only mode");
    }

    [Fact]
    public void Transactions_InlineEditCancelButton_RestoresRowViewAndDiscardsChangesWithZeroServerUpdates()
    {
        // Arrange
        var originalTx = CreateSampleTransaction("Original Description", 50.00m, TransactionType.Expense);

        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([originalTx]));

        var cut = RenderComponent<Transactions>();
        var rows = cut.FindAll("tbody tr");

        // Act 1: Enter edit mode
        var editButton = FindRowEditButton(rows[0]);
        editButton.Click();

        // Act 2: Mutate input values
        var activeRow = cut.FindAll("tbody tr")[0];
        var descInput = activeRow.QuerySelectorAll("input[type='text'], input:not([type])").First();
        descInput.Change("Modified Discarded Description");

        var amountInput = activeRow.QuerySelector("input[type='number'], input[step='0.01']");
        amountInput?.Change("999.99");

        // Act 3: Click Cancel button
        var cancelButton = FindRowCancelButton(activeRow);
        cancelButton.Click();

        // Assert: Row exits edit mode and restores original values
        var rowsAfterCancel = cut.FindAll("tbody tr");
        rowsAfterCancel[0].QuerySelectorAll("input[type='date'], input[type='number']").Should().BeEmpty(
            "row should exit inline edit mode upon cancel");

        cut.Markup.Should().Contain("Original Description", "original description should be preserved");
        cut.Markup.Should().NotContain("Modified Discarded Description", "discarded modifications must not be displayed");
        cut.Markup.Should().Contain("50.00", "original amount should be preserved");

        // Zero service updates invoked
        MockTransactionService.DidNotReceiveWithAnyArgs()
            .UpdateTransactionAsync(default!, default, default!, default);
    }

    [Fact]
    public void Transactions_InlineEditSaveButton_InvokesUpdateTransactionAsyncWithUpdatedPayload()
    {
        // Arrange
        var txId = Guid.NewGuid();
        var originalTx = new TransactionDto(
            Id: txId,
            Date: new DateOnly(2026, 10, 10),
            Amount: 30.00m,
            Description: "Initial Supermarket",
            Type: TransactionType.Expense,
            CategoryId: null,
            CategoryName: null,
            SubcategoryId: null,
            SubcategoryName: null,
            Notes: null,
            CreatedAtUtc: DateTime.UtcNow
        );

        var updatedTx = originalTx with
        {
            Description = "Updated Organic Groceries",
            Amount = 42.50m,
            Notes = "Added loyalty discount"
        };

        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([originalTx]));

        MockTransactionService.UpdateTransactionAsync(DefaultUserId, txId, Arg.Any<UpdateTransactionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<TransactionDto>.Success(updatedTx));

        var cut = RenderComponent<Transactions>();
        var rows = cut.FindAll("tbody tr");

        // Act 1: Activate edit mode
        var editButton = FindRowEditButton(rows[0]);
        editButton.Click();

        var activeRow = cut.FindAll("tbody tr")[0];

        // Act 2: Update fields
        var textInputs = activeRow.QuerySelectorAll("input[type='text'], input:not([type])").ToList();
        var descInput = textInputs.First();
        descInput.Change("Updated Organic Groceries");

        var amountInput = activeRow.QuerySelector("input[type='number'], input[step='0.01']");
        amountInput?.Change("42.50");

        if (textInputs.Count > 1)
        {
            var notesInput = textInputs.Last();
            notesInput.Change("Added loyalty discount");
        }

        // Act 3: Click Save
        var saveButton = FindRowSaveButton(activeRow);
        saveButton.Click();

        // Assert: Service invoked with updated parameters
        MockTransactionService.Received(1).UpdateTransactionAsync(
            DefaultUserId,
            txId,
            Arg.Is<UpdateTransactionRequest>(r =>
                r.Description == "Updated Organic Groceries" &&
                r.Amount == 42.50m),
            Arg.Any<CancellationToken>()
        );

        // Row should exit edit mode and display new values
        var rowsAfterSave = cut.FindAll("tbody tr");
        rowsAfterSave[0].QuerySelectorAll("input[type='date'], input[type='number']").Should().BeEmpty(
            "row should exit inline edit mode after successful save");

        cut.Markup.Should().Contain("Updated Organic Groceries");
        cut.Markup.Should().Contain("42.50");
    }

    [Fact]
    public void Transactions_WhenUpdateTransactionFails_DisplaysErrorMessageAndRemainsInEditMode()
    {
        // Arrange
        var txId = Guid.NewGuid();
        var originalTx = new TransactionDto(
            Id: txId,
            Date: new DateOnly(2026, 10, 10),
            Amount: 30.00m,
            Description: "Initial Supermarket",
            Type: TransactionType.Expense,
            CategoryId: null,
            CategoryName: null,
            SubcategoryId: null,
            SubcategoryName: null,
            Notes: null,
            CreatedAtUtc: DateTime.UtcNow
        );

        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([originalTx]));

        MockTransactionService.UpdateTransactionAsync(DefaultUserId, txId, Arg.Any<UpdateTransactionRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result<TransactionDto>.Failure("Failed to save transaction due to server error."));

        var cut = RenderComponent<Transactions>();
        var rows = cut.FindAll("tbody tr");

        // Act: Enter edit mode and save
        var editButton = FindRowEditButton(rows[0]);
        editButton.Click();

        var activeRow = cut.FindAll("tbody tr")[0];
        var saveButton = FindRowSaveButton(activeRow);
        saveButton.Click();

        // Assert: Error message rendered
        var alertBanners = cut.FindAll(".alert-danger, [role='alert'], .alert");
        alertBanners.Should().NotBeEmpty("an error alert should be displayed when update fails");
        cut.Markup.Should().Contain("Failed to save transaction due to server error.");

        // Row remains in edit mode so user does not lose input
        var activeRowAfterFailure = cut.FindAll("tbody tr")[0];
        var saveButtonStillVisible = activeRowAfterFailure.QuerySelectorAll("button")
            .Any(b => b.TextContent.Trim() is "✓" or "Save" || b.ClassList.Contains("btn-success"));
        saveButtonStillVisible.Should().BeTrue("row should remain in edit mode upon failure");
    }

    #endregion

    #region Single Row Deletion Tests

    [Fact]
    public void Transactions_ClickingDeleteButton_CallsDeleteTransactionAsyncAndRemovesTransactionFromGrid()
    {
        // Arrange
        var txToDelete = CreateSampleTransaction("Transaction To Delete", 20.00m, TransactionType.Expense);
        var txToKeep = CreateSampleTransaction("Transaction To Keep", 80.00m, TransactionType.Expense);

        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([txToDelete, txToKeep]));

        MockTransactionService.DeleteTransactionAsync(DefaultUserId, txToDelete.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var cut = RenderComponent<Transactions>();
        var rows = cut.FindAll("tbody tr");
        rows.Should().HaveCount(2);

        // Act: Click delete button on the first transaction
        var deleteButton = FindRowDeleteButton(rows[0]);
        deleteButton.Click();

        // If confirmation dialog appears, confirm deletion
        ConfirmDialogIfPresent(cut);

        // Assert: Service invoked for target ID
        MockTransactionService.Received(1).DeleteTransactionAsync(
            DefaultUserId,
            txToDelete.Id,
            Arg.Any<CancellationToken>()
        );

        // Target transaction removed from grid, remaining transaction retained
        var remainingRows = cut.FindAll("tbody tr");
        remainingRows.Should().HaveCount(1, "deleted transaction should be removed from the grid");
        cut.Markup.Should().NotContain("Transaction To Delete");
        cut.Markup.Should().Contain("Transaction To Keep");
    }

    [Fact]
    public void Transactions_WhenDeleteTransactionFails_DisplaysErrorMessageAndRetainsRowInGrid()
    {
        // Arrange
        var tx = CreateSampleTransaction("Failed Delete Transaction", 45.00m, TransactionType.Expense);

        MockTransactionService.GetTransactionsAsync(DefaultUserId, Arg.Any<TransactionFilter>(), Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<TransactionDto>>.Success([tx]));

        MockTransactionService.DeleteTransactionAsync(DefaultUserId, tx.Id, Arg.Any<CancellationToken>())
            .Returns(Result.Failure("Unable to delete transaction record."));

        var cut = RenderComponent<Transactions>();
        var rows = cut.FindAll("tbody tr");

        // Act: Click delete button
        var deleteButton = FindRowDeleteButton(rows[0]);
        deleteButton.Click();

        ConfirmDialogIfPresent(cut);

        // Assert: Service invoked
        MockTransactionService.Received(1).DeleteTransactionAsync(
            DefaultUserId,
            tx.Id,
            Arg.Any<CancellationToken>()
        );

        // Error message displayed
        var alerts = cut.FindAll(".alert-danger, [role='alert'], .alert");
        alerts.Should().NotBeEmpty("an error alert must appear when deletion fails");
        cut.Markup.Should().Contain("Unable to delete transaction record.");

        // Row remains in grid
        var remainingRows = cut.FindAll("tbody tr");
        remainingRows.Should().HaveCount(1, "transaction row should remain in grid if delete failed");
        cut.Markup.Should().Contain("Failed Delete Transaction");
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

    private static IElement FindRowEditButton(IElement row) =>
        row.QuerySelectorAll("button")
            .FirstOrDefault(b => b.TextContent.Trim().Equals("Edit", StringComparison.OrdinalIgnoreCase) ||
                                b.TextContent.Contains("Edit", StringComparison.OrdinalIgnoreCase) ||
                                b.ClassList.Contains("btn-outline-primary") ||
                                b.GetAttribute("title")?.Contains("Edit", StringComparison.OrdinalIgnoreCase) == true)
        ?? throw new InvalidOperationException("Edit button not found in row.");

    private static IElement FindRowDeleteButton(IElement row) =>
        row.QuerySelectorAll("button")
            .FirstOrDefault(b => b.TextContent.Trim().Equals("Delete", StringComparison.OrdinalIgnoreCase) ||
                                b.TextContent.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
                                b.ClassList.Contains("btn-outline-danger") ||
                                b.GetAttribute("title")?.Contains("Delete", StringComparison.OrdinalIgnoreCase) == true)
        ?? throw new InvalidOperationException("Delete button not found in row.");

    private static IElement FindRowSaveButton(IElement row) =>
        row.QuerySelectorAll("button")
            .FirstOrDefault(b => b.TextContent.Trim() is "✓" or "Save" ||
                                b.TextContent.Contains("Save", StringComparison.OrdinalIgnoreCase) ||
                                b.ClassList.Contains("btn-success") ||
                                b.GetAttribute("title")?.Contains("Save", StringComparison.OrdinalIgnoreCase) == true)
        ?? throw new InvalidOperationException("Save button not found in editing row.");

    private static IElement FindRowCancelButton(IElement row) =>
        row.QuerySelectorAll("button")
            .FirstOrDefault(b => b.TextContent.Trim() is "✕" or "Cancel" ||
                                b.TextContent.Contains("Cancel", StringComparison.OrdinalIgnoreCase) ||
                                b.ClassList.Contains("btn-secondary") ||
                                b.GetAttribute("title")?.Contains("Cancel", StringComparison.OrdinalIgnoreCase) == true)
        ?? throw new InvalidOperationException("Cancel button not found in editing row.");

    private static void ConfirmDialogIfPresent(IRenderedComponent<Transactions> cut)
    {
        var confirmButton = cut.FindAll(".modal button, [role='dialog'] button, .confirm-dialog button")
            .FirstOrDefault(b => b.TextContent.Trim() is "Confirm" or "Delete" or "Yes" ||
                                b.TextContent.Contains("Delete", StringComparison.OrdinalIgnoreCase) ||
                                b.TextContent.Contains("Confirm", StringComparison.OrdinalIgnoreCase));
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
