namespace Expenses.Models.Entities;

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? ParentCategoryId { get; set; }
    public string? ColorCode { get; set; }
    public string? Icon { get; set; }
    public int DisplayOrder { get; set; }

    // Navigation properties
    public Category? ParentCategory { get; set; }
    public ICollection<Category> Subcategories { get; set; } = [];
    public ICollection<Transaction> Transactions { get; set; } = [];
}
