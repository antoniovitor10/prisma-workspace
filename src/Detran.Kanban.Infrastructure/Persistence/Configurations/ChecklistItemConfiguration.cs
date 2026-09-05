using Detran.Kanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detran.Kanban.Infrastructure.Persistence.Configurations;

public class ChecklistItemConfiguration : IEntityTypeConfiguration<ChecklistItem>
{
    public void Configure(EntityTypeBuilder<ChecklistItem> builder)
    {
        builder.ToTable("ChecklistItems");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Text).IsRequired().HasMaxLength(500);
        builder.Property(c => c.CreatedAt).IsRequired();
        builder.HasIndex(c => c.WorkItemId);

        builder.HasOne(c => c.WorkItem)
            .WithMany(w => w.ChecklistItems)
            .HasForeignKey(c => c.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
