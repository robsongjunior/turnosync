using TurnoSync.Domain.Collaborators;
using TurnoSync.Domain.Common;

namespace TurnoSync.Domain.Shifts;

public sealed class Shift : BaseEntity
{
    // Used by EF Core; creation goes through Create.
    private Shift()
    {
    }

    public int CollaboratorId { get; private set; }

    public Collaborator? Collaborator { get; private set; }

    public DateOnly Date { get; private set; }

    public TimeOnly StartTime { get; private set; }

    public TimeOnly EndTime { get; private set; }

    /// <summary>Absolute start of the shift (Date + StartTime).</summary>
    public DateTime StartsAt { get; private set; }

    /// <summary>
    /// Absolute end of the shift. If EndTime is earlier than StartTime, the shift crosses
    /// midnight and ends the next day.
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
        // The validator rejects this earlier (a business error); here we only protect the invariant.
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
    /// The system's overlap rule: same collaborator and intersecting [start, end) intervals.
    /// Shifts that merely touch (08-12 and 12-16) do not overlap.
    /// </summary>
    public bool Overlaps(Shift other)
    {
        ArgumentNullException.ThrowIfNull(other);

        return CollaboratorId == other.CollaboratorId
            && StartsAt < other.EndsAt
            && other.StartsAt < EndsAt;
    }
}
