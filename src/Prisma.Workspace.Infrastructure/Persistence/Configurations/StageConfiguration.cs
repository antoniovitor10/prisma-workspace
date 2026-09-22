using Prisma.Workspace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Prisma.Workspace.Infrastructure.Persistence.Configurations;

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

        builder.HasIndex(s => s.ProjectId);
        builder.HasIndex(s => s.BoardId);
        builder.HasIndex(s => s.LegacyStageId);
        builder.HasIndex(s => s.WorkflowStatusId);
        builder.HasIndex(s => new { s.ProjectId, s.Position });
        builder.HasIndex(s => new { s.BoardId, s.Position });

        builder.HasOne(s => s.Project)
            .WithMany(p => p.Stages)
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict preserva as etapas legadas e impede apagar quadro com colunas operacionais.
        builder.HasOne(s => s.Board)
            .WithMany(b => b.Stages)
            .HasForeignKey(s => s.BoardId)
            .OnDelete(DeleteBehavior.Restrict);

        // Referência lógica da clone para a etapa legada; nunca gera cascade no histórico.
        builder.HasOne(s => s.LegacyStage)
            .WithMany()
            .HasForeignKey(s => s.LegacyStageId)
            .OnDelete(DeleteBehavior.Restrict);

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
