using Prisma.Workspace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Prisma.Workspace.Infrastructure.Persistence.Configurations;

public class WikiAttachmentConfiguration : IEntityTypeConfiguration<WikiAttachment>
{
    public void Configure(EntityTypeBuilder<WikiAttachment> builder)
    {
        builder.ToTable("WikiAttachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StoragePath).IsRequired().HasMaxLength(500);
        builder.Property(x => x.FileName).IsRequired().HasMaxLength(300);
        builder.Property(x => x.MimeType).HasMaxLength(150);
        builder.Property(x => x.UploadedByUserId).IsRequired().HasMaxLength(450);

        builder.HasOne(x => x.Page)
            .WithMany()
            .HasForeignKey(x => x.WikiPageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.WikiPageId);
    }
}
