using TurnoSync.Application.Common;
using TurnoSync.Domain.Collaborators;

namespace TurnoSync.Application.Collaborators;

public interface ICollaboratorRepository : IRepository<Collaborator>
{
    /// <summary>Is there another (non-deleted) collaborator with this email? The email must already be normalized.</summary>
    Task<bool> EmailExistsAsync(string email, int? excludeId, CancellationToken ct = default);

    /// <summary>Finds collaborators by email (used by the import). The emails must already be normalized.</summary>
    Task<IReadOnlyList<Collaborator>> GetByEmailsAsync(IEnumerable<string> emails, CancellationToken ct = default);
}
