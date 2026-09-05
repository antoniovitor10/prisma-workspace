using Prisma.Workspace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Prisma.Workspace.Infrastructure.Persistence.Configurations;

public sealed class OrganizationWorkflowTemplateConfiguration : IEntityTypeConfiguration<OrganizationWorkflowTemplate>
{
    public void Configure(EntityTypeBuilder<OrganizationWorkflowTemplate> builder)
    {
        builder.ToTable("OrganizationWorkflowTemplates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.OrganizationId, x.Name });
        builder.HasIndex(x => x.OrganizationId).IsUnique().HasFilter("[IsDefault] = 1 AND [IsActive] = 1");
        builder.HasOne(x => x.Organization).WithMany(x => x.WorkflowTemplates)
            .HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OrganizationWorkflowStatusConfiguration : IEntityTypeConfiguration<OrganizationWorkflowStatus>
{
    public void Configure(EntityTypeBuilder<OrganizationWorkflowStatus> builder)
    {
        builder.ToTable("OrganizationWorkflowStatuses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Key).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Color).IsRequired().HasMaxLength(7);
        builder.Property(x => x.Category).HasConversion<int>();
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.TemplateId, x.Key }).IsUnique();
        builder.HasOne(x => x.Template).WithMany(x => x.Statuses)
            .HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class OrganizationWorkflowTransitionConfiguration : IEntityTypeConfiguration<OrganizationWorkflowTransition>
{
    public void Configure(EntityTypeBuilder<OrganizationWorkflowTransition> builder)
    {
        builder.ToTable("OrganizationWorkflowTransitions");
        builder.HasKey(x => new { x.SourceStatusId, x.TargetStatusId });
        builder.HasIndex(x => new { x.TemplateId, x.SourceStatusId });
        builder.HasOne(x => x.Template).WithMany(x => x.Transitions)
            .HasForeignKey(x => x.TemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SourceStatus).WithMany()
            .HasForeignKey(x => x.SourceStatusId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TargetStatus).WithMany()
            .HasForeignKey(x => x.TargetStatusId).OnDelete(DeleteBehavior.Restrict);
    }
}
