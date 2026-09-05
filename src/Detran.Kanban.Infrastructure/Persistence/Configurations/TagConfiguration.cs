using Detran.Kanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detran.Kanban.Infrastructure.Persistence.Configurations;

public class TagConfiguration : IEntityTypeConfiguration<Tag>
{
    public void Configure(EntityTypeBuilder<Tag> builder)
    {
        builder.ToTable("Tags");
        builder.HasKey(t => t.Id);
        builder.HasIndex(t => t.OrganizationId);
        builder.HasOne<Organization>().WithMany().HasForeignKey(t => t.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(80);
        builder.Property(t => t.Color).IsRequired().HasMaxLength(20);
        builder.Property(t => t.CreatedAt).IsRequired();
    }
}

public class WorkItemTagConfiguration : IEntityTypeConfiguration<WorkItemTag>
{
    public void Configure(EntityTypeBuilder<WorkItemTag> builder)
    {
        builder.ToTable("WorkItemTags");
        builder.HasKey(wt => new { wt.WorkItemId, wt.TagId });

        builder.HasOne(wt => wt.WorkItem)
            .WithMany(w => w.WorkItemTags)
            .HasForeignKey(wt => wt.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(wt => wt.Tag)
            .WithMany(t => t.WorkItemTags)
            .HasForeignKey(wt => wt.TagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
