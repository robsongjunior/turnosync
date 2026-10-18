using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TurnoSync.Api.Common;
using TurnoSync.Application.Holidays;

namespace TurnoSync.Tests.Support;

/// <summary>
/// Starts the whole API in memory. Each instance uses its own in-memory SQLite database
/// (unique name) and a fake Nager.Date, so tests share no state and use no network.
/// </summary>
public sealed class TurnoSyncFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString =
        $"Data Source=TurnoSyncApi_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    /// <summary>Returns "no holidays" by default; tests configure whatever they need.</summary>
    public FakeHolidayProvider Holidays { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:TurnoSync"] = _connectionString }));

        builder.ConfigureTestServices(services => services.AddSingleton<IHolidayProvider>(Holidays));
    }
}

public static class TestJson
{
    /// <summary>Same JSON rules as the API (camelCase and HH:mm times).</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new TimeOnlyJsonConverter() },
    };
}
