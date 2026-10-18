using System.Globalization;
using FluentValidation;

namespace TurnoSync.Application.Collaborators;

public sealed class CollaboratorRequestValidator : AbstractValidator<CollaboratorRequest>
{
    public CollaboratorRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre no puede superar los {MaxLength} caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El email es obligatorio.")
            .EmailAddress().WithMessage("El email no es válido.")
            .MaximumLength(254).WithMessage("El email no puede superar los {MaxLength} caracteres.");

        RuleFor(x => x.CountryCode)
            .NotEmpty().WithMessage("El país es obligatorio.")
            .Length(2).WithMessage("El código de país debe tener {MaxLength} letras.")
            .Must(BeKnownCountry).WithMessage("El código de país debe ser un código ISO 3166-1 alfa-2 válido (por ejemplo, ES).");
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
