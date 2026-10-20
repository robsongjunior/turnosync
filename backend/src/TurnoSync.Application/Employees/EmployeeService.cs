using FluentValidation;
using TurnoSync.Application.Common;
using TurnoSync.Application.Shifts;
using TurnoSync.Domain.Employees;

namespace TurnoSync.Application.Employees;

public sealed class EmployeeService(
    IEmployeeRepository employees,
    IShiftRepository shifts,
    IValidator<EmployeeRequest> validator)
{
    public async Task<IReadOnlyList<EmployeeResponse>> ListAsync(CancellationToken ct = default) =>
        (await employees.ListAsync(ct)).Select(ToResponse).ToList();

    public async Task<Result<EmployeeResponse>> GetAsync(int id, CancellationToken ct = default)
    {
        var employee = await employees.GetByIdAsync(id, ct);

        return employee is null ? NotFound(id) : ToResponse(employee);
    }

    public async Task<Result<EmployeeResponse>> CreateAsync(
        EmployeeRequest request, CancellationToken ct = default)
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

        var employee = Employee.Create(request.Name!, request.Email!, request.CountryCode!);
        employees.Add(employee);
        await employees.SaveChangesAsync(ct);

        return ToResponse(employee);
    }

    public async Task<Result<EmployeeResponse>> UpdateAsync(
        int id, EmployeeRequest request, CancellationToken ct = default)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return validation.ToResultError();
        }

        var employee = await employees.GetByIdAsync(id, ct);
        if (employee is null)
        {
            return NotFound(id);
        }

        if (await EmailInUseAsync(request.Email!, excludeId: id, ct) is { } conflict)
        {
            return conflict;
        }

        employee.Update(request.Name!, request.Email!, request.CountryCode!);
        await employees.SaveChangesAsync(ct);

        return ToResponse(employee);
    }

    /// <summary>Borra lógicamente al colaborador y todos sus turnos en un único SaveChanges (atómico).</summary>
    public async Task<Result> DeleteAsync(int id, CancellationToken ct = default)
    {
        var employee = await employees.GetByIdAsync(id, ct);
        if (employee is null)
        {
            return NotFound(id);
        }

        foreach (var shift in await shifts.ListByEmployeeAsync(id, ct))
        {
            shift.Delete();
        }

        employee.Delete();

        // Ambos repositorios comparten el mismo DbContext (ámbito de la solicitud).
        await employees.SaveChangesAsync(ct);

        return Result.Success();
    }

    private async Task<ResultError?> EmailInUseAsync(string email, int? excludeId, CancellationToken ct)
    {
        var normalized = Employee.NormalizeEmail(email);

        return await employees.EmailExistsAsync(normalized, excludeId, ct)
            ? ResultError.Conflict($"Ya existe un colaborador con el email '{normalized}'.")
            : null;
    }

    private static ResultError NotFound(int id) => ResultError.NotFound($"No se encontró el colaborador {id}.");

    private static EmployeeResponse ToResponse(Employee c) => new(c.Id, c.Name, c.Email, c.CountryCode);
}
