using System.Globalization;
using TurnoSync.Application.Common;

namespace TurnoSync.Application.Shifts;

public sealed record ExportedFile(string FileName, byte[] Content);

public sealed class ShiftExportService(IShiftRepository shifts, IShiftSpreadsheet spreadsheet)
{
    /// <summary>
    /// Exporta los turnos cuya fecha (Date, la de inicio) está entre from y to, ambas inclusivas y obligatorias,
    /// opcionalmente de un solo colaborador, en el mismo orden que el listado y con las mismas columnas que la
    /// importación. No filtra por festivo: generar el archivo no depende de la API externa.
    /// </summary>
    public async Task<Result<ExportedFile>> ExportAsync(
        DateOnly? from, DateOnly? to, int? collaboratorId = null, CancellationToken ct = default)
    {
        if (ShiftPeriod.Validate(from, to, required: true) is { } invalid)
        {
            return invalid;
        }

        var period = await shifts.ListWithCollaboratorAsync(collaboratorId, from, to, ct);
        var content = spreadsheet.Write(
            period.Select(s => new ShiftExportRow(s.Collaborator!.Email, s.Date, s.StartTime, s.EndTime)));

        var fileName = FormattableString.Invariant($"turnos_{from:yyyy-MM-dd}_{to:yyyy-MM-dd}.xlsx");

        return new ExportedFile(fileName, content);
    }
}
