using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TurnoSync.Application.Collaborators;
using TurnoSync.Application.Holidays;
using TurnoSync.Application.Shifts;
using TurnoSync.Infra.Holidays;
using TurnoSync.Infra.Persistence;
using TurnoSync.Infra.Persistence.Repositories;
using TurnoSync.Infra.Spreadsheets;

namespace TurnoSync.Infra;

public static class InfraServicesRegistration
{
    public const string ConnectionStringName = "TurnoSync";

    // Archivo SQLite relativo al directorio de trabajo. En Docker se sustituye por "Data Source=/data/turnosync.db" (volumen).
    public const string DefaultConnectionString = "Data Source=turnosync.db";

    public const string NagerBaseUrlKey = "Nager:BaseUrl";
    public const string DefaultNagerBaseUrl = "https://date.nager.at/";

    public static IServiceCollection AddInfra(this IServiceCollection services)
    {
        // Con una base en memoria (la usan las pruebas), una conexión singleton queda abierta para mantenerla viva
        // mientras la API esté en marcha. Con un archivo no hace falta.
        services.AddSingleton(sp => OpenKeepAliveConnection(GetConnectionString(sp)));

        services.AddDbContext<TurnoSyncContext>((sp, options) =>
        {
            // Asegura que la conexión que mantiene viva la base exista antes de que cualquier DbContext abra la suya.
            if (IsInMemory(GetConnectionString(sp)))
            {
                _ = sp.GetRequiredService<SqliteConnection>();
            }

            options
                .UseSqlite(GetConnectionString(sp))
                .UseSnakeCaseNamingConvention()
                // Shift y Collaborator tienen un filtro de consulta de borrado lógico y se borran juntos (cascada en el servicio).
                .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        });

        services.AddScoped<ICollaboratorRepository, CollaboratorRepository>();
        services.AddScoped<IShiftRepository, ShiftRepository>();

        services.AddNagerDateHolidays();
        services.AddSingleton<IShiftSpreadsheet, ClosedXmlShiftSpreadsheet>();

        return services;
    }

    /// <summary>
    /// Cliente tipado de Nager.Date con caché en memoria y resiliencia: 5 s de timeout total,
    /// 2 s por intento y 1 reintento, para que una API externa lenta o caída no cuelgue el listado.
    /// </summary>
    public static IHttpClientBuilder AddNagerDateHolidays(this IServiceCollection services)
    {
        services.AddMemoryCache();

        var httpClient = services.AddHttpClient<IHolidayProvider, NagerDateHolidayProvider>((sp, client) =>
            client.BaseAddress = new Uri(
                sp.GetRequiredService<IConfiguration>()[NagerBaseUrlKey] ?? DefaultNagerBaseUrl));

        httpClient.AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
            options.Retry.MaxRetryAttempts = 1;
            // El valor por defecto (2 s) agotaría el timeout total antes de que empiece el segundo intento.
            options.Retry.Delay = TimeSpan.FromMilliseconds(500);
        });

        return httpClient;
    }

    /// <summary>Aplica las migraciones (y los datos iniciales de colaboradores) al arrancar.</summary>
    public static void MigrateDatabase(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TurnoSyncContext>().Database.Migrate();
    }

    private static string GetConnectionString(IServiceProvider sp) =>
        sp.GetRequiredService<IConfiguration>().GetConnectionString(ConnectionStringName) ?? DefaultConnectionString;

    private static bool IsInMemory(string connectionString)
    {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        return builder.Mode == SqliteOpenMode.Memory || builder.DataSource == ":memory:";
    }

    private static SqliteConnection OpenKeepAliveConnection(string connectionString)
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }
}
