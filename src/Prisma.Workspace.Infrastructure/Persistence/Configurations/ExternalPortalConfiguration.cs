using Prisma.Workspace.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Prisma.Workspace.Infrastructure.Persistence.Configurations;

public class ExternalPortalConfiguration : IEntityTypeConfiguration<ExternalPortal>
{
    public void Configure(EntityTypeBuilder<ExternalPortal> builder)
    {
        builder.ToTable("ExternalPortals");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PublicSlug).IsRequired().HasMaxLength(80);
        builder.Property(x => x.AccessModes).HasConversion<int>();
        builder.HasIndex(x => x.ProjectId).IsUnique();
        builder.HasIndex(x => x.PublicSlug).IsUnique();
        builder.HasOne(x => x.Project).WithOne().HasForeignKey<ExternalPortal>(x => x.ProjectId)
            .OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Board).WithMany().HasForeignKey(x => x.BoardId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}

public class ExternalRequestConfiguration : IEntityTypeConfiguration<ExternalRequest>
{
    public void Configure(EntityTypeBuilder<ExternalRequest> builder)
    {
        builder.ToTable("ExternalRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Protocol).IsRequired().HasMaxLength(32);
        builder.Property(x => x.AccessKeyHash).IsRequired().HasMaxLength(64);
        builder.Property(x => x.RequesterEmail).IsRequired().HasMaxLength(320);
        builder.Property(x => x.RequesterPhone).HasMaxLength(30);
        builder.Property(x => x.Category).HasMaxLength(120);
        builder.Property(x => x.RelatedService).HasMaxLength(200);
        builder.Property(x => x.SubmittedValuesJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.TriageStatus).HasConversion<int>()
            .HasDefaultValue(Prisma.Workspace.Domain.Enums.ExternalRequestTriageStatus.New);
        builder.Property(x => x.RatingComment).HasMaxLength(1000);
        builder.Property(x => x.SlaPolicySnapshotJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => x.Protocol).IsUnique();
        builder.HasIndex(x => x.WorkItemId).IsUnique();
        builder.HasIndex(x => x.ExternalFormId);
        builder.HasIndex(x => new { x.ExternalPortalId, x.CreatedAt });
        builder.HasIndex(x => x.FirstResponseDueAt);
        builder.HasIndex(x => x.ResolutionDueAt);
        builder.HasOne(x => x.ExternalPortal).WithMany(x => x.Requests)
            .HasForeignKey(x => x.ExternalPortalId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.WorkItem).WithOne(x => x.ExternalRequest)
            .HasForeignKey<ExternalRequest>(x => x.WorkItemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.ExternalForm).WithMany(x => x.Requests)
            .HasForeignKey(x => x.ExternalFormId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class ExternalFormConfiguration : IEntityTypeConfiguration<ExternalForm>
{
    public void Configure(EntityTypeBuilder<ExternalForm> builder)
    {
        builder.ToTable("ExternalForms");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PublicSlug).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.Category).HasMaxLength(120);
        builder.Property(x => x.ConfirmationMessage).HasMaxLength(1000);
        builder.Property(x => x.DefaultPriority).HasConversion<int>();
        builder.Property(x => x.DefaultResponsibleId).HasMaxLength(450);
        builder.Property(x => x.AllowedExtensions).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.AllowedMimeTypes).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.FieldsJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(x => x.AssignmentRulesJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.ExternalPortalId, x.PublicSlug }).IsUnique();
        builder.HasIndex(x => new { x.ExternalPortalId, x.IsDefault });
        builder.HasOne(x => x.ExternalPortal).WithMany(x => x.Forms)
            .HasForeignKey(x => x.ExternalPortalId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.InitialStage).WithMany()
            .HasForeignKey(x => x.InitialStageId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.DefaultTeam).WithMany()
            .HasForeignKey(x => x.DefaultTeamId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class ExternalRequestTriageEventConfiguration : IEntityTypeConfiguration<ExternalRequestTriageEvent>
{
    public void Configure(EntityTypeBuilder<ExternalRequestTriageEvent> builder)
    {
        builder.ToTable("ExternalRequestTriageEvents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasConversion<int>();
        builder.Property(x => x.ActorId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.ActorName).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.DataJson).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.ExternalRequestId, x.CreatedAt });
        builder.HasOne(x => x.ExternalRequest).WithMany(x => x.TriageEvents)
            .HasForeignKey(x => x.ExternalRequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExternalRequestMessageConfiguration : IEntityTypeConfiguration<ExternalRequestMessage>
{
    public void Configure(EntityTypeBuilder<ExternalRequestMessage> builder)
    {
        builder.ToTable("ExternalRequestMessages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AuthorType).HasConversion<int>();
        builder.Property(x => x.AuthorName).IsRequired().HasMaxLength(256);
        builder.Property(x => x.AuthorId).HasMaxLength(450);
        builder.Property(x => x.Content).IsRequired().HasMaxLength(4000);
        builder.HasIndex(x => new { x.ExternalRequestId, x.CreatedAt });
        builder.HasOne(x => x.ExternalRequest).WithMany(x => x.Messages)
            .HasForeignKey(x => x.ExternalRequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExternalPortalInvitationConfiguration : IEntityTypeConfiguration<ExternalPortalInvitation>
{
    public void Configure(EntityTypeBuilder<ExternalPortalInvitation> builder)
    {
        builder.ToTable("ExternalPortalInvitations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(320);
        builder.Property(x => x.TokenHash).IsRequired().HasMaxLength(64);
        builder.HasIndex(x => x.TokenHash).IsUnique();
        builder.HasOne(x => x.ExternalPortal).WithMany(x => x.Invitations)
            .HasForeignKey(x => x.ExternalPortalId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExternalPortalVerificationConfiguration : IEntityTypeConfiguration<ExternalPortalVerification>
{
    public void Configure(EntityTypeBuilder<ExternalPortalVerification> builder)
    {
        builder.ToTable("ExternalPortalVerifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(320);
        builder.Property(x => x.CodeHash).IsRequired().HasMaxLength(64);
        builder.HasIndex(x => new { x.ExternalPortalId, x.Email, x.ExpiresAt });
        builder.HasOne(x => x.ExternalPortal).WithMany(x => x.Verifications)
            .HasForeignKey(x => x.ExternalPortalId).OnDelete(DeleteBehavior.Cascade);
    }
}
