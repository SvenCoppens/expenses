using System.ComponentModel.DataAnnotations;
using Expenses.Models.DTOs;
using Expenses.Models.Enums;

namespace Expenses.Models.ViewModels;

public class TransactionFormModel
{
    [Required(ErrorMessage = "Date is required.")]
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [Required]
    [Range(0.01, 1_000_000_000, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Description is required.")]
    [StringLength(250, ErrorMessage = "Description cannot exceed 250 characters.")]
    public string Description { get; set; } = string.Empty;

    public TransactionType Type { get; set; } = TransactionType.Expense;

    public Guid? CategoryId { get; set; }

    public Guid? SubcategoryId { get; set; }

    [StringLength(1000, ErrorMessage = "Notes cannot exceed 1000 characters.")]
    public string? Notes { get; set; }

    public CreateTransactionRequest ToRequest() => new(
        Date: Date,
        Amount: Amount,
        Description: Description?.Trim() ?? string.Empty,
        Type: Type,
        CategoryId: CategoryId,
        SubcategoryId: SubcategoryId,
        Notes: Notes?.Trim()
    );

    public void Reset()
    {
        Date = DateOnly.FromDateTime(DateTime.UtcNow);
        Amount = 0m;
        Description = string.Empty;
        Type = TransactionType.Expense;
        CategoryId = null;
        SubcategoryId = null;
        Notes = null;
    }
}
