using FluentValidation;

namespace TurnoSync.Application.Shifts;

/// <summary>Usado tanto pela API quanto, linha a linha, pela importação do Excel.</summary>
public sealed class ShiftRequestValidator : AbstractValidator<ShiftRequest>
{
    public ShiftRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CollaboratorId).NotNull().GreaterThan(0);
        RuleFor(x => x.Date).NotNull();
        RuleFor(x => x.StartTime).NotNull();
        RuleFor(x => x.EndTime).NotNull();

        // A condição vai numa regra separada: When() se aplicaria também ao NotNull acima.
        RuleFor(x => x.EndTime)
            .Must((request, endTime) => endTime != request.StartTime)
            .When(x => x.StartTime is not null && x.EndTime is not null)
            .WithMessage("'End Time' must be different from 'Start Time'.");
    }
}
