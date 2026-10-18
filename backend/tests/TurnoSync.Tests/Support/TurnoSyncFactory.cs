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
/// Sobe a API inteira em memória. Cada instância usa seu próprio banco SQLite em memória
/// (nome único) e uma Nager.Date falsa, então os testes não compartilham estado nem usam a rede.
/// </summary>
public sealed class TurnoSyncFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString =
        $"Data Source=TurnoSyncApi_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    /// <summary>Por padrão devolve "sem feriados"; os testes configuram o que precisarem.</summary>
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
    /// <summary>Mesmas regras de JSON da API (camelCase e horários HH:mm).</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new TimeOnlyJsonConverter() },
    };
}
