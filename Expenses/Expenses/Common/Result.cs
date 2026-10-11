namespace Expenses.Common;

public sealed record Result<T>
{
    public bool IsSuccess { get; init; }
    public T? Value { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }

    public static Result<T> Success(T value) =>
        new() { IsSuccess = true, Value = value };

    public static Result<T> Failure(string errorMessage, string? errorCode = null) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage, ErrorCode = errorCode };
}

public sealed record Result
{
    public bool IsSuccess { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorCode { get; init; }

    public static Result Success() =>
        new() { IsSuccess = true };

    public static Result Failure(string errorMessage, string? errorCode = null) =>
        new() { IsSuccess = false, ErrorMessage = errorMessage, ErrorCode = errorCode };
}
