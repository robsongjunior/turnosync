namespace TurnoSync.Application.Common;

public enum ErrorType
{
    /// <summary>Datos no válidos (HTTP 400).</summary>
    Validation,

    /// <summary>El recurso no existe (HTTP 404).</summary>
    NotFound,

    /// <summary>Conflicto con el estado actual, como un email duplicado o un turno solapado (HTTP 409).</summary>
    Conflict,
}

/// <summary>Error de negocio. <see cref="FieldErrors"/> solo se rellena en los errores de validación.</summary>
public sealed record ResultError(ErrorType Type, string Message, IReadOnlyDictionary<string, string[]> FieldErrors)
{
    private static readonly IReadOnlyDictionary<string, string[]> NoFieldErrors = new Dictionary<string, string[]>();

    public static ResultError Validation(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new(ErrorType.Validation, "Se han producido uno o más errores de validación.", fieldErrors);

    public static ResultError Validation(string field, string message) =>
        Validation(new Dictionary<string, string[]> { [field] = [message] });

    public static ResultError NotFound(string message) => new(ErrorType.NotFound, message, NoFieldErrors);

    public static ResultError Conflict(string message) => new(ErrorType.Conflict, message, NoFieldErrors);
}

/// <summary>Resultado de una operación sin valor de retorno. Un error de negocio es un valor, no una excepción.</summary>
public sealed class Result
{
    private Result(ResultError? error) => Error = error;

    public bool IsSuccess => Error is null;

    public ResultError? Error { get; }

    public static Result Success() => new(null);

    public static implicit operator Result(ResultError error) => new(error);
}

/// <summary>Resultado de una operación que devuelve un valor. Un error de negocio es un valor, no una excepción.</summary>
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
