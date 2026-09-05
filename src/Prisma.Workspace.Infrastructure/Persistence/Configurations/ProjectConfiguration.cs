using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Prisma.Workspace.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.OwnerId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.Status).HasConversion<int>().HasDefaultValue(ProjectStatus.Active).IsRequired();
        builder.Property(x => x.Methodology).HasConversion<int>().HasDefaultValue(ProjectMethodology.Kanban).IsRequired();
        builder.Property(x => x.Nature).HasConversion<int>().HasDefaultValue(WorkNature.Unclassified).IsRequired();
        builder.Property(x => x.WorkType).HasConversion<int>().HasDefaultValue(WorkType.Unclassified).IsRequired();
        builder.Property(x => x.WorkflowInheritanceMode).HasConversion<int>()
            .HasDefaultValue(WorkflowInheritanceMode.Custom).IsRequired();
        builder.Property(x => x.SettingsJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.UpdatedAt).HasDefaultValueSql("SYSUTCDATETIME()");
        builder.HasIndex(x => new { x.OrganizationId, x.Key }).IsUnique();
        builder.HasIndex(x => x.OrganizationId);
        builder.HasIndex(x => x.OwnerId);
        builder.HasIndex(x => new { x.OrganizationId, x.IsArchived, x.Status });
        builder.HasIndex(x => new { x.OrganizationId, x.Nature, x.WorkType });
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Client).WithMany().HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.WorkflowTemplate).WithMany(x => x.Projects)
            .HasForeignKey(x => x.WorkflowTemplateId).OnDelete(DeleteBehavior.Restrict);
        // ClientSetNull evita ciclo de cascata detectado pelo SQL Server:
        // Projects.DefaultBoardId → Boards → Projects (já Restrict acima).
        builder.HasOne(x => x.DefaultBoard).WithMany()
            .HasForeignKey(x => x.DefaultBoardId)
            .OnDelete(DeleteBehavior.ClientSetNull);
        builder.ToTable(t => t.HasCheckConstraint("CK_Projects_WorkflowInheritance",
            "([WorkflowInheritanceMode] = 1 AND [WorkflowTemplateId] IS NULL) OR ([WorkflowInheritanceMode] = 2 AND [WorkflowTemplateId] IS NOT NULL)"));
    }
}

public class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.ToTable("ProjectMembers");
        builder.HasKey(x => new { x.ProjectId, x.UserId });
        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.Property(x => x.Role).HasConversion<int>();
        builder.HasOne(x => x.Project).WithMany(x => x.Members).HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.UserId);
    }
}

public class ProjectTeamConfiguration : IEntityTypeConfiguration<ProjectTeam>
{
    public void Configure(EntityTypeBuilder<ProjectTeam> builder)
    {
        builder.ToTable("ProjectTeams");
        builder.HasKey(x => new { x.ProjectId, x.TeamId });
        builder.HasOne(x => x.Project).WithMany(x => x.Teams).HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Team).WithMany(x => x.Projects).HasForeignKey(x => x.TeamId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
