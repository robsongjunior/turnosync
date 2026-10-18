using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TurnoSync.Application.Collaborators;
using TurnoSync.Application.Shifts;
using TurnoSync.Infra.Persistence;
using TurnoSync.Infra.Persistence.Repositories;

namespace TurnoSync.Infra;

public static class InfraServicesRegistration
{
    public const string ConnectionStringName = "TurnoSync";

    // Banco em memória com cache compartilhado: todo DbContext enxerga o mesmo banco.
    // Para persistir em arquivo basta trocar por, por exemplo, "Data Source=/data/turnosync.db".
    public const string DefaultConnectionString = "Data Source=TurnoSync;Mode=Memory;Cache=Shared";

    public static IServiceCollection AddInfra(this IServiceCollection services)
    {
        // Uma conexão singleton fica aberta só para manter o banco em memória vivo enquanto a API estiver de pé.
        services.AddSingleton(sp => OpenKeepAliveConnection(GetConnectionString(sp)));

        services.AddDbContext<TurnoSyncContext>((sp, options) =>
        {
            // Garante que a conexão keep-alive existe antes de qualquer DbContext abrir a própria.
            _ = sp.GetRequiredService<SqliteConnection>();

            options
                .UseSqlite(GetConnectionString(sp))
                .UseSnakeCaseNamingConvention()
                // Shift e Collaborator têm query filter de soft delete e são excluídos juntos (cascata no service).
                .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
        });

        services.AddScoped<ICollaboratorRepository, CollaboratorRepository>();
        services.AddScoped<IShiftRepository, ShiftRepository>();

        return services;
    }

    /// <summary>Aplica as migrations (e o seed de colaboradores) no startup.</summary>
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
