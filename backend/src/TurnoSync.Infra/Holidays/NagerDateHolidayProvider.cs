using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TurnoSync.Application.Holidays;

namespace TurnoSync.Infra.Holidays;

/// <summary>
/// National public holidays via Nager.Date. Results are cached per (country, year); failures are never
/// cached and become <c>null</c> ("unknown") instead of an exception.
/// </summary>
public sealed class NagerDateHolidayProvider(
    HttpClient httpClient,
    IMemoryCache cache,
    ILogger<NagerDateHolidayProvider> logger) : IHolidayProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlySet<DateOnly>?> GetNationalHolidaysAsync(
        string countryCode, int year, CancellationToken ct = default)
    {
        var cacheKey = $"nager-holidays:{countryCode}:{year}";
        if (cache.TryGetValue(cacheKey, out IReadOnlySet<DateOnly>? cached))
        {
            return cached;
        }

        try
        {
            using var response = await httpClient.GetAsync($"api/v3/PublicHolidays/{year}/{countryCode}", ct);

            // A country Nager does not support: no holidays, no error.
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
            {
                return Cache(cacheKey, new HashSet<DateOnly>());
            }

            response.EnsureSuccessStatusCode();

            var holidays = await response.Content.ReadFromJsonAsync<List<NagerHoliday>>(JsonOptions, ct) ?? [];

            // National holidays only: the collaborator has no region, so regional ones do not apply.
            return Cache(cacheKey, holidays.Where(h => h.Global).Select(h => h.Date).ToHashSet());
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Network, timeout, 5xx, open circuit or unexpected JSON: the listing carries on without the flag.
            logger.LogWarning(ex, "Could not load public holidays for {CountryCode} {Year}", countryCode, year);
            return null;
        }
    }

    private HashSet<DateOnly> Cache(string cacheKey, HashSet<DateOnly> holidays)
    {
        cache.Set(cacheKey, (IReadOnlySet<DateOnly>)holidays, CacheDuration);
        return holidays;
    }

    private sealed record NagerHoliday(DateOnly Date, bool Global);
}
