using Expenses.Common;
using Expenses.Data;
using Expenses.Models.DTOs;
using Expenses.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Expenses.Services.Transactions;

public class TransactionService(
    IDbContextFactory<ApplicationDbContext> dbContextFactory,
    ILogger<TransactionService> logger) : ITransactionService
{
    public async Task<Result<IReadOnlyList<TransactionDto>>> GetTransactionsAsync(
        string userId,
        TransactionFilter? filter = null,
        CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var query = context.Transactions
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Subcategory)
            .Where(t => t.UserId == userId);

        if (filter is not null)
        {
            if (filter.StartDate.HasValue)
            {
                query = query.Where(t => t.Date >= filter.StartDate.Value);
            }

            if (filter.EndDate.HasValue)
            {
                query = query.Where(t => t.Date <= filter.EndDate.Value);
            }

            if (filter.Type.HasValue)
            {
                query = query.Where(t => t.Type == filter.Type.Value);
            }

            if (filter.CategoryIds is { Count: > 0 })
            {
                query = query.Where(t =>
                    (t.CategoryId.HasValue && filter.CategoryIds.Contains(t.CategoryId.Value)) ||
                    (t.SubcategoryId.HasValue && filter.CategoryIds.Contains(t.SubcategoryId.Value)));
            }

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim().ToLower();
                query = query.Where(t => t.Description.ToLower().Contains(term));
            }
        }

        var results = await query
            .OrderByDescending(t => t.Date)
            .ThenByDescending(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        var dtos = results.Select(MapToDto).ToList();
        return Result<IReadOnlyList<TransactionDto>>.Success(dtos);
    }

    public async Task<Result<TransactionDto>> GetTransactionByIdAsync(
        string userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var transaction = await context.Transactions
            .AsNoTracking()
            .Include(t => t.Category)
            .Include(t => t.Subcategory)
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken);

        if (transaction is null)
        {
            return Result<TransactionDto>.Failure("Transaction not found.", "NOT_FOUND");
        }

        return Result<TransactionDto>.Success(MapToDto(transaction));
    }

    public async Task<Result<TransactionDto>> CreateTransactionAsync(
        string userId,
        CreateTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return Result<TransactionDto>.Failure("Description is required.", "INVALID_DESCRIPTION");
        }

        if (request.SubcategoryId.HasValue && !request.CategoryId.HasValue)
        {
            return Result<TransactionDto>.Failure("Subcategory cannot be specified without a parent category.", "INVALID_CATEGORY_HIERARCHY");
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        Category? category = null;
        if (request.CategoryId.HasValue)
        {
            category = await context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId.Value && c.UserId == userId, cancellationToken);

            if (category is null)
            {
                return Result<TransactionDto>.Failure("Category not found.", "CATEGORY_NOT_FOUND");
            }
        }

        Category? subcategory = null;
        if (request.SubcategoryId.HasValue)
        {
            subcategory = await context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.SubcategoryId.Value && c.UserId == userId, cancellationToken);

            if (subcategory is null)
            {
                return Result<TransactionDto>.Failure("Subcategory not found.", "SUBCATEGORY_NOT_FOUND");
            }

            if (subcategory.ParentCategoryId != request.CategoryId!.Value)
            {
                return Result<TransactionDto>.Failure("Subcategory does not belong to the specified parent category.", "INVALID_SUBCATEGORY");
            }
        }

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Date = request.Date,
            Amount = request.Amount,
            Description = request.Description.Trim(),
            Type = request.Type,
            CategoryId = request.CategoryId,
            SubcategoryId = request.SubcategoryId,
            Notes = request.Notes,
            CreatedAtUtc = DateTime.UtcNow
        };

        context.Transactions.Add(transaction);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Created transaction {TransactionId} for user {UserId}", transaction.Id, userId);

        var dto = new TransactionDto(
            Id: transaction.Id,
            Date: transaction.Date,
            Amount: transaction.Amount,
            Description: transaction.Description,
            Type: transaction.Type,
            CategoryId: transaction.CategoryId,
            CategoryName: category?.Name,
            SubcategoryId: transaction.SubcategoryId,
            SubcategoryName: subcategory?.Name,
            Notes: transaction.Notes,
            CreatedAtUtc: transaction.CreatedAtUtc
        );

        return Result<TransactionDto>.Success(dto);
    }

    public async Task<Result<TransactionDto>> UpdateTransactionAsync(
        string userId,
        Guid id,
        UpdateTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return Result<TransactionDto>.Failure("Description is required.", "INVALID_DESCRIPTION");
        }

        if (request.SubcategoryId.HasValue && !request.CategoryId.HasValue)
        {
            return Result<TransactionDto>.Failure("Subcategory cannot be specified without a parent category.", "INVALID_CATEGORY_HIERARCHY");
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var transaction = await context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken);

        if (transaction is null)
        {
            return Result<TransactionDto>.Failure("Transaction not found.", "NOT_FOUND");
        }

        Category? category = null;
        if (request.CategoryId.HasValue)
        {
            category = await context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.CategoryId.Value && c.UserId == userId, cancellationToken);

            if (category is null)
            {
                return Result<TransactionDto>.Failure("Category not found.", "CATEGORY_NOT_FOUND");
            }
        }

        Category? subcategory = null;
        if (request.SubcategoryId.HasValue)
        {
            subcategory = await context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.SubcategoryId.Value && c.UserId == userId, cancellationToken);

            if (subcategory is null)
            {
                return Result<TransactionDto>.Failure("Subcategory not found.", "SUBCATEGORY_NOT_FOUND");
            }

            if (subcategory.ParentCategoryId != request.CategoryId!.Value)
            {
                return Result<TransactionDto>.Failure("Subcategory does not belong to the specified parent category.", "INVALID_SUBCATEGORY");
            }
        }

        transaction.Date = request.Date;
        transaction.Amount = request.Amount;
        transaction.Description = request.Description.Trim();
        transaction.Type = request.Type;
        transaction.CategoryId = request.CategoryId;
        transaction.SubcategoryId = request.SubcategoryId;
        transaction.Notes = request.Notes;

        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Updated transaction {TransactionId} for user {UserId}", transaction.Id, userId);

        var dto = new TransactionDto(
            Id: transaction.Id,
            Date: transaction.Date,
            Amount: transaction.Amount,
            Description: transaction.Description,
            Type: transaction.Type,
            CategoryId: transaction.CategoryId,
            CategoryName: category?.Name,
            SubcategoryId: transaction.SubcategoryId,
            SubcategoryName: subcategory?.Name,
            Notes: transaction.Notes,
            CreatedAtUtc: transaction.CreatedAtUtc
        );

        return Result<TransactionDto>.Success(dto);
    }

    public async Task<Result> DeleteTransactionAsync(
        string userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var transaction = await context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, cancellationToken);

        if (transaction is null)
        {
            return Result.Failure("Transaction not found.", "NOT_FOUND");
        }

        context.Transactions.Remove(transaction);
        await context.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Deleted transaction {TransactionId} for user {UserId}", id, userId);

        return Result.Success();
    }

    public async Task<Result<int>> DeleteTransactionsBulkAsync(
        string userId,
        IReadOnlyList<Guid> transactionIds,
        CancellationToken cancellationToken = default)
    {
        if (transactionIds.Count == 0)
        {
            return Result<int>.Success(0);
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var affected = await context.Transactions
            .Where(t => t.UserId == userId && transactionIds.Contains(t.Id))
            .ExecuteDeleteAsync(cancellationToken);

        logger.LogInformation("Bulk deleted {Count} transactions for user {UserId}", affected, userId);

        return Result<int>.Success(affected);
    }

    private static TransactionDto MapToDto(Transaction transaction) =>
        new(
            Id: transaction.Id,
            Date: transaction.Date,
            Amount: transaction.Amount,
            Description: transaction.Description,
            Type: transaction.Type,
            CategoryId: transaction.CategoryId,
            CategoryName: transaction.Category?.Name,
            SubcategoryId: transaction.SubcategoryId,
            SubcategoryName: transaction.Subcategory?.Name,
            Notes: transaction.Notes,
            CreatedAtUtc: transaction.CreatedAtUtc
        );
}
