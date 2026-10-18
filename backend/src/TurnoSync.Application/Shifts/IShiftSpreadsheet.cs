using TurnoSync.Application.Common;

namespace TurnoSync.Application.Shifts;

/// <summary>
/// A data row of the spreadsheet, already converted. Cells that could not be read (invalid date or time)
/// come as null and are explained in <see cref="Errors"/>.
/// </summary>
/// <param name="RowNumber">Row number in the sheet (the first data row is 2).</param>
public sealed record ShiftSheetRow(
    int RowNumber,
    string? Email,
    DateOnly? Date,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    IReadOnlyList<string> Errors);

public sealed record ShiftExportRow(string Email, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime);

/// <summary>
/// Reads and writes the shifts .xlsx. Columns: Email, Fecha, HoraInicio, HoraFin (those of the brief),
/// the same for import and export.
/// </summary>
public interface IShiftSpreadsheet
{
    /// <summary>
    /// Reads the first sheet, finding the headers by name (case-insensitive, in any order).
    /// Blank rows are ignored. Fails if the file is not a valid .xlsx or columns are missing.
    /// </summary>
    Result<IReadOnlyList<ShiftSheetRow>> Read(Stream content);

    /// <summary>Generates the .xlsx with date and time as native Excel cells.</summary>
    byte[] Write(IEnumerable<ShiftExportRow> rows);
}
