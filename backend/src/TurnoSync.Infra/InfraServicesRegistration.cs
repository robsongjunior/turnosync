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

    // Base de datos en memoria con caché compartida: todos los DbContext ven la misma base.
    // Para persistir en un archivo basta con sustituirla por, por ejemplo, "Data Source=/data/turnosync.db".
    public const string DefaultConnectionString = "Data Source=TurnoSync;Mode=Memory;Cache=Shared";

    public const string NagerBaseUrlKey = "Nager:BaseUrl";
    public const string DefaultNagerBaseUrl = "https://date.nager.at/";

    public static IServiceCollection AddInfra(this IServiceCollection services)
    {
        // Una conexión singleton queda abierta solo para mantener viva la base en memoria mientras la API esté en marcha.
        services.AddSingleton(sp => OpenKeepAliveConnection(GetConnectionString(sp)));

        services.AddDbContext<TurnoSyncContext>((sp, options) =>
        {
            // Asegura que la conexión que mantiene viva la base exista antes de que cualquier DbContext abra la suya.
            _ = sp.GetRequiredService<SqliteConnection>();

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

    private static SqliteConnection OpenKeepAliveConnection(string connectionString)
    {
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }
}
