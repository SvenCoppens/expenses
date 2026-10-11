using Expenses.Common;
using Expenses.Data;
using Expenses.Models.DTOs;
using Expenses.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Expenses.Services.Categories;

public class CategoryService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ILogger<CategoryService> logger) : ICategoryService
{
    public async Task<Result<IReadOnlyList<CategoryDto>>> GetCategoriesAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<IReadOnlyList<CategoryDto>>.Failure("User ID cannot be empty.", "INVALID_USER_ID");
        }

        try
        {
            await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var categories = await context.Categories
                .AsNoTracking()
                .Where(c => c.UserId == userId && c.ParentCategoryId == null)
                .Include(c => c.Subcategories)
                .OrderBy(c => c.DisplayOrder)
                .ThenBy(c => c.Name)
                .ToListAsync(cancellationToken);

            var dtos = categories.Select(MapToDto).ToList();
            return Result<IReadOnlyList<CategoryDto>>.Success(dtos);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve categories for user {UserId}", userId);
            return Result<IReadOnlyList<CategoryDto>>.Failure("An error occurred while retrieving categories.", "DATABASE_ERROR");
        }
    }

    public async Task<Result<CategoryDto>> GetCategoryByIdAsync(
        string userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<CategoryDto>.Failure("User ID cannot be empty.", "INVALID_USER_ID");
        }

        try
        {
            await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var category = await context.Categories
                .AsNoTracking()
                .Include(c => c.Subcategories)
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);

            if (category is null)
            {
                return Result<CategoryDto>.Failure("Category not found.", "CATEGORY_NOT_FOUND");
            }

            return Result<CategoryDto>.Success(MapToDto(category));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to retrieve category {CategoryId} for user {UserId}", id, userId);
            return Result<CategoryDto>.Failure("An error occurred while retrieving the category.", "DATABASE_ERROR");
        }
    }

    public async Task<Result<CategoryDto>> CreateCategoryAsync(
        string userId,
        CreateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<CategoryDto>.Failure("User ID cannot be empty.", "INVALID_USER_ID");
        }

        if (request is null)
        {
            return Result<CategoryDto>.Failure("Request cannot be null.", "INVALID_REQUEST");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<CategoryDto>.Failure("Category name cannot be empty.", "INVALID_NAME");
        }

        try
        {
            await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            if (request.ParentCategoryId.HasValue)
            {
                var parent = await context.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == request.ParentCategoryId.Value && c.UserId == userId, cancellationToken);

                if (parent is null)
                {
                    return Result<CategoryDto>.Failure("Parent category not found.", "PARENT_NOT_FOUND");
                }

                if (parent.ParentCategoryId.HasValue)
                {
                    return Result<CategoryDto>.Failure("Cannot nest categories more than 2 levels deep.", "MAX_DEPTH_EXCEEDED");
                }
            }

            var category = new Category
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Name = request.Name.Trim(),
                ParentCategoryId = request.ParentCategoryId,
                ColorCode = request.ColorCode,
                Icon = request.Icon,
                DisplayOrder = request.DisplayOrder
            };

            context.Categories.Add(category);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Created category {CategoryId} ({Name}) for user {UserId}", category.Id, category.Name, userId);

            return Result<CategoryDto>.Success(MapToDto(category));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create category {Name} for user {UserId}", request.Name, userId);
            return Result<CategoryDto>.Failure("An error occurred while creating the category.", "DATABASE_ERROR");
        }
    }

    public async Task<Result<CategoryDto>> UpdateCategoryAsync(
        string userId,
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result<CategoryDto>.Failure("User ID cannot be empty.", "INVALID_USER_ID");
        }

        if (request is null)
        {
            return Result<CategoryDto>.Failure("Request cannot be null.", "INVALID_REQUEST");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Result<CategoryDto>.Failure("Category name cannot be empty.", "INVALID_NAME");
        }

        if (request.ParentCategoryId.HasValue && request.ParentCategoryId.Value == id)
        {
            return Result<CategoryDto>.Failure("A category cannot be its own parent.", "INVALID_PARENT");
        }

        try
        {
            await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var category = await context.Categories
                .Include(c => c.Subcategories)
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);

            if (category is null)
            {
                return Result<CategoryDto>.Failure("Category not found.", "CATEGORY_NOT_FOUND");
            }

            if (request.ParentCategoryId.HasValue)
            {
                if (category.Subcategories.Count > 0)
                {
                    return Result<CategoryDto>.Failure("Cannot nest categories more than 2 levels deep.", "MAX_DEPTH_EXCEEDED");
                }

                var parent = await context.Categories
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == request.ParentCategoryId.Value && c.UserId == userId, cancellationToken);

                if (parent is null)
                {
                    return Result<CategoryDto>.Failure("Parent category not found.", "PARENT_NOT_FOUND");
                }

                if (parent.ParentCategoryId.HasValue)
                {
                    return Result<CategoryDto>.Failure("Cannot nest categories more than 2 levels deep.", "MAX_DEPTH_EXCEEDED");
                }
            }

            category.Name = request.Name.Trim();
            category.ParentCategoryId = request.ParentCategoryId;
            category.ColorCode = request.ColorCode;
            category.Icon = request.Icon;
            category.DisplayOrder = request.DisplayOrder;

            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Updated category {CategoryId} for user {UserId}", category.Id, userId);

            return Result<CategoryDto>.Success(MapToDto(category));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update category {CategoryId} for user {UserId}", id, userId);
            return Result<CategoryDto>.Failure("An error occurred while updating the category.", "DATABASE_ERROR");
        }
    }

    public async Task<Result> DeleteCategoryAsync(
        string userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Result.Failure("User ID cannot be empty.", "INVALID_USER_ID");
        }

        try
        {
            await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

            var category = await context.Categories
                .Include(c => c.Subcategories)
                .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, cancellationToken);

            if (category is null)
            {
                return Result.Failure("Category not found.", "CATEGORY_NOT_FOUND");
            }

            var allCategoryIds = new List<Guid> { id };
            allCategoryIds.AddRange(category.Subcategories.Select(s => s.Id));

            var hasTransactions = await context.Transactions
                .AnyAsync(t => (t.CategoryId.HasValue && allCategoryIds.Contains(t.CategoryId.Value)) ||
                               (t.SubcategoryId.HasValue && allCategoryIds.Contains(t.SubcategoryId.Value)),
                          cancellationToken);

            if (hasTransactions)
            {
                return Result.Failure("Cannot delete category with associated transactions.", "CATEGORY_IN_USE");
            }

            context.Categories.Remove(category);
            await context.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Deleted category {CategoryId} for user {UserId}", id, userId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete category {CategoryId} for user {UserId}", id, userId);
            return Result.Failure("An error occurred while deleting the category.", "DATABASE_ERROR");
        }
    }

    private static CategoryDto MapToDto(Category category) =>
        new(
            category.Id,
            category.Name,
            category.ParentCategoryId,
            category.ColorCode,
            category.Icon,
            category.DisplayOrder,
            category.Subcategories is { Count: > 0 }
                ? category.Subcategories
                    .OrderBy(s => s.DisplayOrder)
                    .ThenBy(s => s.Name)
                    .Select(s => new CategoryDto(
                        s.Id,
                        s.Name,
                        s.ParentCategoryId,
                        s.ColorCode,
                        s.Icon,
                        s.DisplayOrder,
                        []))
                    .ToList()
                : []
        );
}
