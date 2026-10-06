using Prisma.Workspace.Domain.Interfaces;

namespace Prisma.Workspace.Domain.Entities;

public sealed class AiProviderConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = "OpenAiCompatible";
    public string Provider { get; set; } = "Custom";
    public string? BaseUrl { get; set; }
    public string Model { get; set; } = string.Empty;
    public string Purpose { get; set; } = "Chat";
    public string? EncryptedSecret { get; set; }
    public string? SecretSuffix { get; set; }
    public decimal? InputPrice { get; set; }
    public decimal? OutputPrice { get; set; }
    public bool IsActive { get; set; }
    public bool TestSucceeded { get; set; }
    public bool SupportsTools { get; set; }
    public DateTimeOffset? TestedAt { get; set; }
    public int? LatencyMs { get; set; }
    public string? TestMessage { get; set; }
}

public sealed class AiInstallationSettings
{
    public int Id { get; set; } = 1;
    public long DailyTokenLimit { get; set; }
    public long OrganizationTokenLimit { get; set; }
    public long UserTokenLimit { get; set; }
    public string TimeZone { get; set; } = "America/Sao_Paulo";
}

public sealed class OrganizationAiSettings : IOrganizationOwned
{
    public Guid OrganizationId { get; set; }
    public bool Enabled { get; set; }
    public long? DailyTokenLimit { get; set; }
    public long? UserTokenLimit { get; set; }
}

public sealed class AiConversation : IOrganizationOwned
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = "Nova conversa";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<AiMessage> Messages { get; set; } = new List<AiMessage>();
}

public sealed class AiMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ConversationId { get; set; }
    public AiConversation Conversation { get; set; } = null!;
    public string Role { get; set; } = "user";
    public string Content { get; set; } = string.Empty;
    public string SourcesJson { get; set; } = "[]";
    public string ToolsJson { get; set; } = "[]";
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class AiUsageRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? OrganizationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public Guid ConnectionId { get; set; }
    public string Feature { get; set; } = "Chat";
    public string Model { get; set; } = string.Empty;
    public long InputTokens { get; set; }
    public long OutputTokens { get; set; }
    public long ReservedTokens { get; set; }
    public decimal? EstimatedCost { get; set; }
    public int DurationMs { get; set; }
    public string Outcome { get; set; } = "pending";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
