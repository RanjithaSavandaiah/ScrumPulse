namespace ScrumPulse.Domain.Common;

/// <summary>
/// Railway-oriented Result monad for explicit success/failure propagation
/// without exceptions. Supports error codes, monadic chaining, and validation.
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string? Error { get; }
    public string? ErrorCode { get; }

    protected Result(bool isSuccess, string? error, string? errorCode = null)
    {
        if (isSuccess && error is not null)
            throw new InvalidOperationException("A successful result cannot carry an error message.");
        if (!isSuccess && string.IsNullOrWhiteSpace(error))
            throw new InvalidOperationException("A failed result must carry an error message.");

        IsSuccess = isSuccess;
        Error = error;
        ErrorCode = errorCode;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(string error, string? errorCode = null) => new(false, error, errorCode);

    /// <summary>Validates a condition and returns Failure if the predicate is false.</summary>
    public static Result Ensure(bool condition, string error, string? errorCode = null) =>
        condition ? Success() : Failure(error, errorCode);

    /// <summary>Combines multiple results; returns the first failure or Success if all pass.</summary>
    public static Result Combine(params Result[] results)
    {
        foreach (var result in results)
        {
            if (result.IsFailure)
                return result;
        }
        return Success();
    }
}

/// <summary>
/// Generic Result monad carrying a typed value on success.
/// Supports Map/Bind for functional composition.
/// </summary>
public class Result<T> : Result
{
    public T? Value { get; }

    protected Result(bool isSuccess, T? value, string? error, string? errorCode = null)
        : base(isSuccess, error, errorCode)
    {
        if (isSuccess && value is null)
            throw new InvalidOperationException("A successful result must carry a value.");
        Value = value;
    }

    public static Result<T> Success(T value) => new(true, value, null);
    public static new Result<T> Failure(string error, string? errorCode = null) => new(false, default, error, errorCode);

    /// <summary>Transforms the success value using the provided mapping function.</summary>
    public Result<TOut> Map<TOut>(Func<T, TOut> mapper) =>
        IsSuccess ? Result<TOut>.Success(mapper(Value!)) : Result<TOut>.Failure(Error!, ErrorCode);

    /// <summary>Chains a result-producing operation onto a success value.</summary>
    public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> binder) =>
        IsSuccess ? binder(Value!) : Result<TOut>.Failure(Error!, ErrorCode);

    /// <summary>Executes an action on the value if the result is successful.</summary>
    public Result<T> Tap(Action<T> action)
    {
        if (IsSuccess) action(Value!);
        return this;
    }
}
