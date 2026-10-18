using TurnoSync.Domain.Collaborators;
using TurnoSync.Domain.Common;

namespace TurnoSync.Domain.Shifts;

public sealed class Shift : BaseEntity
{
    // Usado pelo EF Core; a criacao passa por Create.
    private Shift()
    {
    }

    public int CollaboratorId { get; private set; }

    public Collaborator? Collaborator { get; private set; }

    public DateOnly Date { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    /// <summary>Inicio absoluto do turno (Date + StartTime).</summary>
    public DateTime StartsAt { get; private set; }

    /// <summary>
    /// Fim absoluto do turno. Se EndTime for menor que StartTime, o turno atravessa a
    /// meia-noite e termina no dia seguinte.
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
        // O validator barra isso antes (erro de negocio); aqui so protegemos a invariante.
        if (startTime == endTime)
        {
            throw new ArgumentException("A shift cannot start and end at the same time.", nameof(endTime));
        }

        CollaboratorId = collaboratorId;
        Date = date;
        StartTime = startTime;
        EndTime = endTime;
        StartsAt = date.ToDateTime(startTime);
        EndsAt = (endTime > startTime ? date : date.AddDays(1)).ToDateTime(endTime);
    }

    /// <summary>
    /// A regra de sobreposicao do sistema: mesmo colaborador e intervalos [inicio, fim) que se
    /// cruzam. Turnos encostados (08-12 e 12-16) nao se sobrepoem.
    /// </summary>
    public bool Overlaps(Shift other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return CollaboratorId == other.CollaboratorId
            && StartsAt < other.EndsAt
            && other.StartsAt < EndsAt;
    }
}
