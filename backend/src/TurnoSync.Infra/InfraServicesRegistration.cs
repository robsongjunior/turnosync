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

    // In-memory database with a shared cache: every DbContext sees the same database.
    // To persist to a file, just replace it with, for example, "Data Source=/data/turnosync.db".
    public const string DefaultConnectionString = "Data Source=TurnoSync;Mode=Memory;Cache=Shared";

    public const string NagerBaseUrlKey = "Nager:BaseUrl";
    public const string DefaultNagerBaseUrl = "https://date.nager.at/";

    public static IServiceCollection AddInfra(this IServiceCollection services)
    {
        // A singleton connection stays open only to keep the in-memory database alive while the API is up.
        services.AddSingleton(sp => OpenKeepAliveConnection(GetConnectionString(sp)));

        services.AddDbContext<TurnoSyncContext>((sp, options) =>
        {
            // Make sure the keep-alive connection exists before any DbContext opens its own.
            _ = sp.GetRequiredService<SqliteConnection>();

            options
                .UseSqlite(GetConnectionString(sp))
                .UseSnakeCaseNamingConvention()
                // Shift and Collaborator have a soft-delete query filter and are deleted together (cascade in the service).
                .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        });

        services.AddScoped<ICollaboratorRepository, CollaboratorRepository>();
        services.AddScoped<IShiftRepository, ShiftRepository>();

        services.AddNagerDateHolidays();
        services.AddSingleton<IShiftSpreadsheet, ClosedXmlShiftSpreadsheet>();

        return services;
    }

    /// <summary>
    /// Typed Nager.Date client with an in-memory cache and resilience: 5s total timeout,
    /// 2s per attempt and 1 retry, so a slow or unavailable external API cannot hang the listing.
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
            // The default (2s) would blow the total timeout before the second attempt even starts.
            options.Retry.Delay = TimeSpan.FromMilliseconds(500);
        });

        return httpClient;
    }

    /// <summary>Applies the migrations (and the collaborator seed) at startup.</summary>
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
