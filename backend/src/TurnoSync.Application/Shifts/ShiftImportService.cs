using System.Globalization;
using FluentValidation;
using TurnoSync.Application.Collaborators;
using TurnoSync.Application.Common;
using TurnoSync.Domain.Collaborators;
using TurnoSync.Domain.Shifts;

namespace TurnoSync.Application.Shifts;

public sealed class ShiftImportService(
    IShiftSpreadsheet spreadsheet,
    ICollaboratorRepository collaborators,
    IShiftRepository shifts,
    IValidator<ShiftRequest> validator)
{
    private const string XlsxExtension = ".xlsx";

    /// <summary>
    /// Valida la hoja de cálculo fila a fila. Las filas válidas se importan (un único SaveChanges, atómico) y
    /// las inválidas vuelven en el informe con su motivo. Una fila con error no detiene a las demás.
    /// </summary>
    public async Task<Result<ShiftImportReport>> ImportAsync(string fileName, Stream content, CancellationToken ct = default)
    {
        if (!string.Equals(Path.GetExtension(fileName), XlsxExtension, StringComparison.OrdinalIgnoreCase))
        {
            return ResultError.Validation("file", "Solo se admiten archivos .xlsx.");
        }

        var read = spreadsheet.Read(content);
        if (!read.IsSuccess)
        {
            return read.Error!;
        }

        var rows = read.Value!;
        var collaboratorsByEmail = await LoadCollaboratorsAsync(rows, ct);
        var existingByCollaborator = await LoadExistingShiftsAsync(rows, collaboratorsByEmail, ct);

        var imported = new List<ImportedRow>();
        var failed = new List<FailedRow>();
        var accepted = new List<(int Row, Shift Shift)>();

        foreach (var row in rows)
        {
            var (shift, reasons) = await EvaluateAsync(row, collaboratorsByEmail, existingByCollaborator, accepted, ct);

            if (shift is not null)
            {
                accepted.Add((row.RowNumber, shift));
                imported.Add(new ImportedRow(row.RowNumber, row.Email!));
            }
            else
            {
                failed.Add(new FailedRow(row.RowNumber, row.Email, reasons));
            }
        }

        if (accepted.Count > 0)
        {
            shifts.AddRange(accepted.Select(a => a.Shift));
            await shifts.SaveChangesAsync(ct);
        }

        return new ShiftImportReport(imported, failed);
    }

    private async Task<(Shift? Shift, List<string> Reasons)> EvaluateAsync(
        ShiftSheetRow row,
        Dictionary<string, Collaborator> collaboratorsByEmail,
        Dictionary<int, IReadOnlyList<Shift>> existingByCollaborator,
        List<(int Row, Shift Shift)> accepted,
        CancellationToken ct)
    {
        var reasons = new List<string>(row.Errors);

        Collaborator? collaborator = null;
        if (string.IsNullOrWhiteSpace(row.Email))
        {
            reasons.Add("El email es obligatorio.");
        }
        else if (!collaboratorsByEmail.TryGetValue(Collaborator.NormalizeEmail(row.Email), out collaborator))
        {
            reasons.Add($"No existe ningún colaborador con el email '{row.Email.Trim()}'.");
        }

        // Las celdas ilegibles ya las explicó la hoja de cálculo; validar de nuevo repetiría el problema.
        if (row.Errors.Count == 0)
        {
            var request = new ShiftRequest(collaborator?.Id, row.Date, row.StartTime, row.EndTime);
            var validation = await validator.ValidateAsync(request, ct);

            // Sin colaborador el motivo ya es el email; no se añade "CollaboratorId vacío".
            reasons.AddRange(validation.Errors
                .Where(e => collaborator is not null || e.PropertyName != nameof(ShiftRequest.CollaboratorId))
                .Select(e => e.ErrorMessage));
        }

        if (reasons.Count > 0)
        {
            return (null, reasons);
        }

        var shift = Shift.Create(collaborator!.Id, row.Date!.Value, row.StartTime!.Value, row.EndTime!.Value);

        // La misma regla que la API (Shift.Overlaps): contra la base de datos y contra las filas ya aceptadas del archivo.
        if (existingByCollaborator.TryGetValue(collaborator.Id, out var existing)
            && existing.FirstOrDefault(shift.Overlaps) is { } clash)
        {
            reasons.Add($"Se solapa con un turno existente ({Describe(clash)}).");
        }

        var previousRow = accepted.FirstOrDefault(a => shift.Overlaps(a.Shift));
        if (previousRow.Shift is not null)
        {
            reasons.Add($"Se solapa con la fila {previousRow.Row}.");
        }

        return reasons.Count > 0 ? (null, reasons) : (shift, reasons);
    }

    private async Task<Dictionary<string, Collaborator>> LoadCollaboratorsAsync(
        IReadOnlyList<ShiftSheetRow> rows, CancellationToken ct)
    {
        var emails = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Email))
            .Select(r => Collaborator.NormalizeEmail(r.Email!))
            .Distinct()
            .ToList();

        if (emails.Count == 0)
        {
            return [];
        }

        return (await collaborators.GetByEmailsAsync(emails, ct)).ToDictionary(c => c.Email);
    }

    /// <summary>Una consulta por colaborador, que cubre el rango de fechas de sus filas (±1 día por los turnos nocturnos).</summary>
    private async Task<Dictionary<int, IReadOnlyList<Shift>>> LoadExistingShiftsAsync(
        IReadOnlyList<ShiftSheetRow> rows, Dictionary<string, Collaborator> collaboratorsByEmail, CancellationToken ct)
    {
        var ranges = rows
            .Where(r => r.Date is not null && !string.IsNullOrWhiteSpace(r.Email)
                && collaboratorsByEmail.ContainsKey(Collaborator.NormalizeEmail(r.Email)))
            .GroupBy(r => collaboratorsByEmail[Collaborator.NormalizeEmail(r.Email!)].Id)
            .Select(g => (CollaboratorId: g.Key, From: g.Min(r => r.Date!.Value), To: g.Max(r => r.Date!.Value)));

        var existing = new Dictionary<int, IReadOnlyList<Shift>>();
        foreach (var (collaboratorId, from, to) in ranges)
        {
            existing[collaboratorId] = await shifts.ListForOverlapAsync(
                collaboratorId, from.AddDays(-1), to.AddDays(1), excludeId: null, ct);
        }

        return existing;
    }

    private static string Describe(Shift shift) =>
        FormattableString.Invariant($"{shift.Date:yyyy-MM-dd} {shift.StartTime:HH:mm}-{shift.EndTime:HH:mm}");
}
