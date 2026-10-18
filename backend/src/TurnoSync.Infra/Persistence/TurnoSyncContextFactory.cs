using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TurnoSync.Infra.Persistence;

/// <summary>Usada apenas pelas ferramentas do EF (dotnet ef migrations); não participa da execução.</summary>
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
