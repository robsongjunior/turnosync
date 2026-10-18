using Microsoft.EntityFrameworkCore;
using TurnoSync.Application.Common;
using TurnoSync.Domain.Common;

namespace TurnoSync.Infra.Persistence.Repositories;

public abstract class BaseRepository<T>(TurnoSyncContext context) : IRepository<T>
    where T : BaseEntity
{
    protected TurnoSyncContext Context { get; } = context;

    // FirstOrDefault e não Find: o Find ignora o query filter do soft delete.
    public Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Context.Set<T>().FirstOrDefaultAsync(e => e.Id == id, ct);

    public async Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default) =>
        await Context.Set<T>().AsNoTracking().ToListAsync(ct);

    public void Add(T entity) => Context.Set<T>().Add(entity);

    public void AddRange(IEnumerable<T> entities) => Context.Set<T>().AddRange(entities);

    public Task SaveChangesAsync(CancellationToken ct = default) => Context.SaveChangesAsync(ct);
}
