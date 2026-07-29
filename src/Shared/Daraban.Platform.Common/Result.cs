namespace Daraban.Platform.Common;

/// <summary>No-exception-for-control-flow result type used by every Service method
/// that can fail in an expected way (validation, business rule, not-found).</summary>
public class Result
{
    public bool IsSuccess { get; }
    public Error? Error { get; }

    protected Result(bool isSuccess, Error? error)
    {
        if (isSuccess && error is not null)
            throw new InvalidOperationException("A successful result cannot carry an error.");
        if (!isSuccess && error is null)
            throw new InvalidOperationException("A failed result must carry an error.");
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, null);
    public static Result Failure(Error error) => new(false, error);
    public static Result<T> Success<T>(T value) => new(value, true, null);
    public static Result<T> Failure<T>(Error error) => new(default, false, error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;
    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("Cannot access Value of a failed result.");

    internal Result(T? value, bool isSuccess, Error? error) : base(isSuccess, error) => _value = value;
}

/// <summary>errorCode follows the MODULE.SCREAMING_SNAKE_CASE convention from Task 1.4 SS6.</summary>
public sealed record Error(string Code, string Message, ErrorType Type);

public enum ErrorType { Validation, NotFound, Conflict, BusinessRule, Forbidden }
