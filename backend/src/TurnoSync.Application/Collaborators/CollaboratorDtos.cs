namespace TurnoSync.Application.Collaborators;

// Los campos de la solicitud son anulables para que un campo ausente llegue al validador (la única fuente
// de mensajes de error) en lugar de fallar antes, en el enlace de modelo.
public sealed record CollaboratorRequest(string? Name, string? Email, string? CountryCode);

public sealed record CollaboratorResponse(int Id, string Name, string Email, string CountryCode);
