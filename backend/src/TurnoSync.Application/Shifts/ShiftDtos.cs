namespace TurnoSync.Application.Shifts;

// Campos anuláveis pelo mesmo motivo do CollaboratorRequest. Para valores como TimeOnly
// isso também distingue "ausente" de 00:00, que é um horário válido.
public sealed record ShiftRequest(int? CollaboratorId, DateOnly? Date, TimeOnly? StartTime, TimeOnly? EndTime);

public sealed record ShiftResponse(
    int Id,
    int CollaboratorId,
    string CollaboratorName,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime);

/// <summary>
/// Turno na listagem. <see cref="IsHoliday"/> é <c>null</c> (desconhecido, não "falso") quando a API de
/// feriados falhou para o país do colaborador.
/// </summary>
public sealed record ShiftListItem(
    int Id,
    int CollaboratorId,
    string CollaboratorName,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool? IsHoliday);

/// <param name="Items">Turnos na ordem fixa: início do turno e depois nome do colaborador.</param>
/// <param name="HolidaysAvailable">False se a consulta de feriados falhou para algum país.</param>
public sealed record ShiftListResponse(IReadOnlyList<ShiftListItem> Items, bool HolidaysAvailable);
