namespace TurnoSync.Application.Shifts;

// Anulables por la misma razón que CollaboratorRequest. En valores como TimeOnly, además,
// permite distinguir "ausente" de 00:00, que es una hora válida.
public sealed record ShiftRequest(int? CollaboratorId, DateOnly? Date, TimeOnly? StartTime, TimeOnly? EndTime);

public sealed record ShiftResponse(
    int Id,
    int CollaboratorId,
    string CollaboratorName,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime);

/// <summary>
/// Un turno del listado. <see cref="IsHoliday"/> es <c>null</c> (desconocido, no "false") cuando la API de
/// festivos falló para el país del colaborador.
/// </summary>
public sealed record ShiftListItem(
    int Id,
    int CollaboratorId,
    string CollaboratorName,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool? IsHoliday);

/// <param name="Items">Turnos en orden fijo: inicio del turno y luego nombre del colaborador.</param>
/// <param name="HolidaysAvailable">False si la consulta de festivos falló para algún país.</param>
public sealed record ShiftListResponse(IReadOnlyList<ShiftListItem> Items, bool HolidaysAvailable);
