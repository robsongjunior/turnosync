using FluentValidation;

namespace TurnoSync.Application.Shifts;

/// <summary>Used by the API and, row by row, by the Excel import.</summary>
public sealed class ShiftRequestValidator : AbstractValidator<ShiftRequest>
{
    public ShiftRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.CollaboratorId).NotNull().GreaterThan(0);
        RuleFor(x => x.Date).NotNull();
        RuleFor(x => x.StartTime).NotNull();
        RuleFor(x => x.EndTime).NotNull();

        // The condition goes in a separate rule: When() would also apply to the NotNull above.
        RuleFor(x => x.EndTime)
            .Must((request, endTime) => endTime != request.StartTime)
            .When(x => x.StartTime is not null && x.EndTime is not null)
            .WithMessage("'End Time' must be different from 'Start Time'.");
    }
}
