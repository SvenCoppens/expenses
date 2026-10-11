using Expenses.Models.Enums;

namespace Expenses.Models.Entities;

public class Transaction
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public TransactionType Type { get; set; } = TransactionType.Expense;
    public Guid? CategoryId { get; set; }
    public Guid? SubcategoryId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public Category? Category { get; set; }
    public Category? Subcategory { get; set; }
}
