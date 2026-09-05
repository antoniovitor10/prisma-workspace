using Detran.Kanban.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Detran.Kanban.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.Type).HasConversion<int>();
        builder.Property(x => x.Title).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Message).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.Link).HasMaxLength(500);
        builder.Property(x => x.DeduplicationKey).HasMaxLength(300);
        builder.Property(x => x.EmailStatus).HasConversion<int>();
        builder.Property(x => x.EmailFailureReason).HasMaxLength(500);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.OrganizationId, x.UserId, x.IsInAppVisible, x.IsRead, x.CreatedAt });
        builder.HasIndex(x => new { x.EmailStatus, x.CreatedAt });
        builder.HasIndex(x => new { x.OrganizationId, x.UserId, x.DeduplicationKey })
            .IsUnique()
            .HasFilter("[DeduplicationKey] IS NOT NULL");
    }
}

public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.UserId).IsRequired().HasMaxLength(450);
        builder.Property(x => x.Type).HasConversion<int>();
        builder.HasIndex(x => new { x.OrganizationId, x.UserId, x.Type }).IsUnique();
    }
}
