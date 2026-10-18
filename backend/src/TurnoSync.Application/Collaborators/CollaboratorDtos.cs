namespace TurnoSync.Application.Collaborators;

// Os campos do request são anuláveis para que um campo ausente chegue ao validator
// (uma única fonte das mensagens de erro) em vez de falhar antes, no model binding.
public sealed record CollaboratorRequest(string? Name, string? Email, string? CountryCode);

public sealed record CollaboratorResponse(int Id, string Name, string Email, string CountryCode);
