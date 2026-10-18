using FluentValidation;
using TurnoSync.Application.Common;
using TurnoSync.Application.Shifts;
using TurnoSync.Domain.Collaborators;

namespace TurnoSync.Application.Collaborators;

public sealed class CollaboratorService(
    ICollaboratorRepository collaborators,
    IShiftRepository shifts,
    IValidator<CollaboratorRequest> validator)
{
    public async Task<IReadOnlyList<CollaboratorResponse>> ListAsync(CancellationToken ct = default) =>
        (await collaborators.ListAsync(ct)).Select(ToResponse).ToList();

    public async Task<Result<CollaboratorResponse>> GetAsync(int id, CancellationToken ct = default)
    {
        var collaborator = await collaborators.GetByIdAsync(id, ct);

        return collaborator is null ? NotFound(id) : ToResponse(collaborator);
    }

    public async Task<Result<CollaboratorResponse>> CreateAsync(
        CollaboratorRequest request, CancellationToken ct = default)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return validation.ToResultError();
        }

        if (await EmailInUseAsync(request.Email!, excludeId: null, ct) is { } conflict)
        {
            return conflict;
        }

        var collaborator = Collaborator.Create(request.Name!, request.Email!, request.CountryCode!);
        collaborators.Add(collaborator);
        await collaborators.SaveChangesAsync(ct);

        return ToResponse(collaborator);
    }

    public async Task<Result<CollaboratorResponse>> UpdateAsync(
        int id, CollaboratorRequest request, CancellationToken ct = default)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return validation.ToResultError();
        }

        var collaborator = await collaborators.GetByIdAsync(id, ct);
        if (collaborator is null)
        {
            return NotFound(id);
        }

        if (await EmailInUseAsync(request.Email!, excludeId: id, ct) is { } conflict)
        {
            return conflict;
        }

        collaborator.Update(request.Name!, request.Email!, request.CountryCode!);
        await collaborators.SaveChangesAsync(ct);

        return ToResponse(collaborator);
    }

    /// <summary>Soft-deletes the collaborator and all of their shifts in a single SaveChanges (atomic).</summary>
    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var collaborator = await collaborators.GetByIdAsync(id, ct);
        if (collaborator is null)
        {
            return NotFound(id);
        }

        foreach (var shift in await shifts.ListByCollaboratorAsync(id, ct))
        {
            shift.Delete();
        }

        collaborator.Delete();

        // Both repositories share the same DbContext (request scope).
        await collaborators.SaveChangesAsync(ct);

        return Result.Success();
    }

    private async Task<ResultError?> EmailInUseAsync(string email, int? excludeId, CancellationToken ct)
    {
        var normalized = Collaborator.NormalizeEmail(email);

        return await collaborators.EmailExistsAsync(normalized, excludeId, ct)
            ? ResultError.Conflict($"A collaborator with the email '{normalized}' already exists.")
            : null;
    }

    private static ResultError NotFound(int id) => ResultError.NotFound($"Collaborator {id} was not found.");

    private static CollaboratorResponse ToResponse(Collaborator c) => new(c.Id, c.Name, c.Email, c.CountryCode);
}
