using FluentValidation;

namespace TurnoSync.Application.Shifts;

/// <summary>Lo usan la API y, fila a fila, la importación desde Excel.</summary>
public sealed class ShiftRequestValidator : AbstractValidator<ShiftRequest>
{
    public ShiftRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CollaboratorId)
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
    }
}
