using TurnoSync.Application.Common;
using TurnoSync.Domain.Shifts;

namespace TurnoSync.Application.Shifts;

public interface IShiftRepository : IRepository<Shift>
{
    /// <summary>All shifts with their collaborator, in a fixed order: shift start, then collaborator name.</summary>
    Task<IReadOnlyList<Shift>> ListWithCollaboratorAsync(CancellationToken ct = default);

    /// <summary>
    /// The collaborator's shifts with Date between from and to (inclusive), to check for overlaps.
    /// excludeId skips the shift itself when updating.
    /// </summary>
    Task<IReadOnlyList<Shift>> ListForOverlapAsync(
        int collaboratorId, DateOnly from, DateOnly to, int? excludeId, CancellationToken ct = default);

    /// <summary>Shifts with Date between from and to (inclusive), with their collaborator, in the same order as the listing.</summary>
    Task<IReadOnlyList<Shift>> ListByPeriodAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>The collaborator's tracked shifts (for the cascading soft delete).</summary>
    Task<IReadOnlyList<Shift>> ListByCollaboratorAsync(int collaboratorId, CancellationToken ct = default);
}
