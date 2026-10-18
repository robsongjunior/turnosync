using TurnoSync.Domain.Common;

namespace TurnoSync.Application.Common;

public interface IRepository<T>
    where T : BaseEntity
{
    /// <summary>Devuelve la entidad con seguimiento (para actualizarla o borrarla) o null si no existe.</summary>
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Lista sin seguimiento (solo lectura).</summary>
    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);

    void Add(T entity);

    void AddRange(IEnumerable<T> entities);

    Task SaveChangesAsync(CancellationToken ct = default);
}
