using System.Globalization;
using FluentValidation;
using TurnoSync.Application.Collaborators;
using TurnoSync.Application.Common;
using TurnoSync.Domain.Shifts;

namespace TurnoSync.Application.Shifts;

public sealed class ShiftService(
    IShiftRepository shifts,
    ICollaboratorRepository collaborators,
    IValidator<ShiftRequest> validator)
{
    public async Task<IReadOnlyList<ShiftResponse>> ListAsync(CancellationToken ct = default) =>
        (await shifts.ListWithCollaboratorAsync(ct)).Select(s => ToResponse(s, s.Collaborator!.Name)).ToList();

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

        // A checagem usa um candidato para não alterar a entidade rastreada antes de saber se o conflito existe.
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
    /// Como um turno dura menos de 24h, só os turnos do dia anterior ao seguinte podem se sobrepor;
    /// quem decide é a entidade (<see cref="Shift.Overlaps"/>).
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
