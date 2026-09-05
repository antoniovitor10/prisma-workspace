using Detran.Kanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detran.Kanban.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração Fluent API para a entidade <see cref="Stage"/>.
/// Mapeia a tabela "Stages" e define restrições, índices e relacionamentos.
/// </summary>
public class StageConfiguration : IEntityTypeConfiguration<Stage>
{
    public void Configure(EntityTypeBuilder<Stage> builder)
    {
        builder.ToTable("Stages");

        builder.HasKey(s => s.Id);

        // --- Propriedades escalares ---

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Position)
            .IsRequired();

        builder.Property(s => s.CreatedAt)
            .IsRequired();

        builder.Property(s => s.Category)
            .HasConversion<int>()
            .IsRequired();

        // --- Índices ---

        builder.HasIndex(s => s.BoardId);
        builder.HasIndex(s => s.WorkflowStatusId);

        // --- Relacionamentos ---

        // Board → Stages já configurado em BoardConfiguration (lado principal).
        // Aqui apenas reforçamos a FK para clareza.
        builder.HasOne(s => s.Board)
            .WithMany(b => b.Stages)
            .HasForeignKey(s => s.BoardId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(s => s.WorkItems)
            .WithOne(w => w.Stage)
            .HasForeignKey(w => w.StageId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        builder.HasOne(s => s.WorkflowStatus)
            .WithMany(s => s.Stages)
            .HasForeignKey(s => s.WorkflowStatusId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
