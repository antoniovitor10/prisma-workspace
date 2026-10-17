using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Infrastructure.Persistence.Configurations;

public sealed class GitRepositoryIntegrationConfiguration : IEntityTypeConfiguration<GitRepositoryIntegration>
{
    public void Configure(EntityTypeBuilder<GitRepositoryIntegration> b)
    {
        b.ToTable("GitRepositoryIntegrations", t =>
        {
            t.HasCheckConstraint("CK_GitRepositoryIntegrations_Provider", "[Provider] = 'github'");
            t.HasCheckConstraint("CK_GitRepositoryIntegrations_Ids", "[RepositoryId] > 0 AND [AppId] > 0 AND [InstallationId] > 0");
            t.HasCheckConstraint("CK_GitRepositoryIntegrations_Rotation", "([PreviousWebhookSecretRef] IS NULL AND [PreviousSecretValidUntilUtc] IS NULL) OR ([PreviousWebhookSecretRef] IS NOT NULL AND [PreviousSecretValidUntilUtc] IS NOT NULL)");
        });
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.ProjectId, x.Id });
        b.Property(x => x.Provider).HasColumnType("varchar(16)").IsRequired();
        b.Property(x => x.Host).HasMaxLength(255).IsRequired();
        b.Property(x => x.RepositoryFullName).HasMaxLength(300).IsRequired();
        b.Property(x => x.DefaultBranch).HasMaxLength(255).IsRequired();
        b.Property(x => x.AppPrivateKeySecretRef).HasMaxLength(512).IsRequired();
        b.Property(x => x.WebhookSecretRef).HasMaxLength(512).IsRequired();
        b.Property(x => x.PreviousWebhookSecretRef).HasMaxLength(512);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.Provider, x.Host, x.RepositoryId }).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.Enabled });
        b.HasOne(x => x.Settings).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.SystemPrincipal).WithMany().HasForeignKey(x => new { x.ProjectId, x.SystemPrincipalId })
            .HasPrincipalKey(x => new { x.ProjectId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GitPullRequestObservationConfiguration : IEntityTypeConfiguration<GitPullRequestObservation>
{
    public void Configure(EntityTypeBuilder<GitPullRequestObservation> b)
    {
        b.ToTable("GitPullRequestObservations", t => t.HasCheckConstraint("CK_GitPullRequestObservations_Number", "[PullRequestNumber] > 0"));
        b.HasKey(x => x.Id);
        b.HasAlternateKey(x => new { x.ProjectId, x.IntegrationId, x.Id });
        b.Property(x => x.Url).HasMaxLength(1024).IsRequired();
        b.Property(x => x.Title).HasMaxLength(1000).IsRequired();
        b.Property(x => x.HeadBranch).HasMaxLength(255).IsRequired();
        b.Property(x => x.BaseBranch).HasMaxLength(255).IsRequired();
        b.Property(x => x.HeadSha).HasColumnType("varchar(64)").IsRequired();
        b.Property(x => x.MergeSha).HasColumnType("varchar(64)");
        b.Property(x => x.PullRequestAuthorId).HasColumnType("varchar(32)");
        b.Property(x => x.PullRequestAuthorName).HasMaxLength(200);
        b.Property(x => x.State).HasColumnType("varchar(16)").IsRequired();
        b.Property(x => x.UnlinkedReason).HasMaxLength(500);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.IntegrationId, x.PullRequestNumber }).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.State, x.ProviderUpdatedAtUtc });
        b.HasOne(x => x.Integration).WithMany().HasForeignKey(x => new { x.ProjectId, x.IntegrationId })
            .HasPrincipalKey(x => new { x.ProjectId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class TaskGitLinkConfiguration : IEntityTypeConfiguration<TaskGitLink>
{
    public void Configure(EntityTypeBuilder<TaskGitLink> b)
    {
        b.ToTable("TaskGitLinks", t =>
        {
            t.HasCheckConstraint("CK_TaskGitLinks_Target", "[PullRequestObservationId] IS NOT NULL OR [BranchName] IS NOT NULL");
            t.HasCheckConstraint("CK_TaskGitLinks_Source", "[Source] IN ('explicit','branch','title')");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.BranchName).HasMaxLength(255);
        b.Property(x => x.Source).HasColumnType("varchar(16)").IsRequired();
        b.Property(x => x.CreatedByUserId).HasMaxLength(450);
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => x.PullRequestObservationId).IsUnique().HasFilter("[PullRequestObservationId] IS NOT NULL");
        b.HasIndex(x => new { x.IntegrationId, x.BranchName }).IsUnique().HasFilter("[PullRequestObservationId] IS NULL AND [BranchName] IS NOT NULL");
        b.HasIndex(x => new { x.ProjectId, x.WorkItemId });
        b.HasOne(x => x.Integration).WithMany().HasForeignKey(x => new { x.ProjectId, x.IntegrationId })
            .HasPrincipalKey(x => new { x.ProjectId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Observation).WithMany().HasForeignKey(x => new { x.ProjectId, x.IntegrationId, x.PullRequestObservationId })
            .HasPrincipalKey(x => new { x.ProjectId, x.IntegrationId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MethodTaskState>().WithMany().HasForeignKey(x => new { x.ProjectId, x.WorkItemId })
            .HasPrincipalKey(x => new { x.ProjectId, x.WorkItemId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MethodPrincipal>().WithMany().HasForeignKey(x => new { x.ProjectId, x.CreatedByPrincipalId })
            .HasPrincipalKey(x => new { x.ProjectId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GitWebhookDeliveryConfiguration : IEntityTypeConfiguration<GitWebhookDelivery>
{
    public void Configure(EntityTypeBuilder<GitWebhookDelivery> b)
    {
        b.ToTable("GitWebhookDeliveries", t => t.HasCheckConstraint("CK_GitWebhookDeliveries_Attempts", "[Attempts] >= 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Provider).HasColumnType("varchar(16)").IsRequired();
        b.Property(x => x.DeliveryId).HasColumnType("varchar(100)").IsRequired();
        b.Property(x => x.BodyHash).HasColumnType("binary(32)").IsRequired();
        b.Property(x => x.EventName).HasColumnType("varchar(64)").IsRequired();
        b.Property(x => x.EventAction).HasColumnType("varchar(64)");
        b.Property(x => x.NormalizedEventJson).HasColumnType("nvarchar(max)").IsRequired();
        b.Property(x => x.Status).HasColumnType("varchar(24)").IsRequired();
        b.Property(x => x.LastErrorCode).HasColumnType("varchar(64)");
        b.Property(x => x.CorrelationId).HasColumnType("varchar(64)").IsRequired();
        b.Property(x => x.RowVersion).IsRowVersion();
        b.HasIndex(x => new { x.IntegrationId, x.Provider, x.DeliveryId }).IsUnique();
        b.HasIndex(x => new { x.Status, x.NextAttemptAtUtc, x.LeaseUntilUtc });
        b.HasIndex(x => x.ProcessedAtUtc);
        b.HasOne(x => x.Integration).WithMany().HasForeignKey(x => new { x.ProjectId, x.IntegrationId })
            .HasPrincipalKey(x => new { x.ProjectId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class GitAppliedFactConfiguration : IEntityTypeConfiguration<GitAppliedFact>
{
    public void Configure(EntityTypeBuilder<GitAppliedFact> b)
    {
        b.ToTable("GitAppliedFacts");
        b.HasKey(x => x.Id);
        b.Property(x => x.SemanticKey).HasColumnType("binary(32)").IsRequired();
        b.Property(x => x.FactType).HasColumnType("varchar(40)").IsRequired();
        b.Property(x => x.ProviderEntityId).HasMaxLength(128).IsRequired();
        b.Property(x => x.ProviderRevision).HasMaxLength(128).IsRequired();
        b.HasIndex(x => new { x.ProjectId, x.WorkItemId, x.SemanticKey }).IsUnique();
        b.HasIndex(x => x.TaskEventId).IsUnique();
        b.HasIndex(x => new { x.IntegrationId, x.ProviderEntityId });
        b.HasOne(x => x.Integration).WithMany().HasForeignKey(x => new { x.ProjectId, x.IntegrationId })
            .HasPrincipalKey(x => new { x.ProjectId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<MethodTaskState>().WithMany().HasForeignKey(x => new { x.ProjectId, x.WorkItemId })
            .HasPrincipalKey(x => new { x.ProjectId, x.WorkItemId }).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Stage>().WithMany().HasForeignKey(x => x.FromStageId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Stage>().WithMany().HasForeignKey(x => x.ToStageId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TaskEvent>().WithMany().HasForeignKey(x => x.TaskEventId).OnDelete(DeleteBehavior.Restrict);
        // DeliveryId não tem FK: o expurgo operacional não apaga nem bloqueia a prova permanente.
    }
}
