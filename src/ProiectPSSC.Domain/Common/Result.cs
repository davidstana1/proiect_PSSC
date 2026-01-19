namespace ProiectPSSC.Domain.Common;

public readonly record struct Error(string Code, string Message)
{
    public static Error Validation(string message) => new("validation", message);
    public static Error NotFound(string message) => new("not_found", message);
    public static Error Conflict(string message) => new("conflict", message);
    public static Error Unexpected(string message) => new("unexpected", message);
}

public readonly struct Result
{
    public bool IsSuccess { get; }
    public Error? Error { get; }

    private Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Ok() => new(true, null);
    public static Result Fail(Error error) => new(false, error);
}

public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error? Error { get; }

    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(Error error) => new(false, default, error);
}

public static class ResultExtensions
{
    public static Result<U> Map<T, U>(this Result<T> result, Func<T, U> map)
        => result.IsSuccess ? Result<U>.Ok(map(result.Value!)) : Result<U>.Fail(result.Error!.Value);

    public static async Task<Result<U>> Bind<T, U>(this Result<T> result, Func<T, Task<Result<U>>> bind)
        => result.IsSuccess ? await bind(result.Value!) : Result<U>.Fail(result.Error!.Value);
}
