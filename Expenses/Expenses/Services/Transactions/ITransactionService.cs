using Expenses.Common;
using Expenses.Models.DTOs;

namespace Expenses.Services.Transactions;

public interface ITransactionService
{
    Task<Result<IReadOnlyList<TransactionDto>>> GetTransactionsAsync(
        string userId,
        TransactionFilter? filter = null,
        CancellationToken cancellationToken = default);

    Task<Result<TransactionDto>> GetTransactionByIdAsync(
        string userId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<TransactionDto>> CreateTransactionAsync(
        string userId,
        CreateTransactionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<TransactionDto>> UpdateTransactionAsync(
        string userId,
        Guid id,
        UpdateTransactionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteTransactionAsync(
        string userId,
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<int>> DeleteTransactionsBulkAsync(
        string userId,
        IReadOnlyList<Guid> transactionIds,
        CancellationToken cancellationToken = default);
}
