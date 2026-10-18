using System.Text.Json;
using FluentValidation.Results;

namespace TurnoSync.Application.Common;

internal static class ValidationResultExtensions
{
    /// <summary>Agrupa as falhas por campo, com o nome em camelCase (o mesmo do JSON da API).</summary>
    public static ResultError ToResultError(this ValidationResult result) =>
        ResultError.Validation(result.Errors
            .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
}
