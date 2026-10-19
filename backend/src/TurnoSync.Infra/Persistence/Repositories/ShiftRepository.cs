using Microsoft.EntityFrameworkCore;
using TurnoSync.Application.Shifts;
using TurnoSync.Domain.Shifts;

namespace TurnoSync.Infra.Persistence.Repositories;

public sealed class ShiftRepository(TurnoSyncContext context)
    : BaseRepository<Shift>(context), IShiftRepository
{
    public async Task<IReadOnlyList<Shift>> ListWithCollaboratorAsync(
        int? collaboratorId = null, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default) =>
        await WithCollaboratorInOrder(Context.Shifts
                .AsNoTracking()
                .Where(s => (collaboratorId == null || s.CollaboratorId == collaboratorId)
                    && (from == null || s.Date >= from)
                    && (to == null || s.Date <= to)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Shift>> ListForOverlapAsync(
        int collaboratorId, DateOnly from, DateOnly to, int? excludeId, CancellationToken ct = default) =>
        await Context.Shifts
            .AsNoTracking()
            .Where(s => s.CollaboratorId == collaboratorId
                && s.Date >= from
                && s.Date <= to
                && (excludeId == null || s.Id != excludeId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Shift>> ListByCollaboratorAsync(
        int collaboratorId, CancellationToken ct = default) =>
        await Context.Shifts.Where(s => s.CollaboratorId == collaboratorId).ToListAsync(ct);

    private static IOrderedQueryable<Shift> WithCollaboratorInOrder(IQueryable<Shift> query) =>
        query.Include(s => s.Collaborator).OrderBy(s => s.StartsAt).ThenBy(s => s.Collaborator!.Name);
}
