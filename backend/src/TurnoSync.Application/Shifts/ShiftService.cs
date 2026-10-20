using System.Globalization;
using FluentValidation;
using TurnoSync.Application.Employees;
using TurnoSync.Application.Common;
using TurnoSync.Application.Holidays;
using TurnoSync.Domain.Shifts;

namespace TurnoSync.Application.Shifts;

public sealed class ShiftService(
    IShiftRepository shifts,
    IEmployeeRepository employees,
    IHolidayProvider holidays,
    IValidator<ShiftRequest> validator)
{
    /// <summary>
    /// Lista los turnos que cumplen el filtro y marca los que empiezan en un festivo nacional del país del colaborador.
    /// El colaborador y el período se filtran en la base de datos; el festivo, después de calcularlo.
    /// Una consulta por cada (país, año) presente tras filtrar por colaborador y período. Si una consulta falla, los
    /// turnos de ese país salen con IsHoliday null y HolidaysAvailable false; el listado nunca falla por ello.
    /// </summary>
    public async Task<Result<ShiftListResponse>> ListAsync(ShiftListFilter filter, CancellationToken ct = default)
    {
        if (ShiftPeriod.Validate(filter.From, filter.To, required: false) is { } invalid)
        {
            return invalid;
        }

        var list = await shifts.ListWithEmployeeAsync(filter.EmployeeId, filter.From, filter.To, ct);

        var keys = list.Select(s => (s.Employee!.CountryCode, s.Date.Year)).Distinct().ToList();
        var lookups = await Task.WhenAll(keys.Select(async key =>
            (Key: key, Holidays: await holidays.GetNationalHolidaysAsync(key.CountryCode, key.Year, ct))));
        var holidaysByKey = lookups.ToDictionary(l => l.Key, l => l.Holidays);

        var items = list.Select(s =>
        {
            var countryHolidays = holidaysByKey[(s.Employee!.CountryCode, s.Date.Year)];
            bool? isHoliday = countryHolidays?.Contains(s.Date);

            return new ShiftListItem(
                s.Id, s.EmployeeId, s.Employee.Name, s.Date, s.StartTime, s.EndTime, isHoliday);
        })
        .Where(i => filter.Holiday is null || i.IsHoliday == filter.Holiday)
        .ToList();

        return new ShiftListResponse(items, HolidaysAvailable: holidaysByKey.Values.All(h => h is not null));
    }

    public async Task<Result<ShiftResponse>> GetAsync(int id, CancellationToken ct = default)
    {
        var shift = await shifts.GetByIdAsync(id, ct);
        if (shift is null)
        {
            return NotFound(id);
        }

        var employee = await employees.GetByIdAsync(shift.EmployeeId, ct);

        return ToResponse(shift, employee!.Name);
    }

    public async Task<Result<ShiftResponse>> CreateAsync(ShiftRequest request, CancellationToken ct = default)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return validation.ToResultError();
        }

        var employee = await employees.GetByIdAsync(request.EmployeeId!.Value, ct);
        if (employee is null)
        {
            return EmployeeNotFound(request.EmployeeId.Value);
        }

        var shift = Shift.Create(employee.Id, request.Date!.Value, request.StartTime!.Value, request.EndTime!.Value);

        if (await FindOverlapAsync(shift, excludeId: null, ct) is { } existing)
        {
            return OverlapConflict(employee.Name, existing);
        }

        shifts.Add(shift);
        await shifts.SaveChangesAsync(ct);

        return ToResponse(shift, employee.Name);
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

        var employee = await employees.GetByIdAsync(request.EmployeeId!.Value, ct);
        if (employee is null)
        {
            return EmployeeNotFound(request.EmployeeId.Value);
        }

        // La comprobación usa un candidato para no modificar la entidad con seguimiento antes de saber si hay conflicto.
        var candidate = Shift.Create(employee.Id, request.Date!.Value, request.StartTime!.Value, request.EndTime!.Value);

        if (await FindOverlapAsync(candidate, excludeId: id, ct) is { } existing)
        {
            return OverlapConflict(employee.Name, existing);
        }

        shift.Update(employee.Id, candidate.Date, candidate.StartTime, candidate.EndTime);
        await shifts.SaveChangesAsync(ct);

        return ToResponse(shift, employee.Name);
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
    /// Como un turno dura menos de 24 h, solo pueden solaparse con él los turnos del día anterior al siguiente;
    /// decide la entidad (<see cref="Shift.Overlaps"/>).
    /// </summary>
    private async Task<Shift?> FindOverlapAsync(Shift candidate, int? excludeId, CancellationToken ct)
    {
        var nearby = await shifts.ListForOverlapAsync(
            candidate.EmployeeId, candidate.Date.AddDays(-1), candidate.Date.AddDays(1), excludeId, ct);

        return nearby.FirstOrDefault(candidate.Overlaps);
    }

    private static ResultError OverlapConflict(string employeeName, Shift existing) =>
        ResultError.Conflict($"El turno se solapa con un turno existente de {employeeName} ({Describe(existing)}).");

    private static ResultError NotFound(int id) => ResultError.NotFound($"No se encontró el turno {id}.");

    private static ResultError EmployeeNotFound(int id) =>
        ResultError.NotFound($"No se encontró el colaborador {id}.");

    private static string Describe(Shift shift) =>
        FormattableString.Invariant($"{shift.Date:yyyy-MM-dd} {shift.StartTime:HH:mm}-{shift.EndTime:HH:mm}");

    private static ShiftResponse ToResponse(Shift s, string employeeName) =>
        new(s.Id, s.EmployeeId, employeeName, s.Date, s.StartTime, s.EndTime);
}
