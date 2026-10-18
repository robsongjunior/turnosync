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
/// Levanta toda la API en memoria. Cada instancia usa su propia base SQLite en memoria
/// (nombre único) y un Nager.Date falso, así que las pruebas no comparten estado ni usan la red.
/// </summary>
public sealed class TurnoSyncFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString =
        $"Data Source=TurnoSyncApi_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    /// <summary>Devuelve "sin festivos" por defecto; cada prueba configura lo que necesite.</summary>
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
    /// <summary>Las mismas reglas JSON que la API (camelCase y horas HH:mm).</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new TimeOnlyJsonConverter() },
    };
}
