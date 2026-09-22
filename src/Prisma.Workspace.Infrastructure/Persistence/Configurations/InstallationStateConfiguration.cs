using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Infrastructure.Persistence.Configurations;

public sealed class InstallationStateConfiguration : IEntityTypeConfiguration<InstallationState>
{
    public void Configure(EntityTypeBuilder<InstallationState> builder)
    {
        builder.ToTable("InstallationStates", table =>
            table.HasCheckConstraint("CK_InstallationStates_Singleton", "[Id] = 1"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Version).IsRowVersion();
    }
}
