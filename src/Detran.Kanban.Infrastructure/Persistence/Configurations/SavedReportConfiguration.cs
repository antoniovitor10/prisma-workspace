using Detran.Kanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detran.Kanban.Infrastructure.Persistence.Configurations;

public class SavedReportConfiguration : IEntityTypeConfiguration<SavedReport>
{
    public void Configure(EntityTypeBuilder<SavedReport> builder)
    {
        builder.ToTable("SavedReports");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OwnerId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Source).HasConversion<int>();
        builder.Property(x => x.Visualization).HasConversion<int>();
        builder.Property(x => x.DefinitionJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.OrganizationId, x.OwnerId, x.UpdatedAt });
        builder.HasIndex(x => new { x.OrganizationId, x.IsShared, x.UpdatedAt });
        builder.HasIndex(x => x.ProjectId);
        builder.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}
