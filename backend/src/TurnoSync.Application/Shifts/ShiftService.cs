using System.Globalization;
using FluentValidation;
using TurnoSync.Application.Collaborators;
using TurnoSync.Application.Common;
using TurnoSync.Application.Holidays;
using TurnoSync.Domain.Shifts;

namespace TurnoSync.Application.Shifts;

public sealed class ShiftService(
    IShiftRepository shifts,
    ICollaboratorRepository collaborators,
    IHolidayProvider holidays,
    IValidator<ShiftRequest> validator)
{
    /// <summary>
    /// Lists the shifts, flagging the ones that start on a national public holiday of the collaborator's country.
    /// One lookup per (country, year) present in the listing. If a lookup fails, that country's shifts
    /// come with IsHoliday null and HolidaysAvailable false; the listing never fails because of it.
    /// </summary>
    public async Task<ShiftListResponse> ListAsync(CancellationToken ct = default)
    {
        var list = await shifts.ListWithCollaboratorAsync(ct);

        var keys = list.Select(s => (s.Collaborator!.CountryCode, s.Date.Year)).Distinct().ToList();
        var lookups = await Task.WhenAll(keys.Select(async key =>
            (Key: key, Holidays: await holidays.GetNationalHolidaysAsync(key.CountryCode, key.Year, ct))));
        var holidaysByKey = lookups.ToDictionary(l => l.Key, l => l.Holidays);

        var items = list.Select(s =>
        {
            var countryHolidays = holidaysByKey[(s.Collaborator!.CountryCode, s.Date.Year)];
            bool? isHoliday = countryHolidays?.Contains(s.Date);

            return new ShiftListItem(
                s.Id, s.CollaboratorId, s.Collaborator.Name, s.Date, s.StartTime, s.EndTime, isHoliday);
        }).ToList();

        return new ShiftListResponse(items, HolidaysAvailable: holidaysByKey.Values.All(h => h is not null));
    }

    public async Task<Result<ShiftResponse>> GetAsync(int id, CancellationToken ct = default)
    {
        var shift = await shifts.GetByIdAsync(id, ct);
        if (shift is null)
        {
            return NotFound(id);
        }

        var collaborator = await collaborators.GetByIdAsync(shift.CollaboratorId, ct);

        return ToResponse(shift, collaborator!.Name);
    }

    public async Task<Result<ShiftResponse>> CreateAsync(ShiftRequest request, CancellationToken ct = default)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return validation.ToResultError();
        }

        var collaborator = await collaborators.GetByIdAsync(request.CollaboratorId!.Value, ct);
        if (collaborator is null)
        {
            return CollaboratorNotFound(request.CollaboratorId.Value);
        }

        var shift = Shift.Create(collaborator.Id, request.Date!.Value, request.StartTime!.Value, request.EndTime!.Value);

        if (await FindOverlapAsync(shift, excludeId: null, ct) is { } existing)
        {
            return OverlapConflict(collaborator.Name, existing);
        }

        shifts.Add(shift);
        await shifts.SaveChangesAsync(ct);

        return ToResponse(shift, collaborator.Name);
    }

    public async Task<Result<ShiftResponse>> UpdateAsync(int id, ShiftRequest request, CancellationToken ct = default)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return validation.ToResultError();
        }

        var shift = await shifts.GetByIdAsync(id, ct);
        if (shift is null)
        {
            return NotFound(id);
        }

        var collaborator = await collaborators.GetByIdAsync(request.CollaboratorId!.Value, ct);
        if (collaborator is null)
        {
            return CollaboratorNotFound(request.CollaboratorId.Value);
        }

        // The check uses a candidate so the tracked entity is not changed before we know whether there is a conflict.
        var candidate = Shift.Create(collaborator.Id, request.Date!.Value, request.StartTime!.Value, request.EndTime!.Value);

        if (await FindOverlapAsync(candidate, excludeId: id, ct) is { } existing)
        {
            return OverlapConflict(collaborator.Name, existing);
        }

        shift.Update(collaborator.Id, candidate.Date, candidate.StartTime, candidate.EndTime);
        await shifts.SaveChangesAsync(ct);

        return ToResponse(shift, collaborator.Name);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var shift = await shifts.GetByIdAsync(id, ct);
        if (shift is null)
        {
            return NotFound(id);
        }

        shift.Delete();
        await shifts.SaveChangesAsync(ct);

        return Result.Success();
    }

    /// <summary>
    /// Since a shift lasts less than 24h, only shifts from the previous to the next day can overlap it;
    /// the entity decides (<see cref="Shift.Overlaps"/>).
    /// </summary>
    private async Task<Shift?> FindOverlapAsync(Shift candidate, int? excludeId, CancellationToken ct)
    {
        var nearby = await shifts.ListForOverlapAsync(
            candidate.CollaboratorId, candidate.Date.AddDays(-1), candidate.Date.AddDays(1), excludeId, ct);

        return nearby.FirstOrDefault(candidate.Overlaps);
    }

    private static ResultError OverlapConflict(string collaboratorName, Shift existing) =>
        ResultError.Conflict($"The shift overlaps an existing shift of {collaboratorName} ({Describe(existing)}).");

    private static ResultError NotFound(int id) => ResultError.NotFound($"Shift {id} was not found.");

    private static ResultError CollaboratorNotFound(int id) =>
        ResultError.NotFound($"Collaborator {id} was not found.");

    private static string Describe(Shift shift) =>
        FormattableString.Invariant($"{shift.Date:yyyy-MM-dd} {shift.StartTime:HH:mm}-{shift.EndTime:HH:mm}");

    private static ShiftResponse ToResponse(Shift s, string collaboratorName) =>
        new(s.Id, s.CollaboratorId, collaboratorName, s.Date, s.StartTime, s.EndTime);
}
