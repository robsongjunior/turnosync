using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurnoSync.Domain.Collaborators;

namespace TurnoSync.Infra.Persistence.Configurations;

internal sealed class CollaboratorConfiguration : IEntityTypeConfiguration<Collaborator>
{
    public void Configure(EntityTypeBuilder<Collaborator> builder)
    {
        builder.ToTable("collaborators");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(254);
        builder.Property(c => c.CountryCode).IsRequired().HasMaxLength(2);

        // Email is unique among non-deleted collaborators: the import identifies the collaborator by it.
        // The service checks first to return 409; the index is the safety net.
        builder.HasIndex(c => c.Email).IsUnique().HasFilter("is_deleted = 0");

        builder.HasQueryFilter(c => !c.IsDeleted);

        // Without the seed there would be no way to create the first shift from the UI (there is no collaborators screen).
        builder.HasData(
            new { Id = 1, Name = "Ana Torres", Email = "ana@turnosync.dev", CountryCode = "ES", IsDeleted = false },
            new { Id = 2, Name = "João Lima", Email = "joao@turnosync.dev", CountryCode = "BR", IsDeleted = false },
            new { Id = 3, Name = "Lucía Fernández", Email = "lucia@turnosync.dev", CountryCode = "AR", IsDeleted = false },
            new { Id = 4, Name = "Carlos Mendoza", Email = "carlos@turnosync.dev", CountryCode = "MX", IsDeleted = false },
            new { Id = 5, Name = "Inês Costa", Email = "ines@turnosync.dev", CountryCode = "PT", IsDeleted = false });
    }
}
