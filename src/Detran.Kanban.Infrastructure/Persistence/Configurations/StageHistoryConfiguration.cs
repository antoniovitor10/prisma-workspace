using Detran.Kanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detran.Kanban.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuração Fluent API para a entidade <see cref="StageHistory"/>.
/// Mapeia a tabela "StageHistories" e registra o histórico de movimentação
/// de um WorkItem entre etapas (lead time por etapa).
/// StageId usa Restrict para não excluir históricos ao remover uma etapa.
/// </summary>
public class StageHistoryConfiguration : IEntityTypeConfiguration<StageHistory>
{
    public void Configure(EntityTypeBuilder<StageHistory> builder)
    {
        builder.ToTable("StageHistories");

        builder.HasKey(h => h.Id);

        // --- Propriedades escalares ---

        builder.Property(h => h.EnteredAt)
            .IsRequired();

        builder.Property(h => h.ActorId)
            .HasMaxLength(450);

        builder.Property(h => h.ActorName)
            .HasMaxLength(256);

        builder.Property(h => h.Reason)
            .HasMaxLength(500);

        // ExitedAt pode ser nulo (item ainda está na etapa).

        // --- Índices ---

        builder.HasIndex(h => new { h.WorkItemId, h.EnteredAt });

        // --- Relacionamentos ---

        builder.HasOne(h => h.WorkItem)
            .WithMany(w => w.StageHistories)
            .HasForeignKey(h => h.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.Stage)
            .WithMany()
            .HasForeignKey(h => h.StageId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
