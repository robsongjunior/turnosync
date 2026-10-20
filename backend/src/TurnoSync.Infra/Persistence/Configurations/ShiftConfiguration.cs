using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TurnoSync.Domain.Shifts;

namespace TurnoSync.Infra.Persistence.Configurations;

internal sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> builder)
    {
        builder.ToTable("shifts");
        builder.HasKey(s => s.Id);

        builder.HasOne(s => s.Employee)
            .WithMany()
            .HasForeignKey(s => s.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => new { s.EmployeeId, s.Date });

        builder.HasQueryFilter(s => !s.IsDeleted);
    }
}
