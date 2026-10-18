using TurnoSync.Application.Common;
using TurnoSync.Domain.Shifts;

namespace TurnoSync.Application.Shifts;

public interface IShiftRepository : IRepository<Shift>
{
    /// <summary>Todos os turnos com o colaborador, na ordem fixa: início do turno e depois nome do colaborador.</summary>
    Task<IReadOnlyList<Shift>> ListWithCollaboratorAsync(CancellationToken ct = default);

    /// <summary>
    /// Turnos do colaborador com Date entre from e to (inclusivos), para checar sobreposição.
    /// excludeId ignora o próprio turno ao atualizar.
    /// </summary>
    Task<IReadOnlyList<Shift>> ListForOverlapAsync(
        int collaboratorId, DateOnly from, DateOnly to, int? excludeId, CancellationToken ct = default);

    /// <summary>Turnos com Date entre from e to (inclusivos), com o colaborador, na mesma ordem da listagem.</summary>
    Task<IReadOnlyList<Shift>> ListByPeriodAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Turnos rastreados do colaborador (para o soft delete em cascata).</summary>
    Task<IReadOnlyList<Shift>> ListByCollaboratorAsync(int collaboratorId, CancellationToken ct = default);
}
