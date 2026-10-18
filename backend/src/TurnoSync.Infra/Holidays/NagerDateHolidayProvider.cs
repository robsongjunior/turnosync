using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using TurnoSync.Application.Holidays;

namespace TurnoSync.Infra.Holidays;

/// <summary>
/// Feriados nacionais via Nager.Date. Resultados são cacheados por (país, ano); falhas nunca são
/// cacheadas e viram <c>null</c> ("desconhecido") em vez de exceção.
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

            // País que a Nager não suporta: sem feriados, sem erro.
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
            {
                return Cache(cacheKey, new HashSet<DateOnly>());
            }

            response.EnsureSuccessStatusCode();

            var holidays = await response.Content.ReadFromJsonAsync<List<NagerHoliday>>(JsonOptions, ct) ?? [];

            // Só feriados nacionais: o colaborador não tem região, então os regionais não se aplicam.
            return Cache(cacheKey, holidays.Where(h => h.Global).Select(h => h.Date).ToHashSet());
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // Rede, timeout, 5xx, circuito aberto ou JSON inesperado: a listagem segue sem a marcação.
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
