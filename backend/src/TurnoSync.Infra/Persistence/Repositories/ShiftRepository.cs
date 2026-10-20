using Microsoft.EntityFrameworkCore;
using TurnoSync.Application.Shifts;
using TurnoSync.Domain.Shifts;

namespace TurnoSync.Infra.Persistence.Repositories;

public sealed class ShiftRepository(TurnoSyncContext context)
    : BaseRepository<Shift>(context), IShiftRepository
{
    public async Task<IReadOnlyList<Shift>> ListWithEmployeeAsync(
        int? employeeId = null, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default) =>
        await WithEmployeeInOrder(Context.Shifts
                .AsNoTracking()
                .Where(s => (employeeId == null || s.EmployeeId == employeeId)
                    && (from == null || s.Date >= from)
                    && (to == null || s.Date <= to)))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Shift>> ListForOverlapAsync(
        int employeeId, DateOnly from, DateOnly to, int? excludeId, CancellationToken ct = default) =>
        await Context.Shifts
            .AsNoTracking()
            .Where(s => s.EmployeeId == employeeId
                && s.Date >= from
                && s.Date <= to
                && (excludeId == null || s.Id != excludeId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Shift>> ListByEmployeeAsync(
        int employeeId, CancellationToken ct = default) =>
        await Context.Shifts.Where(s => s.EmployeeId == employeeId).ToListAsync(ct);

    private static IOrderedQueryable<Shift> WithEmployeeInOrder(IQueryable<Shift> query) =>
        query.Include(s => s.Employee).OrderBy(s => s.StartsAt).ThenBy(s => s.Employee!.Name);
}
