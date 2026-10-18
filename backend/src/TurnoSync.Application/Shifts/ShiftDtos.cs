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
