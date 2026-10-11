using Expenses.Common;
using Expenses.Models.DTOs;
using Expenses.Models.Entities;
using Expenses.Models.Enums;
using Expenses.Services.Categories;
using Expenses.Tests.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Expenses.Tests.Services;

public sealed class CategoryServiceTests : IClassFixture<SqliteTestDbFixture>
{
    private readonly SqliteTestDbFixture _fixture;
    private readonly ILogger<CategoryService> _logger = NullLogger<CategoryService>.Instance;

    public CategoryServiceTests(SqliteTestDbFixture fixture)
    {
        _fixture = fixture;
    }

    private CategoryService CreateSut() => new(_fixture.Factory, _logger);

    private static string CreateUserId() => $"user_{Guid.NewGuid():N}";

    #region 1. Category Creation & Hierarchy Tests

    [Fact]
    public async Task CreateCategoryAsync_WithValidParentRequest_CreatesAndReturnsParentCategory()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var request = new CreateCategoryRequest(
            Name: "Food & Dining",
            ParentCategoryId: null,
            ColorCode: "#FF5733",
            Icon: "utensils",
            DisplayOrder: 1
        );

        // Act
        var result = await sut.CreateCategoryAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().NotBeEmpty();
        result.Value.Name.Should().Be("Food & Dining");
        result.Value.ParentCategoryId.Should().BeNull();
        result.Value.ColorCode.Should().Be("#FF5733");
        result.Value.Icon.Should().Be("utensils");
        result.Value.DisplayOrder.Should().Be(1);
        result.Value.Subcategories.Should().BeEmpty();

