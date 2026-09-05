using Detran.Kanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detran.Kanban.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuracao Fluent API para a entidade <see cref="TaskEvent"/>.
/// </summary>
public class TaskEventConfiguration : IEntityTypeConfiguration<TaskEvent>
{
    public void Configure(EntityTypeBuilder<TaskEvent> builder)
    {
        builder.ToTable("TaskEvents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ActorId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(x => x.Kind)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(x => x.Payload);

        builder.HasIndex(x => new { x.WorkItemId, x.CreatedAt });

        builder.HasOne(x => x.WorkItem)
            .WithMany()
            .HasForeignKey(x => x.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
