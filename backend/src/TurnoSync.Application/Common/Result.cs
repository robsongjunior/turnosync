namespace TurnoSync.Application.Common;

public enum ErrorType
{
    /// <summary>Dados inválidos (HTTP 400).</summary>
    Validation,

    /// <summary>Recurso inexistente (HTTP 404).</summary>
    NotFound,

    /// <summary>Conflito com o estado atual, como e-mail duplicado ou turno sobreposto (HTTP 409).</summary>
    Conflict,
}

/// <summary>Erro de negócio. <see cref="FieldErrors"/> só é preenchido em erros de validação.</summary>
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

/// <summary>Resultado de uma operação sem valor de retorno. Erro de negócio é valor, não exceção.</summary>
public sealed class Result
{
    private Result(ResultError? error) => Error = error;

    public bool IsSuccess => Error is null;

    public ResultError? Error { get; }

    public static Result Success() => new(null);

    public static implicit operator Result(ResultError error) => new(error);
}

/// <summary>Resultado de uma operação com valor de retorno. Erro de negócio é valor, não exceção.</summary>
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
