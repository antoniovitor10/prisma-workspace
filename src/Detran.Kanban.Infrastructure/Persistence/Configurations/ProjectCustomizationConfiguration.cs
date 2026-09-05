using Detran.Kanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detran.Kanban.Infrastructure.Persistence.Configurations;

public class ProjectTagConfiguration : IEntityTypeConfiguration<ProjectTag>
{
    public void Configure(EntityTypeBuilder<ProjectTag> builder)
    {
        builder.ToTable("ProjectTags");
        builder.HasKey(x => new { x.ProjectId, x.TagId });
        builder.HasOne(x => x.Project).WithMany(x => x.Tags).HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Tag).WithMany().HasForeignKey(x => x.TagId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => x.TagId);
    }
}

public class ProjectCustomFieldDefinitionConfiguration : IEntityTypeConfiguration<ProjectCustomFieldDefinition>
{
    public void Configure(EntityTypeBuilder<ProjectCustomFieldDefinition> builder)
    {
        builder.ToTable("ProjectCustomFields");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Type).HasConversion<int>().IsRequired();
        builder.Property(x => x.OptionsJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.ProjectId, x.IsActive, x.Position });
        builder.HasOne(x => x.Project).WithMany(x => x.CustomFields).HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ProjectEventConfiguration : IEntityTypeConfiguration<ProjectEvent>
{
    public void Configure(EntityTypeBuilder<ProjectEvent> builder)
    {
        builder.ToTable("ProjectEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActorId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.Kind).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Payload).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.ProjectId, x.CreatedAt });
        builder.HasOne(x => x.Project).WithMany(x => x.Events).HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
