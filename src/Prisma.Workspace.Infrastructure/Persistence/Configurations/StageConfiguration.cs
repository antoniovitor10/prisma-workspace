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
        builder.HasIndex(s => s.WorkflowStatusId);
        builder.HasIndex(s => new { s.ProjectId, s.Position });

        builder.HasOne(s => s.Project)
            .WithMany(p => p.Stages)
            .HasForeignKey(s => s.ProjectId)
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
