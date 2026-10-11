namespace Expenses.Models.DTOs;

public sealed record CategoryDto(
    Guid Id,
    string Name,
    Guid? ParentCategoryId,
    string? ColorCode,
    string? Icon,
    int DisplayOrder,
    IReadOnlyList<CategoryDto> Subcategories
);

public sealed record CreateCategoryRequest(
    string Name,
    Guid? ParentCategoryId = null,
    string? ColorCode = null,
    string? Icon = null,
    int DisplayOrder = 0
);

public sealed record UpdateCategoryRequest(
    string Name,
    Guid? ParentCategoryId = null,
    string? ColorCode = null,
    string? Icon = null,
    int DisplayOrder = 0
);