        await using var context = await _fixture.CreateContextAsync();
        var entity = await context.Categories.FirstOrDefaultAsync(c => c.Id == result.Value.Id);
        entity.Should().NotBeNull();
        entity!.UserId.Should().Be(userId);
        entity.Name.Should().Be("Food & Dining");
        entity.ParentCategoryId.Should().BeNull();
    }

    [Fact]
    public async Task CreateCategoryAsync_WithValidSubcategoryRequest_CreatesAndReturnsSubcategoryUnderParent()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var parentResult = await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Food & Dining"));
        parentResult.IsSuccess.Should().BeTrue();
        var parentId = parentResult.Value!.Id;

        var subcategoryRequest = new CreateCategoryRequest(
            Name: "Groceries",
            ParentCategoryId: parentId,
            ColorCode: "#33FF57",
            Icon: "shopping-cart",
            DisplayOrder: 2
        );

        // Act
        var result = await sut.CreateCategoryAsync(userId, subcategoryRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().NotBeEmpty();
        result.Value.Name.Should().Be("Groceries");
        result.Value.ParentCategoryId.Should().Be(parentId);
        result.Value.ColorCode.Should().Be("#33FF57");
        result.Value.Icon.Should().Be("shopping-cart");
        result.Value.DisplayOrder.Should().Be(2);

        await using var context = await _fixture.CreateContextAsync();
        var subcategoryEntity = await context.Categories.FirstOrDefaultAsync(c => c.Id == result.Value.Id);
        subcategoryEntity.Should().NotBeNull();
        subcategoryEntity!.ParentCategoryId.Should().Be(parentId);
        subcategoryEntity.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task CreateCategoryAsync_WhenParentCategoryDoesNotExist_ReturnsParentNotFoundFailure()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var nonExistentParentId = Guid.NewGuid();
        var request = new CreateCategoryRequest(
            Name: "Groceries",
            ParentCategoryId: nonExistentParentId
        );

        // Act
        var result = await sut.CreateCategoryAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PARENT_NOT_FOUND");
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();

        await using var context = await _fixture.CreateContextAsync();
        var exists = await context.Categories.AnyAsync(c => c.Name == "Groceries" && c.UserId == userId);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task CreateCategoryAsync_WhenParentIsAlreadySubcategory_FailsWithMaxDepthExceeded()
    {
        // Arrange (Depth 1: Parent -> Depth 2: Subcategory)
        var sut = CreateSut();
        var userId = CreateUserId();
        var parentResult = await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Utilities"));
        var parentId = parentResult.Value!.Id;

        var subcategoryResult = await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Electricity", ParentCategoryId: parentId));
        var subcategoryId = subcategoryResult.Value!.Id;

        // Act: Attempt to create a 3rd level child under "Electricity"
        var level3Request = new CreateCategoryRequest("Power Grid Fee", ParentCategoryId: subcategoryId);
        var result = await sut.CreateCategoryAsync(userId, level3Request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MAX_DEPTH_EXCEEDED");
        result.ErrorMessage.Should().Contain("2 levels deep");

        await using var context = await _fixture.CreateContextAsync();
        var exists = await context.Categories.AnyAsync(c => c.Name == "Power Grid Fee" && c.UserId == userId);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task CreateCategoryAsync_WithWhitespaceName_TrimsCategoryName()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var request = new CreateCategoryRequest(Name: "   Transportation   ");

        // Act
        var result = await sut.CreateCategoryAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Transportation");

        await using var context = await _fixture.CreateContextAsync();
        var entity = await context.Categories.FirstOrDefaultAsync(c => c.Id == result.Value.Id);
        entity.Should().NotBeNull();
        entity!.Name.Should().Be("Transportation");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateCategoryAsync_WithEmptyOrWhitespaceName_ReturnsValidationFailure(string invalidName)
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var request = new CreateCategoryRequest(Name: invalidName);

        // Act
        var result = await sut.CreateCategoryAsync(userId, request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region 2. Strict Per-User Isolation Tests

    [Fact]
    public async Task CreateCategoryAsync_WhenParentBelongsToDifferentUser_FailsWithParentNotFound()
    {
        // Arrange
        var sut = CreateSut();
        var userA = CreateUserId();
        var userB = CreateUserId();

        var userAParent = await sut.CreateCategoryAsync(userA, new CreateCategoryRequest("User A Food"));
        var userAParentId = userAParent.Value!.Id;

        // User B tries to use User A's parent
        var userBRequest = new CreateCategoryRequest("User B Subcategory", ParentCategoryId: userAParentId);

        // Act
        var result = await sut.CreateCategoryAsync(userB, userBRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PARENT_NOT_FOUND");

        await using var context = await _fixture.CreateContextAsync();
        var exists = await context.Categories.AnyAsync(c => c.Name == "User B Subcategory" && c.UserId == userB);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task GetCategoriesAsync_StrictlyIsolatesDataToSpecifiedUser()
    {
        // Arrange
        var sut = CreateSut();
        var userA = CreateUserId();
        var userB = CreateUserId();

        await sut.CreateCategoryAsync(userA, new CreateCategoryRequest("User A Category 1"));
        await sut.CreateCategoryAsync(userA, new CreateCategoryRequest("User A Category 2"));
        await sut.CreateCategoryAsync(userB, new CreateCategoryRequest("User B Secret Category"));

        // Act
        var resultA = await sut.GetCategoriesAsync(userA);
        var resultB = await sut.GetCategoriesAsync(userB);

        // Assert
        resultA.Should().NotBeNull();
        resultA.IsSuccess.Should().BeTrue();
        resultA.Value.Should().HaveCount(2);
        resultA.Value!.Select(c => c.Name).Should().Contain(["User A Category 1", "User A Category 2"]);
        resultA.Value.Select(c => c.Name).Should().NotContain("User B Secret Category");

        resultB.Should().NotBeNull();
        resultB.IsSuccess.Should().BeTrue();
        resultB.Value.Should().HaveCount(1);
        resultB.Value![0].Name.Should().Be("User B Secret Category");
    }

    [Fact]
    public async Task GetCategoryByIdAsync_WhenCategoryBelongsToDifferentUser_ReturnsNotFoundFailure()
    {
        // Arrange
        var sut = CreateSut();
        var userA = CreateUserId();
        var userB = CreateUserId();

        var userACategory = await sut.CreateCategoryAsync(userA, new CreateCategoryRequest("User A Category"));
        var categoryId = userACategory.Value!.Id;

        // Act: User B tries to fetch User A's category
        var result = await sut.GetCategoryByIdAsync(userB, categoryId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UpdateCategoryAsync_WhenCategoryBelongsToDifferentUser_ReturnsNotFoundFailureAndDoesNotMutate()
    {
        // Arrange
        var sut = CreateSut();
        var userA = CreateUserId();
        var userB = CreateUserId();

        var userACategory = await sut.CreateCategoryAsync(userA, new CreateCategoryRequest("Original User A Name"));
        var categoryId = userACategory.Value!.Id;

        var maliciousRequest = new UpdateCategoryRequest("Hacked Name");

        // Act: User B tries to update User A's category
        var result = await sut.UpdateCategoryAsync(userB, categoryId, maliciousRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();

        await using var context = await _fixture.CreateContextAsync();
        var entity = await context.Categories.FirstOrDefaultAsync(c => c.Id == categoryId);
        entity.Should().NotBeNull();
        entity!.Name.Should().Be("Original User A Name");
        entity.UserId.Should().Be(userA);
    }

    [Fact]
    public async Task DeleteCategoryAsync_WhenCategoryBelongsToDifferentUser_ReturnsNotFoundFailureAndDoesNotDelete()
    {
        // Arrange
        var sut = CreateSut();
        var userA = CreateUserId();
        var userB = CreateUserId();

        var userACategory = await sut.CreateCategoryAsync(userA, new CreateCategoryRequest("User A Protected Category"));
        var categoryId = userACategory.Value!.Id;

        // Act: User B tries to delete User A's category
        var result = await sut.DeleteCategoryAsync(userB, categoryId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();

        await using var context = await _fixture.CreateContextAsync();
        var exists = await context.Categories.AnyAsync(c => c.Id == categoryId);
        exists.Should().BeTrue();
    }

    #endregion

    #region 3. Hierarchical Retrieval & Single Item Retrieval Tests

    [Fact]
    public async Task GetCategoriesAsync_WhenUserHasNoCategories_ReturnsEmptyList()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        // Act
        var result = await sut.GetCategoriesAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCategoriesAsync_ReturnsTwoTierHierarchicalTreeWithSubcategoriesNested()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        // Create 2 parent categories
        var food = await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Food & Dining", DisplayOrder: 1));
        var housing = await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Housing", DisplayOrder: 2));

        var foodId = food.Value!.Id;
        var housingId = housing.Value!.Id;

        // Create subcategories under Food
        await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Groceries", ParentCategoryId: foodId, DisplayOrder: 1));
        await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Restaurants", ParentCategoryId: foodId, DisplayOrder: 2));

        // Create subcategory under Housing
        await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Rent", ParentCategoryId: housingId, DisplayOrder: 1));

        // Act
        var result = await sut.GetCategoriesAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(2); // Only top-level parents at root

        var foodDto = result.Value!.First(c => c.Id == foodId);
        foodDto.Name.Should().Be("Food & Dining");
        foodDto.ParentCategoryId.Should().BeNull();
        foodDto.Subcategories.Should().HaveCount(2);
        foodDto.Subcategories.Select(s => s.Name).Should().ContainInOrder(["Groceries", "Restaurants"]);
        foodDto.Subcategories.Should().OnlyContain(s => s.ParentCategoryId == foodId);

        var housingDto = result.Value.First(c => c.Id == housingId);
        housingDto.Name.Should().Be("Housing");
        housingDto.ParentCategoryId.Should().BeNull();
        housingDto.Subcategories.Should().HaveCount(1);
        housingDto.Subcategories[0].Name.Should().Be("Rent");
        housingDto.Subcategories[0].ParentCategoryId.Should().Be(housingId);
    }

    [Fact]
    public async Task GetCategoriesAsync_OrdersCategoriesByDisplayOrderThenByName()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Zulu Category", DisplayOrder: 10));
        await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Beta Category", DisplayOrder: 5));
        await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Alpha Category", DisplayOrder: 5));

        // Act
        var result = await sut.GetCategoriesAsync(userId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(3);

        var names = result.Value!.Select(c => c.Name).ToList();
        names[0].Should().Be("Alpha Category");
        names[1].Should().Be("Beta Category");
        names[2].Should().Be("Zulu Category");
    }

    [Fact]
    public async Task GetCategoryByIdAsync_WhenCategoryExists_ReturnsCategoryDtoWithSubcategories()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        var parent = await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Entertainment", ColorCode: "#123456", Icon: "film"));
        var parentId = parent.Value!.Id;

        await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Cinema", ParentCategoryId: parentId));
        await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Streaming", ParentCategoryId: parentId));

        // Act
        var result = await sut.GetCategoryByIdAsync(userId, parentId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(parentId);
        result.Value.Name.Should().Be("Entertainment");
        result.Value.ColorCode.Should().Be("#123456");
        result.Value.Icon.Should().Be("film");
        result.Value.Subcategories.Should().HaveCount(2);
        result.Value.Subcategories.Select(s => s.Name).Should().Contain(["Cinema", "Streaming"]);
    }

    [Fact]
    public async Task GetCategoryByIdAsync_WhenCategoryDoesNotExist_ReturnsNotFoundFailure()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await sut.GetCategoryByIdAsync(userId, nonExistentId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region 4. Update Category Tests

    [Fact]
    public async Task UpdateCategoryAsync_WithValidRequest_UpdatesCategoryPropertiesSuccessfully()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        var created = await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Health", ColorCode: "#111111", Icon: "medkit", DisplayOrder: 1));
        var categoryId = created.Value!.Id;

        var updateRequest = new UpdateCategoryRequest(
            Name: "Healthcare & Fitness",
            ParentCategoryId: null,
            ColorCode: "#222222",
            Icon: "heart",
            DisplayOrder: 5
        );

        // Act
        var result = await sut.UpdateCategoryAsync(userId, categoryId, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(categoryId);
        result.Value.Name.Should().Be("Healthcare & Fitness");
        result.Value.ColorCode.Should().Be("#222222");
        result.Value.Icon.Should().Be("heart");
        result.Value.DisplayOrder.Should().Be(5);

        await using var context = await _fixture.CreateContextAsync();
        var entity = await context.Categories.FirstOrDefaultAsync(c => c.Id == categoryId);
        entity.Should().NotBeNull();
        entity!.Name.Should().Be("Healthcare & Fitness");
        entity.ColorCode.Should().Be("#222222");
        entity.Icon.Should().Be("heart");
        entity.DisplayOrder.Should().Be(5);
    }

    [Fact]
    public async Task UpdateCategoryAsync_WhenCategoryDoesNotExist_ReturnsNotFoundFailure()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var nonExistentId = Guid.NewGuid();
        var request = new UpdateCategoryRequest("Updated Name");

        // Act
        var result = await sut.UpdateCategoryAsync(userId, nonExistentId, request);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task UpdateCategoryAsync_WhenParentCategoryDoesNotExist_FailsWithParentNotFound()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        var created = await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Subcategory"));
        var categoryId = created.Value!.Id;

        var nonExistentParentId = Guid.NewGuid();
        var updateRequest = new UpdateCategoryRequest("Subcategory", ParentCategoryId: nonExistentParentId);

        // Act
        var result = await sut.UpdateCategoryAsync(userId, categoryId, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("PARENT_NOT_FOUND");
    }

    [Fact]
    public async Task UpdateCategoryAsync_WhenTargetParentIsAlreadySubcategory_FailsWithMaxDepthExceeded()
    {
        // Arrange: Parent (Depth 1) -> Subcategory1 (Depth 2), Category2 (Root)
        var sut = CreateSut();
        var userId = CreateUserId();

        var root = await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Root"));
        var sub1 = await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Sub1", ParentCategoryId: root.Value!.Id));
        var cat2 = await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Cat2"));

        // Act: Attempt to update Cat2 to have Sub1 as parent (which would be depth 3)
        var updateRequest = new UpdateCategoryRequest("Cat2", ParentCategoryId: sub1.Value!.Id);
        var result = await sut.UpdateCategoryAsync(userId, cat2.Value!.Id, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MAX_DEPTH_EXCEEDED");
    }

    [Fact]
    public async Task UpdateCategoryAsync_WhenCategoryHasSubcategoriesAndAttemptingToSetParent_FailsWithMaxDepthExceeded()
    {
        // Arrange: ParentA has SubA. We also have ParentB.
        // Attempting to make ParentA a child of ParentB would make SubA a 3rd-level child.
        var sut = CreateSut();
        var userId = CreateUserId();

        var parentA = await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Parent A"));
        var parentAId = parentA.Value!.Id;
        await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Sub A", ParentCategoryId: parentAId));

        var parentB = await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Parent B"));
        var parentBId = parentB.Value!.Id;

        // Act: Attempt to set ParentB as parent of ParentA
        var updateRequest = new UpdateCategoryRequest("Parent A", ParentCategoryId: parentBId);
        var result = await sut.UpdateCategoryAsync(userId, parentAId, updateRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be("MAX_DEPTH_EXCEEDED");
    }

    [Fact]
    public async Task UpdateCategoryAsync_WhenSettingSelfAsParent_ReturnsFailure()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        var created = await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Self Category"));
        var categoryId = created.Value!.Id;

        var selfParentRequest = new UpdateCategoryRequest("Self Category", ParentCategoryId: categoryId);

        // Act
        var result = await sut.UpdateCategoryAsync(userId, categoryId, selfParentRequest);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    #endregion

    #region 5. Delete Category Tests

    [Fact]
    public async Task DeleteCategoryAsync_WhenCategoryExistsWithoutDependencies_DeletesSuccessfully()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        var created = await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Temporary Category"));
        var categoryId = created.Value!.Id;

        // Act
        var result = await sut.DeleteCategoryAsync(userId, categoryId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        await using var context = await _fixture.CreateContextAsync();
        var exists = await context.Categories.AnyAsync(c => c.Id == categoryId);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteCategoryAsync_WhenParentCategoryDeleted_CascadesDeleteToSubcategories()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        var parent = await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Parent To Delete"));
        var parentId = parent.Value!.Id;

        var sub1 = await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Child 1", ParentCategoryId: parentId));
        var sub2 = await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Child 2", ParentCategoryId: parentId));

        var sub1Id = sub1.Value!.Id;
        var sub2Id = sub2.Value!.Id;

        // Act: Delete parent category
        var result = await sut.DeleteCategoryAsync(userId, parentId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeTrue();

        await using var context = await _fixture.CreateContextAsync();
        var parentExists = await context.Categories.AnyAsync(c => c.Id == parentId);
        var sub1Exists = await context.Categories.AnyAsync(c => c.Id == sub1Id);
        var sub2Exists = await context.Categories.AnyAsync(c => c.Id == sub2Id);

        parentExists.Should().BeFalse();
        sub1Exists.Should().BeFalse();
        sub2Exists.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteCategoryAsync_WhenCategoryDoesNotExist_ReturnsNotFoundFailure()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await sut.DeleteCategoryAsync(userId, nonExistentId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task DeleteCategoryAsync_WhenCategoryHasAssociatedTransactions_FailsAndPreservesCategory()
    {
        // Arrange
        var sut = CreateSut();
        var userId = CreateUserId();

        var category = await sut.CreateCategoryAsync(userId, new CreateCategoryRequest("Transactions Category"));
        var categoryId = category.Value!.Id;

        // Seed a transaction directly in the database linked to this category
        await using (var context = await _fixture.CreateContextAsync())
        {
            context.Transactions.Add(new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                CategoryId = categoryId,
                Amount = 89.99m,
                Description = "Restaurant Bill",
                Type = TransactionType.Expense,
                Date = DateOnly.FromDateTime(DateTime.UtcNow),
                CreatedAtUtc = DateTime.UtcNow
            });
            await context.SaveChangesAsync();
        }

        // Act
        var result = await sut.DeleteCategoryAsync(userId, categoryId);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ErrorMessage.Should().NotBeNullOrWhiteSpace();

        // Verify category is still present in the database
        await using (var verifyContext = await _fixture.CreateContextAsync())
        {
            var exists = await verifyContext.Categories.AnyAsync(c => c.Id == categoryId);
            exists.Should().BeTrue();
        }
    }

    #endregion
}
