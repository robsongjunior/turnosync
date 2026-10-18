using System.Collections.Concurrent;
using TurnoSync.Application.Holidays;

namespace TurnoSync.Tests.Support;

/// <summary>Sustituye a Nager.Date en las pruebas de integración: sin red y con respuestas controladas por país y año.</summary>
public sealed class FakeHolidayProvider : IHolidayProvider
{
    private static readonly IReadOnlySet<DateOnly> NoHolidays = new HashSet<DateOnly>();

    private readonly ConcurrentDictionary<(string Country, int Year), IReadOnlySet<DateOnly>?> _responses = new();
    private readonly ConcurrentQueue<(string Country, int Year)> _calls = new();

    public IReadOnlyCollection<(string Country, int Year)> Calls => _calls;

    public void SetHolidays(string country, int year, params DateOnly[] dates) =>
        _responses[(country, year)] = dates.ToHashSet();

    /// <summary>Simula que Nager.Date está caído para el país y el año.</summary>
    public void SetUnavailable(string country, int year) => _responses[(country, year)] = null;

    public Task<IReadOnlySet<DateOnly>?> GetNationalHolidaysAsync(
        string countryCode, int year, CancellationToken ct = default)
    {
        _calls.Enqueue((countryCode, year));

        return Task.FromResult(_responses.TryGetValue((countryCode, year), out var holidays) ? holidays : NoHolidays);
    }
}
