using TurnoSync.Application.Common;
using TurnoSync.Domain.Collaborators;

namespace TurnoSync.Application.Collaborators;

public interface ICollaboratorRepository : IRepository<Collaborator>
{
    /// <summary>Existe outro colaborador (não excluído) com este e-mail? O e-mail já deve vir normalizado.</summary>
    Task<bool> EmailExistsAsync(string email, int? excludeId, CancellationToken ct = default);

    /// <summary>Busca colaboradores pelos e-mails (usado pela importação). Os e-mails já devem vir normalizados.</summary>
    Task<IReadOnlyList<Collaborator>> GetByEmailsAsync(IEnumerable<string> emails, CancellationToken ct = default);
}
