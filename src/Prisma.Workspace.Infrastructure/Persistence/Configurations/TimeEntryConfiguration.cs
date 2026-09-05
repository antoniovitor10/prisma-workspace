using Prisma.Workspace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Prisma.Workspace.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração Fluent API para a entidade <see cref="TimeEntry"/>.
/// Mapeia a tabela "TimeEntries" e define restrições, índices e relacionamentos.
/// A propriedade DurationSeconds é ignorada pois é calculada em tempo de execução.
/// </summary>
public class TimeEntryConfiguration : IEntityTypeConfiguration<TimeEntry>
{
    public void Configure(EntityTypeBuilder<TimeEntry> builder)
    {
        builder.ToTable("TimeEntries");

        builder.HasKey(t => t.Id);

        // --- Propriedades escalares ---

        builder.Property(t => t.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(t => t.Note)
            .HasMaxLength(2000);

        builder.Property(t => t.StartedAt)
            .IsRequired();

        // DurationSeconds é propriedade computada (não persistida).
        builder.Ignore(t => t.DurationSeconds);

        // --- Índices ---

        builder.HasIndex(t => t.WorkItemId);
        builder.HasIndex(t => t.UserId);

        // --- Relacionamentos ---

        builder.HasOne(t => t.WorkItem)
            .WithMany(w => w.TimeEntries)
            .HasForeignKey(t => t.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
