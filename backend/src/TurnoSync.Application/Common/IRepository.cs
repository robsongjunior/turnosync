using TurnoSync.Domain.Common;

namespace TurnoSync.Application.Common;

public interface IRepository<T>
    where T : BaseEntity
{
    /// <summary>Devolve a entidade rastreada (para atualizar ou excluir) ou null se não existir.</summary>
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Lista sem rastreamento (somente leitura).</summary>
    Task<IReadOnlyList<T>> ListAsync(CancellationToken ct = default);

    void Add(T entity);

    void AddRange(IEnumerable<T> entities);

    Task SaveChangesAsync(CancellationToken ct = default);
}
