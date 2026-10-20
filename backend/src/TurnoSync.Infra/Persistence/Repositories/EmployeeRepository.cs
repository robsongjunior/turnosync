using Microsoft.EntityFrameworkCore;
using TurnoSync.Application.Employees;
using TurnoSync.Domain.Employees;

namespace TurnoSync.Infra.Persistence.Repositories;

public sealed class EmployeeRepository(TurnoSyncContext context)
    : BaseRepository<Employee>(context), IEmployeeRepository
{
    public Task<bool> EmailExistsAsync(string email, int? excludeId, CancellationToken ct = default) =>
        Context.Employees.AnyAsync(c => c.Email == email && (excludeId == null || c.Id != excludeId), ct);

    public async Task<IReadOnlyList<Employee>> GetByEmailsAsync(
        IEnumerable<string> emails, CancellationToken ct = default)
    {
        var list = emails.Distinct().ToList();
        return await Context.Employees.AsNoTracking().Where(c => list.Contains(c.Email)).ToListAsync(ct);
    }
}
