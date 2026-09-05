using Detran.Kanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detran.Kanban.Infrastructure.Persistence.Configurations;

public class WorkItemBoardPlacementConfiguration : IEntityTypeConfiguration<WorkItemBoardPlacement>
{
    public void Configure(EntityTypeBuilder<WorkItemBoardPlacement> builder)
    {
        builder.ToTable("WorkItemBoardPlacements");
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.WorkItemId, x.BoardId }).IsUnique();
        builder.HasIndex(x => x.BoardId);
        builder.HasIndex(x => x.StageId);
        builder.HasIndex(x => new { x.BoardId, x.StageId, x.Position });

        builder.HasOne(x => x.WorkItem)
            .WithMany(w => w.BoardPlacements)
            .HasForeignKey(x => x.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Board)
            .WithMany(b => b.Placements)
            .HasForeignKey(x => x.BoardId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Stage)
            .WithMany()
            .HasForeignKey(x => x.StageId)
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
