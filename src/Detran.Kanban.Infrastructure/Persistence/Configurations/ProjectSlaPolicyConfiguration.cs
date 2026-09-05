using Detran.Kanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detran.Kanban.Infrastructure.Persistence.Configurations;

public class ProjectSlaPolicyConfiguration : IEntityTypeConfiguration<ProjectSlaPolicy>
{
    public void Configure(EntityTypeBuilder<ProjectSlaPolicy> builder)
    {
        builder.ToTable("ProjectSlaPolicies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ServiceStart).HasColumnType("time");
        builder.Property(x => x.ServiceEnd).HasColumnType("time");
        builder.Property(x => x.TimeZoneId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.HolidaysJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(x => x.RulesJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.HasIndex(x => x.ProjectId).IsUnique();
        builder.HasOne(x => x.Project).WithOne(x => x.SlaPolicy)
            .HasForeignKey<ProjectSlaPolicy>(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
