using Detran.Kanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detran.Kanban.Infrastructure.Persistence.Configurations;

public class WorkItemLinkConfiguration : IEntityTypeConfiguration<WorkItemLink>
{
    public void Configure(EntityTypeBuilder<WorkItemLink> builder)
    {
        builder.ToTable("WorkItemLinks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasConversion<int>().IsRequired();
        builder.Property(x => x.CreatedBy).IsRequired().HasMaxLength(450);
        builder.HasIndex(x => new { x.SourceWorkItemId, x.TargetWorkItemId, x.Type }).IsUnique();
        builder.HasIndex(x => x.TargetWorkItemId);
        builder.HasOne(x => x.SourceWorkItem).WithMany(x => x.OutgoingLinks)
            .HasForeignKey(x => x.SourceWorkItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TargetWorkItem).WithMany(x => x.IncomingLinks)
            .HasForeignKey(x => x.TargetWorkItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class WorkItemFollowerConfiguration : IEntityTypeConfiguration<WorkItemFollower>
{
    public void Configure(EntityTypeBuilder<WorkItemFollower> builder)
    {
        builder.ToTable("WorkItemFollowers");
        builder.HasKey(x => new { x.WorkItemId, x.UserId });
        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.HasIndex(x => x.UserId);
        builder.HasOne(x => x.WorkItem).WithMany(x => x.Followers)
            .HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class WorkItemCustomFieldValueConfiguration : IEntityTypeConfiguration<WorkItemCustomFieldValue>
{
    public void Configure(EntityTypeBuilder<WorkItemCustomFieldValue> builder)
    {
        builder.ToTable("WorkItemCustomFieldValues");
        builder.HasKey(x => new { x.WorkItemId, x.FieldDefinitionId });
        builder.Property(x => x.Value).HasColumnType("nvarchar(max)");
        builder.Property(x => x.UpdatedBy).IsRequired().HasMaxLength(450);
        builder.HasIndex(x => x.FieldDefinitionId);
        builder.HasOne(x => x.WorkItem).WithMany(x => x.CustomFieldValues)
            .HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.FieldDefinition).WithMany(x => x.Values)
            .HasForeignKey(x => x.FieldDefinitionId).OnDelete(DeleteBehavior.Restrict);
    }
}
