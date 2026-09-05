using Detran.Kanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detran.Kanban.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração Fluent API para a entidade <see cref="WorkItemAssignee"/>.
/// Tabela de junção entre WorkItem e usuário (chave composta).
/// </summary>
public class WorkItemAssigneeConfiguration : IEntityTypeConfiguration<WorkItemAssignee>
{
    public void Configure(EntityTypeBuilder<WorkItemAssignee> builder)
    {
        builder.ToTable("WorkItemAssignees");

        // Chave composta (WorkItemId + UserId).
        builder.HasKey(a => new { a.WorkItemId, a.UserId });

        // --- Propriedades escalares ---

        builder.Property(a => a.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(a => a.AssignedAt)
            .IsRequired();

        // --- Relacionamentos ---

        builder.HasOne(a => a.WorkItem)
            .WithMany(w => w.Assignees)
            .HasForeignKey(a => a.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
