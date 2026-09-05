using Detran.Kanban.Application.Features.Notifications;
using Detran.Kanban.Application.Features.Search;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Infrastructure.Persistence;
using Detran.Kanban.Infrastructure.Repositories;
using Detran.Kanban.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Detran.Kanban.Tests;

public class PlatformCrossCuttingTests
{
    [Fact]
    public async Task Notification_publisher_respects_defaults_preferences_and_deduplication()
    {
        var organizationId = Guid.NewGuid();
        var userId = "user-1";
        var organization = new TestOrganizationContext(organizationId);
        await using var db = CreateContext(organization);
        db.Organizations.Add(new Organization
        {
            Id = organizationId, Name = "Órgão", Slug = "orgao", Locale = "pt-BR",
            TimeZone = "America/Sao_Paulo", IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
        db.OrganizationMembers.Add(new OrganizationMember
        {
            OrganizationId = organizationId, UserId = userId,
            Role = OrganizationRole.TeamMember, IsActive = true, JoinedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var repository = new NotificationRepository(db, organization);
        var envelope = new NotificationEnvelope(organizationId, userId, NotificationType.TaskAssigned,
            "Tarefa atribuída", "Você recebeu uma tarefa.", DeduplicationKey: "assignment:1");

        Assert.True(await repository.PublishAsync(envelope));
        Assert.False(await repository.PublishAsync(envelope));
        var notification = await db.Notifications.SingleAsync();
        Assert.True(notification.IsInAppVisible);
        Assert.Equal(NotificationEmailStatus.Pending, notification.EmailStatus);

        repository.AddPreference(new NotificationPreference
        {
            Id = Guid.NewGuid(), OrganizationId = organizationId, UserId = userId,
            Type = NotificationType.Comment, InAppEnabled = false, EmailEnabled = false,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await repository.SaveAsync();
        Assert.False(await repository.PublishAsync(new NotificationEnvelope(
            organizationId, userId, NotificationType.Comment, "Comentário", "Nova mensagem.")));
    }

    [Fact]
    public async Task Refresh_token_is_hashed_rotated_and_reuse_revokes_family()
    {
        await using var db = CreateContext();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["Jwt:RefreshTokenDays"] = "7",
                ["Jwt:RefreshReuseGraceSeconds"] = "0"
            }).Build();
        var service = new RefreshTokenService(db, configuration);

        var first = await service.IssueAsync("user-1", "127.0.0.1");
        var stored = await db.RefreshTokens.SingleAsync();
        Assert.NotEqual(first.Token, stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);

        var second = await service.RotateAsync(first.Token, "127.0.0.1");
        Assert.NotNull(second);
        Assert.NotEqual(first.Token, second!.Token);
        Assert.Null(await service.RotateAsync(first.Token, "127.0.0.1"));
        Assert.Null(await service.RotateAsync(second.Token, "127.0.0.1"));
        Assert.All(await db.RefreshTokens.ToListAsync(), token => Assert.NotNull(token.RevokedAt));
    }

    [Fact]
    public async Task DbContext_writes_immutable_audit_with_actor_ip_and_redaction()
    {
        var organizationId = Guid.NewGuid();
        var organization = new TestOrganizationContext(organizationId);
        var audit = new TestAuditContext("actor-1", "web", "127.0.0.1", "trace-1");
        await using var db = CreateContext(organization, audit);
        var request = new ExternalRequest
        {
            Id = Guid.NewGuid(), ExternalPortalId = Guid.NewGuid(), WorkItemId = Guid.NewGuid(),
            Protocol = "2026-000001", AccessKeyHash = "DO-NOT-STORE-IN-AUDIT",
            RequesterEmail = "pessoa@example.com", CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        db.ExternalRequests.Add(request);

        await db.SaveChangesAsync();
        var entry = await db.AuditLogs.SingleAsync(x => x.EntityType == nameof(ExternalRequest));
        Assert.Equal("actor-1", entry.UserId);
        Assert.Equal("web", entry.Origin);
        Assert.Equal("127.0.0.1", entry.IpAddress);
        Assert.Equal(request.Id.ToString(), entry.EntityId);
        Assert.Contains("[REDACTED]", entry.NewValuesJson);
        Assert.DoesNotContain("DO-NOT-STORE-IN-AUDIT", entry.NewValuesJson);
        Assert.DoesNotContain("pessoa@example.com", entry.NewValuesJson);
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("a", false)]
    [InlineData("placas", true)]
    public void Global_search_requires_bounded_meaningful_query(string query, bool valid)
    {
        var result = new GlobalSearchQueryValidator().Validate(new GlobalSearchQuery(query, "user-1", 6));
        Assert.Equal(valid, result.IsValid);
    }

    private static AppDbContext CreateContext(
        IOrganizationContext? organization = null,
        IAuditContext? audit = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AppDbContext(options, organization, audit);
    }

    private sealed class TestOrganizationContext(Guid organizationId) : IOrganizationContext
    {
        public Guid? OrganizationId { get; } = organizationId;
    }

    private sealed class TestAuditContext(
        string? userId, string origin, string? ipAddress, string? correlationId) : IAuditContext
    {
        public string? UserId { get; } = userId;
        public string Origin { get; } = origin;
        public string? IpAddress { get; } = ipAddress;
        public string? CorrelationId { get; } = correlationId;
    }
}
