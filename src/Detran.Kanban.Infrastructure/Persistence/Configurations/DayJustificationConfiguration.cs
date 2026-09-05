using Detran.Kanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detran.Kanban.Infrastructure.Persistence.Configurations;

public class DayJustificationConfiguration : IEntityTypeConfiguration<DayJustification>
{
    public void Configure(EntityTypeBuilder<DayJustification> builder)
    {
        builder.ToTable("DayJustifications");
        builder.HasKey(d => d.Id);
        builder.HasIndex(d => d.OrganizationId);
        builder.HasOne<Organization>().WithMany().HasForeignKey(d => d.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(d => d.UserId).IsRequired().HasMaxLength(450);
        builder.Property(d => d.Reason).IsRequired().HasMaxLength(200);
        builder.Property(d => d.Hours).HasColumnType("decimal(6,2)");
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.HasIndex(d => new { d.UserId, d.Date });
    }
}
