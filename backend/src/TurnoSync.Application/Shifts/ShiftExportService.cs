using System.Globalization;
using TurnoSync.Application.Common;

namespace TurnoSync.Application.Shifts;

public sealed record ExportedFile(string FileName, byte[] Content);

public sealed class ShiftExportService(IShiftRepository shifts, IShiftSpreadsheet spreadsheet)
{
    /// <summary>
    /// Exporta os turnos cuja data (Date, a de início) está entre from e to, ambos inclusivos e obrigatórios,
    /// na mesma ordem da listagem e com as mesmas colunas da importação.
    /// </summary>
    public async Task<Result<ExportedFile>> ExportAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (from is null)
        {
            errors["from"] = ["'from' is required (yyyy-MM-dd)."];
        }

        if (to is null)
        {
            errors["to"] = ["'to' is required (yyyy-MM-dd)."];
        }

        if (from > to)
        {
            errors["from"] = ["'from' must be on or before 'to'."];
        }

        if (errors.Count > 0)
        {
            return ResultError.Validation(errors);
        }

        var period = await shifts.ListByPeriodAsync(from!.Value, to!.Value, ct);
        var content = spreadsheet.Write(
            period.Select(s => new ShiftExportRow(s.Collaborator!.Email, s.Date, s.StartTime, s.EndTime)));

        var fileName = FormattableString.Invariant($"shifts_{from:yyyy-MM-dd}_{to:yyyy-MM-dd}.xlsx");

        return new ExportedFile(fileName, content);
    }
}
