using Detran.Kanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detran.Kanban.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração Fluent API para a entidade <see cref="Attachment"/>.
/// Mapeia a tabela "Attachments" e define restrições, índices e relacionamentos.
/// </summary>
public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("Attachments");

        builder.HasKey(a => a.Id);

        // --- Propriedades escalares ---

        builder.Property(a => a.StoragePath)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(a => a.FileName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(a => a.MimeType)
            .HasMaxLength(200);

        builder.Property(a => a.UploadedBy)
            .HasMaxLength(450);

        builder.Property(a => a.CreatedAt)
            .IsRequired();

        builder.Property(a => a.IsExternalVisible)
            .HasDefaultValue(false);

        // --- Índices ---

        builder.HasIndex(a => a.WorkItemId);

        // --- Relacionamentos ---

        builder.HasOne(a => a.WorkItem)
            .WithMany(w => w.Attachments)
            .HasForeignKey(a => a.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
