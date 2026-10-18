namespace TurnoSync.Application.Collaborators;

// Request fields are nullable so that a missing field reaches the validator (a single source of
// error messages) instead of failing earlier, in model binding.
public sealed record CollaboratorRequest(string? Name, string? Email, string? CountryCode);

public sealed record CollaboratorResponse(int Id, string Name, string Email, string CountryCode);
