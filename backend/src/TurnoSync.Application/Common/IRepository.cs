using TurnoSync.Domain.Common;

namespace TurnoSync.Application.Common;

public interface IRepository<T>
    where T : BaseEntity
{
    /// <summary>Returns the tracked entity (to update or delete it) or null if it does not exist.</summary>
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Lists without tracking (read-only).</summary>
    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);

    void Add(T entity);

    void AddRange(IEnumerable<T> entities);

    Task SaveChangesAsync(CancellationToken ct = default);
}
