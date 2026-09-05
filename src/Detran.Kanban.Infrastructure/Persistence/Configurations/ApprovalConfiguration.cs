using Detran.Kanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detran.Kanban.Infrastructure.Persistence.Configurations;

public class ApprovalConfiguration : IEntityTypeConfiguration<Approval>
{
    public void Configure(EntityTypeBuilder<Approval> builder)
    {
        builder.ToTable("Approvals");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.RequesterId).IsRequired().HasMaxLength(450);
        builder.Property(a => a.ApproverId).IsRequired().HasMaxLength(450);
        builder.Property(a => a.Note).HasMaxLength(1000);
        builder.Property(a => a.CreatedAt).IsRequired();
        builder.HasIndex(a => a.WorkItemId);
        builder.HasIndex(a => new { a.ApproverId, a.Status });

        builder.HasOne(a => a.WorkItem)
            .WithMany()
            .HasForeignKey(a => a.WorkItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
