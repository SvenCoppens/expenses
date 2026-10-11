using Expenses.Common;
using Expenses.Models.DTOs;

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
