using Prisma.Workspace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Prisma.Workspace.Infrastructure.Persistence.Configurations;

public sealed class WorkflowStatusConfiguration : IEntityTypeConfiguration<WorkflowStatus>
{
    public void Configure(EntityTypeBuilder<WorkflowStatus> builder)
    {
        builder.ToTable("WorkflowStatuses");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Color).IsRequired().HasMaxLength(7);
        builder.Property(x => x.Category).HasConversion<int>().IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();

        builder.HasIndex(x => new { x.ProjectId, x.Position });
        builder.HasIndex(x => new { x.ProjectId, x.Name });
        builder.HasIndex(x => new { x.ProjectId, x.OrganizationWorkflowStatusId })
            .IsUnique().HasFilter("[OrganizationWorkflowStatusId] IS NOT NULL");

        builder.HasOne(x => x.Project)
            .WithMany(x => x.WorkflowStatuses)
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.OrganizationWorkflowStatus)
            .WithMany(x => x.ProjectStatuses)
            .HasForeignKey(x => x.OrganizationWorkflowStatusId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.ToTable("WorkflowTransitions");
        builder.HasKey(x => new { x.SourceStatusId, x.TargetStatusId });
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => x.TargetStatusId);

        builder.HasOne(x => x.SourceStatus)
            .WithMany(x => x.OutgoingTransitions)
            .HasForeignKey(x => x.SourceStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TargetStatus)
            .WithMany(x => x.IncomingTransitions)
            .HasForeignKey(x => x.TargetStatusId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
