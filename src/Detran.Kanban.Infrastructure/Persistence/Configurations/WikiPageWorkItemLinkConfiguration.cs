using Detran.Kanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detran.Kanban.Infrastructure.Persistence.Configurations;

public class WikiPageWorkItemLinkConfiguration : IEntityTypeConfiguration<WikiPageWorkItemLink>
{
    public void Configure(EntityTypeBuilder<WikiPageWorkItemLink> builder)
    {
        builder.ToTable("WikiPageWorkItemLinks");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CreatedByUserId).IsRequired().HasMaxLength(450);

        builder.HasOne(x => x.Page)
            .WithMany()
            .HasForeignKey(x => x.WikiPageId)
            .OnDelete(DeleteBehavior.Cascade);

        // WorkItem via NoAction para evitar múltiplos caminhos de cascata no SQL Server.
        builder.HasOne(x => x.WorkItem)
            .WithMany()
            .HasForeignKey(x => x.WorkItemId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(x => new { x.WikiPageId, x.WorkItemId }).IsUnique();
        builder.HasIndex(x => x.WorkItemId);
    }
}
