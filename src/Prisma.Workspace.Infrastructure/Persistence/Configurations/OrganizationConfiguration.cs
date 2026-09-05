using Prisma.Workspace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Prisma.Workspace.Infrastructure.Persistence.Configurations;

public class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("Organizations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(160);
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Locale).IsRequired().HasMaxLength(20);
        builder.Property(x => x.TimeZone).IsRequired().HasMaxLength(80);
        builder.Property(x => x.WeekStartDay).HasConversion<int>();
        builder.HasIndex(x => x.Slug).IsUnique();
    }
}

public class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.ToTable("OrganizationMembers");
        builder.HasKey(x => new { x.OrganizationId, x.UserId });
        builder.Property(x => x.UserId).HasMaxLength(450);
        builder.Property(x => x.DisplayName).HasMaxLength(200);
        builder.Property(x => x.Role).HasConversion<int>();
        builder.HasIndex(x => new { x.UserId, x.IsActive });
        builder.HasOne(x => x.Organization).WithMany(x => x.Members)
            .HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrganizationInvitationConfiguration : IEntityTypeConfiguration<OrganizationInvitation>
{
    public void Configure(EntityTypeBuilder<OrganizationInvitation> builder)
    {
        builder.ToTable("OrganizationInvitations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
        builder.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);
        builder.Property(x => x.InvitedBy).IsRequired().HasMaxLength(450);
        builder.Property(x => x.Role).HasConversion<int>();
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasIndex(x => new { x.OrganizationId, x.Email });
        builder.HasOne(x => x.Organization).WithMany(x => x.Invitations)
            .HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PermissionGrantConfiguration : IEntityTypeConfiguration<PermissionGrant>
{
    public void Configure(EntityTypeBuilder<PermissionGrant> builder)
    {
        builder.ToTable("PermissionGrants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.GrantedBy).IsRequired().HasMaxLength(450);
        builder.Property(x => x.Scope).HasConversion<int>();
        builder.Property(x => x.Permission).HasConversion<int>();
        builder.HasIndex(x => new { x.OrganizationId, x.UserId, x.Scope, x.ScopeId, x.Permission })
            .IsUnique()
            .HasFilter(null);
        builder.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
