using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using TurnoSync.Api.Common;

namespace TurnoSync.Tests.Support;

/// <summary>
/// Sobe a API inteira em memória. Cada instância usa seu próprio banco SQLite em memória
/// (nome único), então os testes não compartilham estado.
/// </summary>
public sealed class TurnoSyncFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString =
        $"Data Source=TurnoSyncApi_{Guid.NewGuid():N};Mode=Memory;Cache=Shared";

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
            new Dictionary<string, string?> { ["ConnectionStrings:TurnoSync"] = _connectionString }));
}

public static class TestJson
{
    /// <summary>Mesmas regras de JSON da API (camelCase e horários HH:mm).</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new TimeOnlyJsonConverter() },
    };
}
