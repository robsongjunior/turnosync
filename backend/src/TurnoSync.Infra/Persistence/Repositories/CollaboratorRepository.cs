using Microsoft.EntityFrameworkCore;
using TurnoSync.Application.Collaborators;
using TurnoSync.Domain.Collaborators;

namespace TurnoSync.Infra.Persistence.Repositories;

public sealed class CollaboratorRepository(TurnoSyncContext context)
    : BaseRepository<Collaborator>(context), ICollaboratorRepository
{
    public Task<bool> EmailExistsAsync(string email, int? excludeId, CancellationToken ct = default) =>
        Context.Collaborators.AnyAsync(c => c.Email == email && (excludeId == null || c.Id != excludeId), ct);

    public async Task<IReadOnlyList<Collaborator>> GetByEmailsAsync(
        IEnumerable<string> emails, CancellationToken ct = default)
    {
        var list = emails.Distinct().ToList();
        return await Context.Collaborators.AsNoTracking().Where(c => list.Contains(c.Email)).ToListAsync(ct);
    }
}
