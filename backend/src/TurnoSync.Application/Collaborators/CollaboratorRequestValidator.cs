using System.Globalization;
using FluentValidation;

namespace TurnoSync.Application.Collaborators;

public sealed class CollaboratorRequestValidator : AbstractValidator<CollaboratorRequest>
{
    public CollaboratorRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);

        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);

        RuleFor(x => x.CountryCode)
            .NotEmpty()
            .Length(2)
            .Must(BeKnownCountry).WithMessage("'{PropertyName}' must be a valid ISO 3166-1 alpha-2 country code.");
    }

    private static bool BeKnownCountry(string? countryCode)
    {
        try
        {
            _ = new RegionInfo(countryCode!);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
