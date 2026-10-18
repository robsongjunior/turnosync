using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TurnoSync.Infra.Persistence;

/// <summary>Lo usa solo la herramienta de EF (dotnet ef migrations); no participa en tiempo de ejecución.</summary>
public sealed class TurnoSyncContextFactory : IDesignTimeDbContextFactory<TurnoSyncContext>
{
    public TurnoSyncContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<TurnoSyncContext>()
            .UseSqlite(InfraServicesRegistration.DefaultConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options;

        return new TurnoSyncContext(options);
    }
}
