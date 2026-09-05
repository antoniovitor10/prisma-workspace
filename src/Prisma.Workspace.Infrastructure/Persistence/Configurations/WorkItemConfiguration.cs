using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Prisma.Workspace.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração Fluent API para a entidade <see cref="WorkItem"/>.
/// Mapeia a tabela "WorkItems" e define restrições, índices e relacionamentos.
/// ParentId usa Restrict para evitar múltiplos caminhos de cascade no SQL Server.
/// </summary>
public class WorkItemConfiguration : IEntityTypeConfiguration<WorkItem>
{
    public void Configure(EntityTypeBuilder<WorkItem> builder)
    {
        builder.ToTable("WorkItems");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.Number)
            .ValueGeneratedOnAdd()
            .HasDefaultValueSql("NEXT VALUE FOR [WorkItemNumbers]");

        // --- Propriedades escalares ---

        builder.Property(w => w.Title)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(w => w.Subtitle)
            .HasMaxLength(300);

        builder.Property(w => w.Priority)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(w => w.Kind)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(w => w.Origin)
            .HasConversion<int>()
            .HasDefaultValue(WorkItemOrigin.Internal)
            .IsRequired();

        builder.Property(w => w.ResponsibleId).HasMaxLength(450);
        builder.Property(w => w.RequesterId).HasMaxLength(450);
        builder.Property(w => w.RequesterName).HasMaxLength(200);
        builder.Property(w => w.RequesterEmail).HasMaxLength(320);
        builder.Property(w => w.AcceptanceCriteria).HasColumnType("nvarchar(max)");

        builder.Property(w => w.EstimatedHours)
            .HasColumnType("decimal(6,2)");

        builder.Property(w => w.RemainingHours)
            .HasColumnType("decimal(6,2)");

        builder.Property(w => w.BacklogRank)
            .HasColumnType("decimal(18,6)");

        builder.Property(w => w.RowVersion)
            .IsRowVersion();

        builder.Property(w => w.CreatedBy)
            .HasMaxLength(450);

        builder.Property(w => w.CreatedAt)
            .IsRequired();

        // --- Índices ---

        builder.HasIndex(w => w.BoardId);
        builder.HasIndex(w => w.StageId);
        builder.HasIndex(w => w.ParentId);
        builder.HasIndex(w => w.SprintId);
        builder.HasIndex(w => w.TeamId);
        builder.HasIndex(w => w.WorkflowStatusId);
        builder.HasIndex(w => w.ResponsibleId);
        builder.HasIndex(w => w.Number).IsUnique();
        builder.HasIndex(w => new { w.BoardId, w.IsArchived });
        builder.HasIndex(w => new { w.BoardId, w.BacklogRank });

        // --- Relacionamentos ---

        // Board → WorkItems home: Restrict — exclusão de quadro nunca apaga tarefas (D50).
        builder.HasOne(w => w.Board)
            .WithMany(b => b.WorkItems)
            .HasForeignKey(w => w.BoardId)
            .OnDelete(DeleteBehavior.Restrict);

        // Stage -> WorkItems: sem cascade no banco para evitar multiplos caminhos no SQL Server.
        builder.HasOne(w => w.Stage)
            .WithMany(s => s.WorkItems)
            .HasForeignKey(w => w.StageId)
            .OnDelete(DeleteBehavior.ClientSetNull);

        // WorkItem pai → Subtarefas (Restrict — evita múltiplos caminhos de cascade).
        builder.HasOne(w => w.Parent)
            .WithMany(w => w.SubItems)
            .HasForeignKey(w => w.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // TaskType → WorkItems (SetNull ao excluir o tipo).
        builder.HasOne(w => w.TaskType)
            .WithMany()
            .HasForeignKey(w => w.TaskTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(w => w.TaskTypeId);

        builder.HasOne(w => w.Sprint)
            .WithMany(s => s.WorkItems)
            .HasForeignKey(w => w.SprintId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(w => w.Team)
            .WithMany()
            .HasForeignKey(w => w.TeamId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(w => w.WorkflowStatus)
            .WithMany(s => s.WorkItems)
            .HasForeignKey(w => w.WorkflowStatusId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
