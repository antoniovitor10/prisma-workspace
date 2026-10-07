using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Infrastructure.Persistence.Configurations;

public sealed class AiAssistantConfiguration :
    IEntityTypeConfiguration<AiProviderConnection>, IEntityTypeConfiguration<AiInstallationSettings>,
    IEntityTypeConfiguration<OrganizationAiSettings>, IEntityTypeConfiguration<AiConversation>,
    IEntityTypeConfiguration<AiMessage>, IEntityTypeConfiguration<AiUsageRecord>
{
    public void Configure(EntityTypeBuilder<AiProviderConnection> b)
    {
        b.ToTable("AiProviderConnections"); b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(160); b.Property(x => x.Type).HasMaxLength(32);
        b.Property(x => x.Provider).HasMaxLength(32); b.Property(x => x.BaseUrl).HasMaxLength(2048);
        b.Property(x => x.Model).HasMaxLength(200); b.Property(x => x.Purpose).HasMaxLength(32);
        b.Property(x => x.SecretSuffix).HasMaxLength(4); b.Property(x => x.TestMessage).HasMaxLength(500);
        b.Property(x => x.InputPrice).HasPrecision(18, 8); b.Property(x => x.OutputPrice).HasPrecision(18, 8);
        b.HasIndex(x => x.Purpose).IsUnique().HasFilter("[IsActive] = 1");
    }
    public void Configure(EntityTypeBuilder<AiInstallationSettings> b)
    {
        b.ToTable("AiInstallationSettings", t => t.HasCheckConstraint("CK_AiInstallationSettings_Singleton", "[Id] = 1"));
        b.HasKey(x => x.Id); b.Property(x => x.Id).ValueGeneratedNever(); b.Property(x => x.TimeZone).HasMaxLength(100);
    }
    public void Configure(EntityTypeBuilder<OrganizationAiSettings> b)
    {
        b.ToTable("OrganizationAiSettings"); b.HasKey(x => x.OrganizationId);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<AiConversation> b)
    {
        b.ToTable("AiConversations"); b.HasKey(x => x.Id); b.Property(x => x.UserId).HasMaxLength(450);
        b.Property(x => x.Title).HasMaxLength(160); b.HasIndex(x => new { x.OrganizationId, x.UserId, x.CreatedAt });
        b.Property(x => x.SelectedModel).HasMaxLength(200);
        b.HasOne<Organization>().WithMany().HasForeignKey(x => x.OrganizationId).OnDelete(DeleteBehavior.Restrict);
    }
    public void Configure(EntityTypeBuilder<AiMessage> b)
    {
        b.ToTable("AiMessages"); b.HasKey(x => x.Id); b.Property(x => x.Role).HasMaxLength(16);
        b.HasOne(x => x.Conversation).WithMany(x => x.Messages).HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.ConversationId, x.CreatedAt });
    }
    public void Configure(EntityTypeBuilder<AiUsageRecord> b)
    {
        b.ToTable("AiUsageRecords"); b.HasKey(x => x.Id); b.Property(x => x.UserId).HasMaxLength(450);
        b.Property(x => x.Model).HasMaxLength(200); b.Property(x => x.Feature).HasMaxLength(32);
        b.Property(x => x.Outcome).HasMaxLength(32); b.Property(x => x.EstimatedCost).HasPrecision(18, 8);
        b.HasIndex(x => new { x.CreatedAt, x.OrganizationId, x.UserId, x.ConnectionId });
    }
}
