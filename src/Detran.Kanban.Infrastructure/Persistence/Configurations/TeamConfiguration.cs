using Detran.Kanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detran.Kanban.Infrastructure.Persistence.Configurations;

public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.ToTable("Teams");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(150);
        builder.Property(t => t.LeaderId).HasMaxLength(450);
        builder.Property(t => t.DefaultWeeklyCapacityHours).HasColumnType("decimal(6,2)");
        builder.Property(t => t.CreatedAt).IsRequired();
        builder.HasIndex(t => new { t.OrganizationId, t.Name }).IsUnique();
        builder.HasIndex(t => t.LeaderId);
        builder.HasOne<Organization>().WithMany().HasForeignKey(t => t.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class TeamMemberConfiguration : IEntityTypeConfiguration<TeamMember>
{
    public void Configure(EntityTypeBuilder<TeamMember> builder)
    {
        builder.ToTable("TeamMembers");
        builder.HasKey(m => new { m.TeamId, m.UserId });
        builder.Property(m => m.UserId).HasMaxLength(450);
        builder.Property(m => m.WeeklyCapacityHours).HasColumnType("decimal(6,2)");

        builder.HasOne(m => m.Team)
            .WithMany(t => t.Members)
            .HasForeignKey(m => m.TeamId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
