using Expenses.Models.Enums;

namespace Expenses.Models.DTOs;

public sealed record TransactionDto(
    Guid Id,
    DateOnly Date,
    decimal Amount,
    string Description,
    TransactionType Type,
    Guid? CategoryId,
    string? CategoryName,
    Guid? SubcategoryId,
    string? SubcategoryName,
    string? Notes,
    DateTime CreatedAtUtc
);

public sealed record CreateTransactionRequest(
    DateOnly Date,
    decimal Amount,
    string Description,
    TransactionType Type,
    Guid? CategoryId = null,
    Guid? SubcategoryId = null,
    string? Notes = null
);

public sealed record UpdateTransactionRequest(
    DateOnly Date,
    decimal Amount,
    string Description,
    TransactionType Type,
    Guid? CategoryId = null,
    Guid? SubcategoryId = null,
    string? Notes = null
);

public sealed record TransactionFilter(
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    IReadOnlyList<Guid>? CategoryIds = null,
    TransactionType? Type = null,
    string? SearchTerm = null
);
