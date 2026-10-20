using Microsoft.EntityFrameworkCore;
using TurnoSync.Domain.Employees;
using TurnoSync.Domain.Shifts;

namespace TurnoSync.Infra.Persistence;

public sealed class TurnoSyncContext(DbContextOptions<TurnoSyncContext> options) : DbContext(options)
{
    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<Shift> Shifts => Set<Shift>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TurnoSyncContext).Assembly);
}
