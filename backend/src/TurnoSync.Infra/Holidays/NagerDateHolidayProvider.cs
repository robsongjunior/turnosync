using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TurnoSync.Application.Holidays;

namespace TurnoSync.Infra.Holidays;

/// <summary>
/// Festivos nacionales a través de Nager.Date. Los resultados se cachean por (país, año); los fallos nunca se
/// cachean y se convierten en <c>null</c> ("desconocido") en lugar de una excepción.
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

            // Un país que Nager no admite: sin festivos, sin error.
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
            {
                return Cache(cacheKey, new HashSet<DateOnly>());
            }

            response.EnsureSuccessStatusCode();

            var holidays = await response.Content.ReadFromJsonAsync<List<NagerHoliday>>(JsonOptions, ct) ?? [];

            // Solo festivos nacionales: el colaborador no tiene región, así que los regionales no aplican.
            return Cache(cacheKey, holidays.Where(h => h.Global).Select(h => h.Date).ToHashSet());
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Red, timeout, 5xx, circuito abierto o JSON inesperado: el listado continúa sin la marca.
            logger.LogWarning(ex, "No se pudieron cargar los festivos de {CountryCode} {Year}", countryCode, year);
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
