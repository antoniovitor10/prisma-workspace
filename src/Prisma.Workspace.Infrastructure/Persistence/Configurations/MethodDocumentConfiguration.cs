using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Infrastructure.Persistence.Configurations;

public sealed class MethodDocumentConfiguration : IEntityTypeConfiguration<MethodDocument>
{
    public void Configure(EntityTypeBuilder<MethodDocument> b)
    {
        b.ToTable("MethodDocuments", t =>
        {
            t.HasCheckConstraint("CK_MethodDocuments_Kind", "[Kind] IN (1, 2, 3, 4)");
            t.HasCheckConstraint("CK_MethodDocuments_Via", "[UpdatedVia] IN ('ui', 'mcp')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasConversion<int>();
        b.Property(x => x.MethodKey).HasMaxLength(32);
        b.Property(x => x.FilePath).HasMaxLength(400);
        b.Property(x => x.Content).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(x => x.ContentHash).HasColumnType("char(64)").IsRequired();
        b.Property(x => x.Status).HasMaxLength(20);
        b.Property(x => x.UpdatedByUserId).HasMaxLength(450);
        b.Property(x => x.UpdatedByName).HasMaxLength(200).IsRequired();
        b.Property(x => x.UpdatedVia).HasColumnType("varchar(8)").IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.WorkItemId, x.Kind }).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.MethodKey }).IsUnique().HasFilter("[MethodKey] IS NOT NULL");
        b.HasIndex(x => new { x.ProjectId, x.UpdatedAtUtc });
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.WorkItem).WithMany().HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MethodDocumentRevisionConfiguration : IEntityTypeConfiguration<MethodDocumentRevision>
{
    public void Configure(EntityTypeBuilder<MethodDocumentRevision> b)
    {
        b.ToTable("MethodDocumentRevisions", t =>
            t.HasCheckConstraint("CK_MethodDocumentRevisions_Via", "[Via] IN ('ui', 'mcp')"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Content).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(x => x.ContentHash).HasColumnType("char(64)").IsRequired();
        b.Property(x => x.AuthorUserId).HasMaxLength(450);
        b.Property(x => x.AuthorName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Via).HasColumnType("varchar(8)").IsRequired();
        b.HasIndex(x => new { x.DocumentId, x.Version }).IsUnique();
        b.HasIndex(x => x.ProjectId);
        b.HasOne(x => x.Document).WithMany(x => x.Revisions).HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
