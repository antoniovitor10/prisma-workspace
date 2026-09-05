using Prisma.Workspace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Prisma.Workspace.Infrastructure.Persistence.Configurations;

public class TaskTypeConfiguration : IEntityTypeConfiguration<TaskType>
{
    public void Configure(EntityTypeBuilder<TaskType> builder)
    {
        builder.ToTable("TaskTypes");
        builder.HasKey(t => t.Id);
        builder.HasIndex(t => t.OrganizationId);
        builder.HasOne<Organization>().WithMany().HasForeignKey(t => t.OrganizationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(120);
        builder.Property(t => t.Color).IsRequired().HasMaxLength(20);
        builder.Property(t => t.CreatedAt).IsRequired();
    }
}
