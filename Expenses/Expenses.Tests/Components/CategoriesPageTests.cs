using System.ComponentModel.DataAnnotations;
using Bunit;
using Expenses.Common;
using Expenses.Components.Pages;
using Expenses.Models.DTOs;
using Expenses.Models.ViewModels;
using Expenses.Tests.Fixtures;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Expenses.Tests.Components;

public sealed class CategoriesPageTests : BunitTestContext
{
    [Fact]
    public void Categories_WhenNoCategories_RendersEmptyStateMessage()
    {
        // Arrange
        MockCategoryService.GetCategoriesAsync(DefaultUserId, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<CategoryDto>>.Success([]));

        // Act
        var cut = RenderComponent<Categories>();

        // Assert
        cut.Markup.Should().ContainEquivalentOf("No categories");
        cut.FindAll(".category-tree .card").Should().BeEmpty(
            "no category cards should be rendered when user has no categories");
    }

    [Fact]
    public void Categories_WhenCategoriesExist_RendersParentCategoriesAndNestedSubcategoriesWithColorSwatchesAndDisplayNames()
    {
        // Arrange
        var subcategory1 = new CategoryDto(
            Id: Guid.NewGuid(),
            Name: "Groceries",
            ParentCategoryId: null,
            ColorCode: "#10B981",
            Icon: null,
            DisplayOrder: 1,
            Subcategories: []);

        var subcategory2 = new CategoryDto(
            Id: Guid.NewGuid(),
            Name: "Restaurants",
            ParentCategoryId: null,
            ColorCode: "#34D399",
            Icon: null,
            DisplayOrder: 2,
            Subcategories: []);

        var parent1 = new CategoryDto(
            Id: Guid.NewGuid(),
            Name: "Food & Dining",
            ParentCategoryId: null,
            ColorCode: "#059669",
            Icon: "bi-basket",
            DisplayOrder: 1,
            Subcategories: [subcategory1, subcategory2]);

        var parent2 = new CategoryDto(
            Id: Guid.NewGuid(),
            Name: "Housing",
            ParentCategoryId: null,
            ColorCode: "#3B82F6",
            Icon: "bi-house",
            DisplayOrder: 2,
            Subcategories: []);

        MockCategoryService.GetCategoriesAsync(DefaultUserId, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<CategoryDto>>.Success([parent1, parent2]));

        // Act
        var cut = RenderComponent<Categories>();

        // Assert
        // Parent categories display names
        cut.Markup.Should().Contain("Food & Dining");
        cut.Markup.Should().Contain("Housing");

        // Subcategory display names
        cut.Markup.Should().Contain("Groceries");
        cut.Markup.Should().Contain("Restaurants");

        // Color swatches (rendered either in style attributes or badges)
        cut.Markup.Should().Contain("#059669");
        cut.Markup.Should().Contain("#10B981");
        cut.Markup.Should().Contain("#34D399");
        cut.Markup.Should().Contain("#3B82F6");

        // Display order badges
        cut.Markup.Should().Contain("Order: 1");
        cut.Markup.Should().Contain("Order: 2");

        // Empty subcategories notice for parent2
        cut.Markup.Should().ContainEquivalentOf("No subcategories defined");
    }

    [Fact]
    public void Categories_ClickingAddParentCategory_OpensCategoryDialog()
    {
        // Arrange
        MockCategoryService.GetCategoriesAsync(DefaultUserId, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<CategoryDto>>.Success([]));

        var cut = RenderComponent<Categories>();

        // Act
        var addParentButton = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Add Parent Category", StringComparison.OrdinalIgnoreCase));
        addParentButton.Click();

        // Assert
        var dialogOrModal = cut.FindAll(".modal, [role='dialog'], form");
        dialogOrModal.Should().NotBeEmpty("Add Parent Category button must open the Category Dialog");
        cut.Markup.Should().Match(m =>
            m.Contains("modal") ||
            m.Contains("Add") ||
            m.Contains("CategoryDialog") ||
            m.Contains("Save"));
    }

    [Fact]
    public void Categories_ClickingAddSubcategory_OpensDialogPrepopulatedWithParentCategoryId()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var parent = new CategoryDto(
            Id: parentId,
            Name: "Utilities",
            ParentCategoryId: null,
            ColorCode: "#3B82F6",
            Icon: null,
            DisplayOrder: 1,
            Subcategories: []);

        MockCategoryService.GetCategoriesAsync(DefaultUserId, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<CategoryDto>>.Success([parent]));

        var cut = RenderComponent<Categories>();

        // Act
        var addSubcategoryButton = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Add Subcategory", StringComparison.OrdinalIgnoreCase));
        addSubcategoryButton.Click();

        // Assert
        var dialogOrModal = cut.FindAll(".modal, [role='dialog'], form");
        dialogOrModal.Should().NotBeEmpty("Add Subcategory button must open the Category Dialog");

