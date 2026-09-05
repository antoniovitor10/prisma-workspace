using Detran.Kanban.Application.Interfaces;

namespace Detran.Kanban.Infrastructure.Tenancy;

public sealed class CurrentAuditContext : IAuditContext
{
    public string? UserId { get; private set; }
    public string Origin { get; private set; } = "api";
    public string? IpAddress { get; private set; }
    public string? CorrelationId { get; private set; }

    public void Set(string? userId, string origin, string? ipAddress, string? correlationId)
        => (UserId, Origin, IpAddress, CorrelationId) =
            (userId, string.IsNullOrWhiteSpace(origin) ? "api" : origin, ipAddress, correlationId);
}
