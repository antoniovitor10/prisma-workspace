using Detran.Kanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detran.Kanban.Infrastructure.Persistence.Configurations;

public class SprintConfiguration : IEntityTypeConfiguration<Sprint>
{
    public void Configure(EntityTypeBuilder<Sprint> builder)
    {
        builder.ToTable("Sprints");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Goal).HasMaxLength(1000);
        builder.Property(x => x.Status).HasConversion<int>();
        builder.HasOne(x => x.Project).WithMany(x => x.Sprints).HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Team).WithMany().HasForeignKey(x => x.TeamId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ProjectId, x.TeamId, x.Status });
        builder.HasIndex(x => new { x.ProjectId, x.Status })
            .IsUnique()
            .HasFilter("[Status] = 2");
    }
}

public class SprintItemSnapshotConfiguration : IEntityTypeConfiguration<SprintItemSnapshot>
{
    public void Configure(EntityTypeBuilder<SprintItemSnapshot> builder)
    {
        builder.ToTable("SprintItemSnapshots");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Kind).HasConversion<int>();
        builder.Property(x => x.EstimatedHours).HasColumnType("decimal(6,2)");
        builder.Property(x => x.Outcome).HasConversion<int>();
        builder.HasIndex(x => new { x.SprintId, x.WorkItemId }).IsUnique();
        builder.HasOne(x => x.Sprint).WithMany(x => x.ItemSnapshots).HasForeignKey(x => x.SprintId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SprintCapacityConfiguration : IEntityTypeConfiguration<SprintCapacity>
{
    public void Configure(EntityTypeBuilder<SprintCapacity> builder)
    {
        builder.ToTable("SprintCapacities");
        builder.HasKey(x => new { x.SprintId, x.UserId });
        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.Property(x => x.AvailableHours).HasColumnType("decimal(7,2)");
        builder.Property(x => x.DaysOffHours).HasColumnType("decimal(7,2)");
        builder.HasOne(x => x.Sprint).WithMany(x => x.Capacities).HasForeignKey(x => x.SprintId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
