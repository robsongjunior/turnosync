using System.Text.Json;
using FluentValidation.Results;

namespace TurnoSync.Application.Common;

internal static class ValidationResultExtensions
{
    /// <summary>Groups the failures by field, with the name in camelCase (the same as the API JSON).</summary>
    public static ResultError ToResultError(this ValidationResult result) =>
        ResultError.Validation(result.Errors
            .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()));
}
