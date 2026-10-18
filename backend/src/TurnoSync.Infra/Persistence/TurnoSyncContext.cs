using Microsoft.EntityFrameworkCore;
using TurnoSync.Domain.Collaborators;
using TurnoSync.Domain.Shifts;

namespace TurnoSync.Infra.Persistence;

public sealed class TurnoSyncContext(DbContextOptions<TurnoSyncContext> options) : DbContext(options)
{
    public DbSet<Collaborator> Collaborators => Set<Collaborator>();

    public DbSet<Shift> Shifts => Set<Shift>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TurnoSyncContext).Assembly);
}
