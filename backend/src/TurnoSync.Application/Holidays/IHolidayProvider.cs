namespace TurnoSync.Application.Holidays;

public interface IHolidayProvider
{
    /// <summary>
    /// Dates of the country's national public holidays in the given year. An unsupported country returns an empty set.
    /// Returns <c>null</c> when the lookup fails (network, timeout, 5xx): "unknown", which is different from "no holidays".
    /// Never throws because of an external API failure.
    /// </summary>
    Task<IReadOnlySet<DateOnly>?> GetNationalHolidaysAsync(
        string countryCode, int year, CancellationToken ct = default);
}
