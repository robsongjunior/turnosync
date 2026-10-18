using TurnoSync.Domain.Common;

namespace TurnoSync.Domain.Collaborators;

public sealed class Collaborator : BaseEntity
{
    // Usado pelo EF Core; a criacao passa por Create.
    private Collaborator()
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    /// <summary>ISO 3166-1 alpha-2 (ex.: ES, BR), o formato usado pela Nager.Date.</summary>
    public string CountryCode { get; private set; } = string.Empty;

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static string NormalizeCountryCode(string countryCode) => countryCode.Trim().ToUpperInvariant();

    public static Collaborator Create(string name, string email, string countryCode)
    {
        var collaborator = new Collaborator();
        collaborator.Update(name, email, countryCode);
        return collaborator;
    }

    public void Update(string name, string email, string countryCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);

        Name = name.Trim();
        Email = NormalizeEmail(email);
        CountryCode = NormalizeCountryCode(countryCode);
    }
}
