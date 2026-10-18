namespace TurnoSync.Application.Shifts;

// Nullable for the same reason as CollaboratorRequest. For values like TimeOnly this also
// tells "missing" apart from 00:00, which is a valid time.
public sealed record ShiftRequest(int? CollaboratorId, DateOnly? Date, TimeOnly? StartTime, TimeOnly? EndTime);

public sealed record ShiftResponse(
    int Id,
    int CollaboratorId,
    string CollaboratorName,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime);

/// <summary>
/// A shift in the listing. <see cref="IsHoliday"/> is <c>null</c> (unknown, not "false") when the holiday
/// API failed for the collaborator's country.
/// </summary>
public sealed record ShiftListItem(
    int Id,
    int CollaboratorId,
    string CollaboratorName,
    DateOnly Date,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool? IsHoliday);

/// <param name="Items">Shifts in a fixed order: shift start, then collaborator name.</param>
/// <param name="HolidaysAvailable">False if the holiday lookup failed for any country.</param>
public sealed record ShiftListResponse(IReadOnlyList<ShiftListItem> Items, bool HolidaysAvailable);
