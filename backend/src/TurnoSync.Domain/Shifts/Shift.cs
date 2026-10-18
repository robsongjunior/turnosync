using TurnoSync.Domain.Collaborators;
using TurnoSync.Domain.Common;

namespace TurnoSync.Domain.Shifts;

public sealed class Shift : BaseEntity
{
    // Lo usa EF Core; la creación pasa por Create.
    private Shift()
    {
    }

    public int CollaboratorId { get; private set; }

    public Collaborator? Collaborator { get; private set; }

    public DateOnly Date { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    /// <summary>Inicio absoluto del turno (Date + StartTime).</summary>
    public DateTime StartsAt { get; private set; }

    /// <summary>
    /// Fin absoluto del turno. Si EndTime es anterior a StartTime, el turno cruza la
    /// medianoche y termina al día siguiente.
    /// </summary>
    public DateTime EndsAt { get; private set; }

    public static Shift Create(int collaboratorId, DateOnly date, TimeOnly startTime, TimeOnly endTime)
    {
        var shift = new Shift();
        shift.Update(collaboratorId, date, startTime, endTime);
        return shift;
    }

    public void Update(int collaboratorId, DateOnly date, TimeOnly startTime, TimeOnly endTime)
    {
        // El validador lo rechaza antes (como error de negocio); aquí solo se protege la invariante.
        if (startTime == endTime)
        {
            throw new ArgumentException("Un turno no puede empezar y terminar a la misma hora.", nameof(endTime));
        }

        CollaboratorId = collaboratorId;
        Date = date;
        StartTime = startTime;
        EndTime = endTime;
        StartsAt = date.ToDateTime(startTime);
        EndsAt = (endTime > startTime ? date : date.AddDays(1)).ToDateTime(endTime);
    }

    /// <summary>
    /// La regla de solapamiento del sistema: mismo colaborador e intervalos [inicio, fin) que se cruzan.
    /// Los turnos que solo se tocan (08-12 y 12-16) no se solapan.
    /// </summary>
    public bool Overlaps(Shift other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return CollaboratorId == other.CollaboratorId
            && StartsAt < other.EndsAt
            && other.StartsAt < EndsAt;
    }
}
