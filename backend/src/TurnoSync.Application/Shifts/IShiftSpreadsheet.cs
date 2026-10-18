using TurnoSync.Application.Common;

namespace TurnoSync.Application.Shifts;

/// <summary>
/// Una fila de datos de la hoja de cálculo, ya convertida. Las celdas que no se pudieron leer (fecha u hora
/// no válidas) llegan como null y se explican en <see cref="Errors"/>.
/// </summary>
/// <param name="RowNumber">Número de fila en la hoja (la primera fila de datos es la 2).</param>
public sealed record ShiftSheetRow(
    int RowNumber,
    string? Email,
    DateOnly? Date,
    TimeOnly? StartTime,
    TimeOnly? EndTime,
    IReadOnlyList<string> Errors);

public sealed record ShiftExportRow(string Email, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime);

/// <summary>
/// Lee y escribe el .xlsx de turnos. Columnas: Email, Fecha, HoraInicio, HoraFin (las del enunciado),
/// las mismas para importar y exportar.
/// </summary>
public interface IShiftSpreadsheet
{
    /// <summary>
    /// Lee la primera hoja y localiza los encabezados por nombre (sin distinguir mayúsculas, en cualquier orden).
    /// Las filas en blanco se ignoran. Falla si el archivo no es un .xlsx válido o faltan columnas.
    /// </summary>
    Result<IReadOnlyList<ShiftSheetRow>> Read(Stream content);

    /// <summary>Genera el .xlsx con la fecha y la hora como celdas nativas de Excel.</summary>
    byte[] Write(IEnumerable<ShiftExportRow> rows);
}
