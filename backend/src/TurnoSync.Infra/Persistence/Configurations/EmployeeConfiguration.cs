using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurnoSync.Domain.Employees;

namespace TurnoSync.Infra.Persistence.Configurations;

internal sealed class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employees");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(254);
        builder.Property(c => c.CountryCode).IsRequired().HasMaxLength(2);

        // El email es único entre los colaboradores no borrados: la importación identifica al colaborador por él.
        // El servicio lo comprueba antes para devolver 409; el índice es la red de seguridad.
        builder.HasIndex(c => c.Email).IsUnique().HasFilter("is_deleted = 0");

        builder.HasQueryFilter(c => !c.IsDeleted);

        // Sin estos datos iniciales no habría forma de crear el primer turno desde la interfaz (no hay pantalla de colaboradores).
        builder.HasData(
            new { Id = 1, Name = "Ana Torres", Email = "ana@turnosync.dev", CountryCode = "ES", IsDeleted = false },
            new { Id = 2, Name = "João Lima", Email = "joao@turnosync.dev", CountryCode = "BR", IsDeleted = false },
            new { Id = 3, Name = "Lucía Fernández", Email = "lucia@turnosync.dev", CountryCode = "AR", IsDeleted = false },
            new { Id = 4, Name = "Carlos Mendoza", Email = "carlos@turnosync.dev", CountryCode = "MX", IsDeleted = false },
            new { Id = 5, Name = "Inês Costa", Email = "ines@turnosync.dev", CountryCode = "PT", IsDeleted = false });
    }
}
