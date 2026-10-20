using FluentValidation;

namespace TurnoSync.Application.Shifts;

/// <summary>Lo usan la API y, fila a fila, la importación desde Excel.</summary>
public sealed class ShiftRequestValidator : AbstractValidator<ShiftRequest>
{
    /// <summary>
    /// Los turnos no guardan zona horaria (fecha y hora son locales del colaborador). Para decidir si un turno ya es
    /// pasado se compara con la hora de España, la de la empresa: no hay un mapa fiable país → zona horaria
    /// (BR o MX tienen varias).
    /// </summary>
    private static readonly TimeZoneInfo ReferenceTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Madrid");

    private readonly TimeProvider _timeProvider;

    public ShiftRequestValidator(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;

        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.EmployeeId)
            .NotNull().WithMessage("El colaborador es obligatorio.")
            .GreaterThan(0).WithMessage("El identificador del colaborador debe ser mayor que 0.");
        RuleFor(x => x.Date).NotNull().WithMessage("La fecha es obligatoria.");
        RuleFor(x => x.StartTime).NotNull().WithMessage("La hora de inicio es obligatoria.");
        RuleFor(x => x.EndTime).NotNull().WithMessage("La hora de fin es obligatoria.");

        // La condición va en una regla aparte: When() también se aplicaría al NotNull anterior.
        RuleFor(x => x.EndTime)
            .Must((request, endTime) => endTime != request.StartTime)
            .When(x => x.StartTime is not null && x.EndTime is not null)
            .WithMessage("La hora de fin debe ser distinta de la hora de inicio.");

        // Cuenta el inicio: un turno de hoy cuya hora de inicio ya pasó también es pasado.
        RuleFor(x => x.Date)
            .Must((request, date) => date!.Value.ToDateTime(request.StartTime!.Value) >= Now())
            .When(x => x.Date is not null && x.StartTime is not null)
            .WithMessage("No se puede registrar un turno en el pasado.");
    }

    private DateTime Now() => TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), ReferenceTimeZone).DateTime;
}
