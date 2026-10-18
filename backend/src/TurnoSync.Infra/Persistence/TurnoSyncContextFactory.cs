using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TurnoSync.Infra.Persistence;

/// <summary>Used only by the EF tooling (dotnet ef migrations); it does not take part at runtime.</summary>
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
