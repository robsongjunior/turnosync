namespace TurnoSync.Application.Common;

public enum ErrorType
{
    /// <summary>Invalid data (HTTP 400).</summary>
    Validation,

    /// <summary>Resource does not exist (HTTP 404).</summary>
    NotFound,

    /// <summary>Conflict with the current state, such as a duplicate email or an overlapping shift (HTTP 409).</summary>
    Conflict,
}

/// <summary>Business error. <see cref="FieldErrors"/> is only filled for validation errors.</summary>
public sealed record ResultError(ErrorType Type, string Message, IReadOnlyDictionary<string, string[]> FieldErrors)
{
    private static readonly IReadOnlyDictionary<string, string[]> NoFieldErrors = new Dictionary<string, string[]>();

    public static ResultError Validation(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new(ErrorType.Validation, "One or more validation errors occurred.", fieldErrors);

    public static ResultError Validation(string field, string message) =>
        Validation(new Dictionary<string, string[]> { [field] = [message] });

    public static ResultError NotFound(string message) => new(ErrorType.NotFound, message, NoFieldErrors);

    public static ResultError Conflict(string message) => new(ErrorType.Conflict, message, NoFieldErrors);
}

/// <summary>Outcome of an operation with no return value. A business error is a value, not an exception.</summary>
public sealed class Result
{
    private Result(ResultError? error) => Error = error;

    public bool IsSuccess => Error is null;

    public ResultError? Error { get; }

    public static Result Success() => new(null);

    public static implicit operator Result(ResultError error) => new(error);
}

/// <summary>Outcome of an operation that returns a value. A business error is a value, not an exception.</summary>
public sealed class Result<T>
{
    private Result(T? value, ResultError? error)
    {
        Value = value;
        Error = error;
    }

    public bool IsSuccess => Error is null;

    public T? Value { get; }

    public ResultError? Error { get; }

    public static Result<T> Success(T value) => new(value, null);

    public static implicit operator Result<T>(ResultError error) => new(default, error);

    public static implicit operator Result<T>(T value) => Success(value);
}