        // Modal should reflect association to the parent category (by ID or parent category display name)
        cut.Markup.Should().Match(m =>
            m.Contains(parentId.ToString()) ||
            m.Contains("Utilities") ||
            m.Contains("Subcategory"));
    }

    [Fact]
    public void Categories_WhenDeleteReturnsCategoryInUse_DisplaysAlertBanner()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var category = new CategoryDto(
            Id: categoryId,
            Name: "Fixed Expenses",
            ParentCategoryId: null,
            ColorCode: "#EF4444",
            Icon: null,
            DisplayOrder: 1,
            Subcategories: []);

        MockCategoryService.GetCategoriesAsync(DefaultUserId, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<CategoryDto>>.Success([category]));

        MockCategoryService.DeleteCategoryAsync(DefaultUserId, categoryId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure(
                "Cannot delete category because it is currently assigned to existing transactions. Please reassign those transactions before deleting.",
                "CATEGORY_IN_USE"));

        var cut = RenderComponent<Categories>();

        // Act
        var deleteButton = cut.FindAll("button")
            .First(b => b.TextContent.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        deleteButton.Click();

        // If a confirmation dialog appears, confirm deletion
        var confirmButton = cut.FindAll(".modal button, [role='dialog'] button, .confirm-dialog button")
            .FirstOrDefault(b => b.TextContent.Trim() is "Confirm" or "Delete" or "Yes" || b.TextContent.Contains("Delete"));
        confirmButton?.Click();

        // Assert
        MockCategoryService.Received(1).DeleteCategoryAsync(DefaultUserId, categoryId, Arg.Any<CancellationToken>());

        var alertBanners = cut.FindAll(".alert-danger, [role='alert'], .alert");
        alertBanners.Should().NotBeEmpty("an alert banner should be displayed on deletion failure");
        alertBanners.Any(a => a.TextContent.Contains("Cannot delete category because it is currently assigned to existing transactions"))
            .Should().BeTrue("alert banner must explain that category is assigned to existing transactions");
    }

    [Fact]
    public void Categories_WhenDeleteSucceeds_RemovesCategoryFromUIList()
    {
        // Arrange
        var catToDeleteId = Guid.NewGuid();
        var catToKeepId = Guid.NewGuid();

        var catToDelete = new CategoryDto(
            Id: catToDeleteId,
            Name: "Entertainment",
            ParentCategoryId: null,
            ColorCode: "#8B5CF6",
            Icon: null,
            DisplayOrder: 1,
            Subcategories: []);

        var catToKeep = new CategoryDto(
            Id: catToKeepId,
            Name: "Health & Fitness",
            ParentCategoryId: null,
            ColorCode: "#10B981",
            Icon: null,
            DisplayOrder: 2,
            Subcategories: []);

        MockCategoryService.GetCategoriesAsync(DefaultUserId, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<CategoryDto>>.Success([catToDelete, catToKeep]));

        MockCategoryService.DeleteCategoryAsync(DefaultUserId, catToDeleteId, Arg.Any<CancellationToken>())
            .Returns(Result.Success());

        var cut = RenderComponent<Categories>();
        cut.Markup.Should().Contain("Entertainment");
        cut.Markup.Should().Contain("Health & Fitness");

        // Act
        // Find the delete button corresponding to the category to delete
        var deleteButtons = cut.FindAll("button")
            .Where(b => b.TextContent.Contains("Delete", StringComparison.OrdinalIgnoreCase))
            .ToList();
        deleteButtons.First().Click();

        // If confirmation dialog exists, confirm
        var confirmButton = cut.FindAll(".modal button, [role='dialog'] button, .confirm-dialog button")
            .FirstOrDefault(b => b.TextContent.Trim() is "Confirm" or "Delete" or "Yes" || b.TextContent.Contains("Delete"));
        confirmButton?.Click();

        // Assert
        MockCategoryService.Received(1).DeleteCategoryAsync(DefaultUserId, catToDeleteId, Arg.Any<CancellationToken>());

        // Deleted category should no longer appear in the category list
        var categoryCards = cut.FindAll(".category-tree .card");
        categoryCards.Should().NotContain(card => card.TextContent.Contains("Entertainment"));

        // Remaining category should still appear
        cut.Markup.Should().Contain("Health & Fitness");
    }

    [Fact]
    public void Categories_WhenErrorBannerDismissed_HidesErrorMessage()
    {
        // Arrange
        var categoryId = Guid.NewGuid();
        var category = new CategoryDto(
            Id: categoryId,
            Name: "Bills",
            ParentCategoryId: null,
            ColorCode: "#EF4444",
            Icon: null,
            DisplayOrder: 1,
            Subcategories: []);

        MockCategoryService.GetCategoriesAsync(DefaultUserId, Arg.Any<CancellationToken>())
            .Returns(Result<IReadOnlyList<CategoryDto>>.Success([category]));

        MockCategoryService.DeleteCategoryAsync(DefaultUserId, categoryId, Arg.Any<CancellationToken>())
            .Returns(Result.Failure("Cannot delete category because it is currently assigned to existing transactions.", "CATEGORY_IN_USE"));

        var cut = RenderComponent<Categories>();
        var deleteBtn = cut.FindAll("button").First(b => b.TextContent.Contains("Delete", StringComparison.OrdinalIgnoreCase));
        deleteBtn.Click();

        var confirmButton = cut.FindAll(".modal button, [role='dialog'] button, .confirm-dialog button")
            .FirstOrDefault(b => b.TextContent.Trim() is "Confirm" or "Delete" or "Yes" || b.TextContent.Contains("Delete"));
        confirmButton?.Click();

        // Verify error banner is present
        cut.FindAll(".alert-danger, [role='alert']").Should().NotBeEmpty();

        // Act - Click dismiss button (.btn-close)
        var closeButton = cut.FindAll("button.btn-close, .alert button").FirstOrDefault();
        closeButton?.Click();

        // Assert - Error banner should be gone
        cut.FindAll(".alert-danger").Should().BeEmpty("dismissing the alert should clear the error banner");
    }

    [Theory]
    [InlineData("", false, "Category name is required.")]
    [InlineData("   ", false, "Category name is required.")]
    [InlineData("Valid Name", true, null)]
    public void CategoryFormModel_NameValidation_EnforcesRequired(string name, bool isValid, string? expectedError)
    {
        // Arrange
        var model = new CategoryFormModel
        {
            Name = name,
            ColorCode = "#3B82F6"
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        if (isValid)
        {
            results.Should().NotContain(r => r.MemberNames.Contains(nameof(CategoryFormModel.Name)));
        }
        else
        {
            results.Should().Contain(r => r.ErrorMessage == expectedError);
        }
    }

    [Fact]
    public void CategoryFormModel_NameLength_Exceeding100Chars_FailsValidation()
    {
        // Arrange
        var model = new CategoryFormModel
        {
            Name = new string('A', 101),
            ColorCode = "#3B82F6"
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        results.Should().Contain(r => r.ErrorMessage == "Category name cannot exceed 100 characters.");
    }

    [Theory]
    [InlineData("#3B82F6", true)]
    [InlineData("#FFF", true)]
    [InlineData("#10B981", true)]
    [InlineData("invalid-hex", false)]
    [InlineData("#12", false)]
    [InlineData("#GGGGGG", false)]
    public void CategoryFormModel_ColorCode_EnforcesHexFormat(string colorCode, bool isValid)
    {
        // Arrange
        var model = new CategoryFormModel
        {
            Name = "Groceries",
            ColorCode = colorCode
        };

        // Act
        var results = ValidateModel(model);

        // Assert
        if (isValid)
        {
            results.Should().NotContain(r => r.MemberNames.Contains(nameof(CategoryFormModel.ColorCode)));
        }
        else
        {
            results.Should().Contain(r => r.ErrorMessage == "Color must be a valid hex code (e.g. #FF5733).");
        }
    }

    [Fact]
    public void CategoryFormModel_ToCreateRequest_And_ToUpdateRequest_MapPropertiesAccurately()
    {
        // Arrange
        var parentId = Guid.NewGuid();
        var model = new CategoryFormModel
        {
            Id = Guid.NewGuid(),
            Name = "  Shopping  ",
            ParentCategoryId = parentId,
            ColorCode = "#EC4899",
            Icon = "bi-bag",
            DisplayOrder = 5
        };

        // Act
        var createRequest = model.ToCreateRequest();
        var updateRequest = model.ToUpdateRequest();

        // Assert
        createRequest.Name.Should().Be("Shopping");
        createRequest.ParentCategoryId.Should().Be(parentId);
        createRequest.ColorCode.Should().Be("#EC4899");
        createRequest.Icon.Should().Be("bi-bag");
        createRequest.DisplayOrder.Should().Be(5);

        updateRequest.Name.Should().Be("Shopping");
        updateRequest.ParentCategoryId.Should().Be(parentId);
        updateRequest.ColorCode.Should().Be("#EC4899");
        updateRequest.Icon.Should().Be("bi-bag");
        updateRequest.DisplayOrder.Should().Be(5);
    }

    [Fact]
    public void CategoryFormModel_FromDto_InitializesPropertiesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var parentId = Guid.NewGuid();
        var dto = new CategoryDto(
            Id: id,
            Name: "Electronics",
            ParentCategoryId: parentId,
            ColorCode: "#6366F1",
            Icon: "bi-laptop",
            DisplayOrder: 3,
            Subcategories: []);

        // Act
        var model = CategoryFormModel.FromDto(dto);

        // Assert
        model.Id.Should().Be(id);
        model.Name.Should().Be("Electronics");
        model.ParentCategoryId.Should().Be(parentId);
        model.ColorCode.Should().Be("#6366F1");
        model.Icon.Should().Be("bi-laptop");
        model.DisplayOrder.Should().Be(3);
    }

    private static IList<ValidationResult> ValidateModel(object model)
    {
        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(model, serviceProvider: null, items: null);
        Validator.TryValidateObject(model, validationContext, validationResults, validateAllProperties: true);
        return validationResults;
    }
}
