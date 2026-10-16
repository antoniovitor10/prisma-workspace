using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Infrastructure.Persistence.Configurations;

public sealed class MethodProjectSettingsConfiguration : IEntityTypeConfiguration<MethodProjectSettings>
{
    public void Configure(EntityTypeBuilder<MethodProjectSettings> b)
    {
        b.ToTable("MethodProjectSettings", t =>
        {
            t.HasCheckConstraint("CK_MethodProjectSettings_Autonomy", "[AutonomyLevel] BETWEEN 0 AND 2");
            t.HasCheckConstraint("CK_MethodProjectSettings_Phase", "[GitPhase] BETWEEN 0 AND 3");
        });
        b.HasKey(x => x.ProjectId);
        b.Property(x => x.GitPhase).HasConversion<byte>();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.BoardId).IsUnique().HasFilter("[BoardId] IS NOT NULL");
        b.HasIndex(x => new { x.OrganizationId, x.Enabled });
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Board>().WithMany().HasForeignKey(x => x.BoardId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProjectCustomFieldDefinition>().WithMany().HasForeignKey(x => x.MethodIdFieldId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProjectCustomFieldDefinition>().WithMany().HasForeignKey(x => x.HuFieldId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ProjectCustomFieldDefinition>().WithMany().HasForeignKey(x => x.PrFieldId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MethodStageMappingConfiguration : IEntityTypeConfiguration<MethodStageMapping>
{
    public void Configure(EntityTypeBuilder<MethodStageMapping> b)
    {
        b.ToTable("MethodStageMappings", t => t.HasCheckConstraint("CK_MethodStageMappings_Status",
            "[StatusCode] IN ('backlog','pronta','em-desenvolvimento','em-revisao','em-teste','concluida','bloqueada')"));
        b.HasKey(x => new { x.ProjectId, x.StatusCode });
        b.Property(x => x.StatusCode).HasColumnType("varchar(32)");
        b.HasIndex(x => new { x.ProjectId, x.StageId }).IsUnique();
        b.HasOne(x => x.Settings).WithMany(x => x.StageMappings).HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Stage>().WithMany().HasForeignKey(x => x.StageId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MethodPrincipalConfiguration : IEntityTypeConfiguration<MethodPrincipal>
{
    public void Configure(EntityTypeBuilder<MethodPrincipal> b)
    {
        b.ToTable("MethodPrincipals", t =>
        {
            t.HasCheckConstraint("CK_MethodPrincipals_Role", "[Role] IN (1,2,3)");
            t.HasCheckConstraint("CK_MethodPrincipals_Human", "([Role] = 1 AND [HumanUserId] IS NOT NULL AND [HumanUserId] = [DelegatedByUserId]) OR ([Role] IN (2,3) AND [HumanUserId] IS NULL)");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.ProjectId, x.Id });
        b.Property(x => x.Role).HasConversion<byte>();
        b.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        b.Property(x => x.HumanUserId).HasMaxLength(450);
        b.Property(x => x.DelegatedByUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.PermissionCeilingJson).HasMaxLength(3000).IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.ProjectId, x.Role, x.IsActive });
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.HumanUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.DelegatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MethodApiTokenConfiguration : IEntityTypeConfiguration<MethodApiToken>
{
    public void Configure(EntityTypeBuilder<MethodApiToken> b)
    {
        b.ToTable("MethodApiTokens", t => t.HasCheckConstraint("CK_MethodApiTokens_Expiry", "[ExpiresAtUtc] > [IssuedAtUtc]"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(120).IsRequired();
        b.Property(x => x.TokenHash).HasColumnType("binary(32)").IsRequired();
        b.Property(x => x.TokenPrefix).HasColumnType("varchar(24)").IsRequired();
        b.Property(x => x.IssuedByUserId).HasMaxLength(450).IsRequired();
        b.Property(x => x.RevokedByUserId).HasMaxLength(450);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.PrincipalId, x.RevokedAtUtc, x.ExpiresAtUtc });
        b.HasOne(x => x.Principal).WithMany().HasForeignKey(x => new { x.ProjectId, x.PrincipalId })
            .HasPrincipalKey(x => new { x.ProjectId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.IssuedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.RevokedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MethodAuthorizationEventConfiguration : IEntityTypeConfiguration<MethodAuthorizationEvent>
{
    public void Configure(EntityTypeBuilder<MethodAuthorizationEvent> b)
    {
        b.ToTable("MethodAuthorizationEvents", t =>
        {
            t.HasCheckConstraint("CK_MethodAuthorizationEvents_Kind", "[EventKind] IN ('Granted','Revoked','Blocked','ResumeAllowed','Resumed')");
            t.HasCheckConstraint("CK_MethodAuthorizationEvents_Actor", "[ActorPrincipalId] IS NOT NULL OR [ActorUserId] IS NOT NULL");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.ProjectId, x.WorkItemId, x.CycleId, x.Id });
        b.Property(x => x.EventKind).HasColumnType("varchar(24)").IsRequired();
        b.Property(x => x.ActorName).HasMaxLength(200).IsRequired();
        b.Property(x => x.ActorUserId).HasMaxLength(450);
        b.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        b.HasIndex(x => new { x.ProjectId, x.WorkItemId, x.CreatedAtUtc, x.Id });
        b.HasIndex(x => x.TaskEventId).IsUnique();
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkItem>().WithMany().HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TaskEvent>().WithMany().HasForeignKey(x => x.TaskEventId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MethodPrincipal>().WithMany().HasForeignKey(x => new { x.ProjectId, x.ActorPrincipalId })
            .HasPrincipalKey(x => new { x.ProjectId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<IdentityUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MethodAuthorizationEvent>().WithMany().HasForeignKey(x => new { x.ProjectId, x.WorkItemId, x.CycleId, x.RelatedEventId })
            .HasPrincipalKey(x => new { x.ProjectId, x.WorkItemId, x.CycleId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MethodTaskStateConfiguration : IEntityTypeConfiguration<MethodTaskState>
{
    public void Configure(EntityTypeBuilder<MethodTaskState> b)
    {
        b.ToTable("MethodTaskStates");
        b.HasKey(x => new { x.ProjectId, x.WorkItemId });
        b.Property(x => x.MethodTaskId).HasColumnType("varchar(32)").IsRequired(false);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.ProjectId, x.MethodTaskId }).IsUnique().HasFilter("[MethodTaskId] IS NOT NULL");
        b.HasIndex(x => x.WorkItemId);
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WorkItem>().WithMany().HasForeignKey(x => x.WorkItemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MethodPrincipal>().WithMany().HasForeignKey(x => new { x.ProjectId, x.BlockingPrincipalId })
            .HasPrincipalKey(x => new { x.ProjectId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MethodAuthorizationEvent>().WithMany().HasForeignKey(x => new { x.ProjectId, x.WorkItemId, x.CycleId, x.ActiveAuthorizationEventId })
            .HasPrincipalKey(x => new { x.ProjectId, x.WorkItemId, x.CycleId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MethodAuthorizationEvent>().WithMany().HasForeignKey(x => new { x.ProjectId, x.WorkItemId, x.CycleId, x.BlockingEventId })
            .HasPrincipalKey(x => new { x.ProjectId, x.WorkItemId, x.CycleId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MethodAuthorizationEvent>().WithMany().HasForeignKey(x => new { x.ProjectId, x.WorkItemId, x.CycleId, x.ResumeApprovalEventId })
            .HasPrincipalKey(x => new { x.ProjectId, x.WorkItemId, x.CycleId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}
