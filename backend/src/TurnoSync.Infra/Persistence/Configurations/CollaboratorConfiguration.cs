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

        // E-mail único entre os não excluídos: a importação identifica o colaborador por ele.
        // O service checa antes para devolver 409; o índice é a rede de segurança.
        builder.HasIndex(c => c.Email).IsUnique().HasFilter("is_deleted = 0");

        builder.HasQueryFilter(c => !c.IsDeleted);

        // Sem o seed não haveria como criar o primeiro turno pela interface (não há tela de colaboradores).
        builder.HasData(
            new { Id = 1, Name = "Ana Torres", Email = "ana@turnosync.dev", CountryCode = "ES", IsDeleted = false },
            new { Id = 2, Name = "João Lima", Email = "joao@turnosync.dev", CountryCode = "BR", IsDeleted = false },
            new { Id = 3, Name = "Lucía Fernández", Email = "lucia@turnosync.dev", CountryCode = "AR", IsDeleted = false },
            new { Id = 4, Name = "Carlos Mendoza", Email = "carlos@turnosync.dev", CountryCode = "MX", IsDeleted = false },
            new { Id = 5, Name = "Inês Costa", Email = "ines@turnosync.dev", CountryCode = "PT", IsDeleted = false });
    }
}
