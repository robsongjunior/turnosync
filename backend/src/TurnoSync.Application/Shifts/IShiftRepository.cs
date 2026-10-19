using TurnoSync.Application.Common;
using TurnoSync.Domain.Shifts;

namespace TurnoSync.Application.Shifts;

public interface IShiftRepository : IRepository<Shift>
{
    /// <summary>
    /// Los turnos con su colaborador, en orden fijo: inicio del turno y luego nombre del colaborador.
    /// Cada filtro es opcional; from y to se aplican sobre Date (la fecha de inicio) y son inclusivos.
    /// </summary>
    Task<IReadOnlyList<Shift>> ListWithCollaboratorAsync(
        int? collaboratorId = null, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default);

    /// <summary>
    /// Los turnos del colaborador con Date entre from y to (inclusive), para comprobar solapamientos.
    /// excludeId omite el propio turno al actualizar.
    /// </summary>
    Task<IReadOnlyList<Shift>> ListForOverlapAsync(
        int collaboratorId, DateOnly from, DateOnly to, int? excludeId, CancellationToken ct = default);

    /// <summary>Los turnos del colaborador con seguimiento (para el borrado lógico en cascada).</summary>
    Task<IReadOnlyList<Shift>> ListByCollaboratorAsync(int collaboratorId, CancellationToken ct = default);
}
