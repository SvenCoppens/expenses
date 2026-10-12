using System.ComponentModel.DataAnnotations;
using Expenses.Models.DTOs;

namespace Expenses.Models.ViewModels;

public class CategoryFormModel
{
    public Guid? Id { get; set; }

    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(100, ErrorMessage = "Category name cannot exceed 100 characters.")]
    public string Name { get; set; } = string.Empty;

    public Guid? ParentCategoryId { get; set; }

    [RegularExpression("^#(?:[0-9a-fA-F]{3}){1,2}$", ErrorMessage = "Color must be a valid hex code (e.g. #FF5733).")]
    public string? ColorCode { get; set; } = "#3B82F6";

    public string? Icon { get; set; }

    public int DisplayOrder { get; set; }

    public CreateCategoryRequest ToCreateRequest() => new(
        Name: Name.Trim(),
        ParentCategoryId: ParentCategoryId,
        ColorCode: ColorCode,
        Icon: Icon,
        DisplayOrder: DisplayOrder
    );

    public UpdateCategoryRequest ToUpdateRequest() => new(
        Name: Name.Trim(),
        ParentCategoryId: ParentCategoryId,
        ColorCode: ColorCode,
        Icon: Icon,
        DisplayOrder: DisplayOrder
    );

    public static CategoryFormModel FromDto(CategoryDto dto) => new()
    {
        Id = dto.Id,
        Name = dto.Name,
        ParentCategoryId = dto.ParentCategoryId,
        ColorCode = dto.ColorCode,
        Icon = dto.Icon,
        DisplayOrder = dto.DisplayOrder
    };
}
