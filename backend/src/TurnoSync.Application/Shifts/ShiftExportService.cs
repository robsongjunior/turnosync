using System.Globalization;
using TurnoSync.Application.Common;

namespace TurnoSync.Application.Shifts;

public sealed record ExportedFile(string FileName, byte[] Content);

public sealed class ShiftExportService(IShiftRepository shifts, IShiftSpreadsheet spreadsheet)
{
    /// <summary>
    /// Exporta los turnos cuya fecha (Date, la de inicio) está entre from y to, ambas inclusivas y obligatorias,
    /// en el mismo orden que el listado y con las mismas columnas que la importación.
    /// </summary>
    public async Task<Result<ExportedFile>> ExportAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (from is null)
        {
            errors["from"] = ["El parámetro 'from' es obligatorio (yyyy-MM-dd)."];
        }

        if (to is null)
        {
            errors["to"] = ["El parámetro 'to' es obligatorio (yyyy-MM-dd)."];
        }

        if (from > to)
        {
            errors["from"] = ["El parámetro 'from' debe ser anterior o igual a 'to'."];
        }

        if (errors.Count > 0)
        {
            return ResultError.Validation(errors);
        }

        var period = await shifts.ListByPeriodAsync(from!.Value, to!.Value, ct);
        var content = spreadsheet.Write(
            period.Select(s => new ShiftExportRow(s.Collaborator!.Email, s.Date, s.StartTime, s.EndTime)));

        var fileName = FormattableString.Invariant($"turnos_{from:yyyy-MM-dd}_{to:yyyy-MM-dd}.xlsx");

        return new ExportedFile(fileName, content);
    }
}
