using Detran.Kanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detran.Kanban.Infrastructure.Persistence.Configurations;

public class WikiPageRevisionConfiguration : IEntityTypeConfiguration<WikiPageRevision>
{
    public void Configure(EntityTypeBuilder<WikiPageRevision> builder)
    {
        builder.ToTable("WikiPageRevisions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(300);
        builder.Property(x => x.ContentHtml).IsRequired();
        builder.Property(x => x.AuthorUserId).IsRequired().HasMaxLength(450);

        builder.HasOne(x => x.Page)
            .WithMany()
            .HasForeignKey(x => x.WikiPageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => new { x.WikiPageId, x.UpdatedAt });
    }
}
