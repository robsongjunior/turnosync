using System.Diagnostics;
using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using TurnoSync.Application.Holidays;
using TurnoSync.Infra;
using TurnoSync.Infra.Holidays;
using TurnoSync.Tests.Support;

namespace TurnoSync.Tests.Infra;

public sealed class NagerDateHolidayProviderTests
{
    private const string SpainJson = """
        [
          { "date": "2026-01-01", "localName": "Año Nuevo", "name": "New Year's Day", "countryCode": "ES",
            "fixed": true, "global": true, "counties": null, "launchYear": null, "types": ["Public"] },
          { "date": "2026-04-02", "localName": "Jueves Santo", "name": "Maundy Thursday", "countryCode": "ES",
            "fixed": false, "global": false, "counties": ["ES-AN"], "launchYear": null, "types": ["Public"] },
          { "date": "2026-10-12", "localName": "Fiesta Nacional de España", "name": "National Day", "countryCode": "ES",
            "fixed": true, "global": true, "counties": null, "launchYear": null, "types": ["Public"] }
        ]
        """;

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static NagerDateHolidayProvider CreateProvider(StubHttpHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://nager.test/") },
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<NagerDateHolidayProvider>.Instance);

    [Fact]
    public async Task GetNationalHolidays_ShouldReturnOnlyNationalHolidays_WhenApiResponds()
    {
        var handler = new StubHttpHandler((_, _) => Task.FromResult(Json(SpainJson)));
        var provider = CreateProvider(handler);

        var holidays = await provider.GetNationalHolidaysAsync("ES", 2026);

        Assert.Equal([new DateOnly(2026, 1, 1), new DateOnly(2026, 10, 12)], holidays!.Order());
        Assert.Equal("/api/v3/PublicHolidays/2026/ES", handler.Requests.Single().AbsolutePath);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task GetNationalHolidays_ShouldReturnEmptySetAndCacheIt_WhenCountryIsNotSupported(HttpStatusCode status)
    {
        var handler = new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(status)));
        var provider = CreateProvider(handler);

        var first = await provider.GetNationalHolidaysAsync("ZZ", 2026);
        var second = await provider.GetNationalHolidaysAsync("ZZ", 2026);

        Assert.NotNull(first);
        Assert.Empty(first);
        Assert.Empty(second!);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetNationalHolidays_ShouldNotCallTheApiAgain_WhenCountryAndYearAreCached()
    {
        var handler = new StubHttpHandler((_, _) => Task.FromResult(Json(SpainJson)));
        var provider = CreateProvider(handler);

        await provider.GetNationalHolidaysAsync("ES", 2026);
        await provider.GetNationalHolidaysAsync("ES", 2026);

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetNationalHolidays_ShouldCallTheApiAgain_WhenCountryOrYearDiffer()
    {
        var handler = new StubHttpHandler((_, _) => Task.FromResult(Json("[]")));
        var provider = CreateProvider(handler);

        await provider.GetNationalHolidaysAsync("ES", 2026);
        await provider.GetNationalHolidaysAsync("ES", 2027);
        await provider.GetNationalHolidaysAsync("BR", 2026);

        Assert.Equal(3, handler.CallCount);
    }

    [Fact]
    public async Task GetNationalHolidays_ShouldReturnNullAndNotCache_WhenApiFails()
    {
        var calls = 0;
        var handler = new StubHttpHandler((_, _) => Task.FromResult(
            ++calls == 1
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                : Json(SpainJson)));
        var provider = CreateProvider(handler);

        var failed = await provider.GetNationalHolidaysAsync("ES", 2026);
        var recovered = await provider.GetNationalHolidaysAsync("ES", 2026);

        Assert.Null(failed);
        Assert.NotNull(recovered);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task GetNationalHolidays_ShouldReturnNull_WhenNetworkFails()
    {
        var handler = new StubHttpHandler((_, _) => throw new HttpRequestException("connection refused"));
        var provider = CreateProvider(handler);

        Assert.Null(await provider.GetNationalHolidaysAsync("ES", 2026));
    }

    [Fact]
    public async Task GetNationalHolidays_ShouldReturnNull_WhenResponseIsNotValidJson()
    {
        var handler = new StubHttpHandler((_, _) => Task.FromResult(Json("<html>maintenance</html>")));
        var provider = CreateProvider(handler);

        Assert.Null(await provider.GetNationalHolidaysAsync("ES", 2026));
    }

    [Fact]
    public async Task GetNationalHolidays_ShouldPropagateCancellation_WhenCallerCancels()
    {
        using var cts = new CancellationTokenSource();
        var handler = new StubHttpHandler(async (_, ct) =>
        {
            await cts.CancelAsync();
            ct.ThrowIfCancellationRequested();
            return Json("[]");
        });
        var provider = CreateProvider(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GetNationalHolidaysAsync("ES", 2026, cts.Token));
    }

    // --- Real pipeline (AddNagerDateHolidays): 2s timeout per attempt, 5s in total and 1 retry ---

    private static ServiceProvider BuildPipeline(StubHttpHandler handler)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddNagerDateHolidays().ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Pipeline_ShouldRetryOnce_WhenFirstAttemptFailsWithServerError()
    {
        var calls = 0;
        var handler = new StubHttpHandler((_, _) => Task.FromResult(
            ++calls == 1 ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) : Json(SpainJson)));
        await using var services = BuildPipeline(handler);
        var provider = services.GetRequiredService<IHolidayProvider>();

        var holidays = await provider.GetNationalHolidaysAsync("ES", 2026);

        Assert.NotNull(holidays);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task Pipeline_ShouldGiveUpAfterOneRetry_WhenApiKeepsFailing()
    {
        var handler = new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)));
        await using var services = BuildPipeline(handler);
        var provider = services.GetRequiredService<IHolidayProvider>();

        var holidays = await provider.GetNationalHolidaysAsync("ES", 2026);

        Assert.Null(holidays);
        Assert.Equal(2, handler.CallCount);
    }

    [Fact]
    public async Task Pipeline_ShouldNotRetryNotFound()
    {
        var handler = new StubHttpHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));
        await using var services = BuildPipeline(handler);
        var provider = services.GetRequiredService<IHolidayProvider>();

        var holidays = await provider.GetNationalHolidaysAsync("ZZ", 2026);

        Assert.Empty(holidays!);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task Pipeline_ShouldReturnNullWithinTheTotalTimeout_WhenApiNeverAnswers()
    {
        var handler = new StubHttpHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        await using var services = BuildPipeline(handler);
        var provider = services.GetRequiredService<IHolidayProvider>();

        var stopwatch = Stopwatch.StartNew();
        var holidays = await provider.GetNationalHolidaysAsync("ES", 2026);
        stopwatch.Stop();

        Assert.Null(holidays);
        Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(6));
        Assert.Equal(2, handler.CallCount); // 2s + retry + 2s: the per-attempt timeout worked
    }
}
