using TurnoSync.Application.Common;

namespace TurnoSync.Application.Shifts;

/// <summary>Validación del período (from, to) que comparten el listado y la exportación.</summary>
internal static class ShiftPeriod
{
    /// <summary>Null si el período es válido. En el listado las dos fechas son opcionales; en la exportación, no.</summary>
    public static ResultError? Validate(DateOnly? from, DateOnly? to, bool required)
    {
        var errors = new Dictionary<string, string[]>();
        if (required && from is null)
        {
            errors["from"] = ["El parámetro 'from' es obligatorio (yyyy-MM-dd)."];
        }

        if (required && to is null)
        {
            errors["to"] = ["El parámetro 'to' es obligatorio (yyyy-MM-dd)."];
        }

        if (from > to)
        {
            errors["from"] = ["El parámetro 'from' debe ser anterior o igual a 'to'."];
        }

        return errors.Count > 0 ? ResultError.Validation(errors) : null;
    }
}
