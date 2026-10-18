using TurnoSync.Application.Common;
using TurnoSync.Domain.Collaborators;

namespace TurnoSync.Application.Collaborators;

public interface ICollaboratorRepository : IRepository<Collaborator>
{
    /// <summary>¿Existe otro colaborador (no borrado) con este email? El email debe venir ya normalizado.</summary>
    Task<bool> EmailExistsAsync(string email, int? excludeId, CancellationToken ct = default);

    /// <summary>Busca colaboradores por email (lo usa la importación). Los emails deben venir ya normalizados.</summary>
    Task<IReadOnlyList<Collaborator>> GetByEmailsAsync(IEnumerable<string> emails, CancellationToken ct = default);
}
