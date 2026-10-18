using TurnoSync.Application.Common;
using TurnoSync.Domain.Shifts;

namespace TurnoSync.Application.Shifts;

public interface IShiftRepository : IRepository<Shift>
{
    /// <summary>Todos los turnos con su colaborador, en orden fijo: inicio del turno y luego nombre del colaborador.</summary>
    Task<IReadOnlyList<Shift>> ListWithCollaboratorAsync(CancellationToken ct = default);

    /// <summary>
    /// Los turnos del colaborador con Date entre from y to (inclusive), para comprobar solapamientos.
    /// excludeId omite el propio turno al actualizar.
    /// </summary>
    Task<IReadOnlyList<Shift>> ListForOverlapAsync(
        int collaboratorId, DateOnly from, DateOnly to, int? excludeId, CancellationToken ct = default);

    /// <summary>Los turnos con Date entre from y to (inclusive), con su colaborador, en el mismo orden que el listado.</summary>
    Task<IReadOnlyList<Shift>> ListByPeriodAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Los turnos del colaborador con seguimiento (para el borrado lógico en cascada).</summary>
    Task<IReadOnlyList<Shift>> ListByCollaboratorAsync(int collaboratorId, CancellationToken ct = default);
}
