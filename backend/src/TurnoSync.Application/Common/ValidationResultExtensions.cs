using System.Text.Json;
using FluentValidation.Results;

namespace TurnoSync.Application.Common;

internal static class ValidationResultExtensions
{
    /// <summary>Agrupa los fallos por campo, con el nombre en camelCase (igual que el JSON de la API).</summary>
    public static ResultError ToResultError(this ValidationResult result) =>
        ResultError.Validation(result.Errors
            .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
}
