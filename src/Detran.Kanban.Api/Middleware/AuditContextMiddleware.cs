using System.Security.Claims;
using Detran.Kanban.Infrastructure.Tenancy;

namespace Detran.Kanban.Api.Middleware;

public sealed class AuditContextMiddleware
{
    private static readonly HashSet<string> AllowedOrigins = new(StringComparer.OrdinalIgnoreCase)
        { "api", "web", "portal", "integration", "import", "system" };
    private readonly RequestDelegate _next;
    public AuditContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, CurrentAuditContext auditContext)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(correlationId)) correlationId = context.TraceIdentifier;
        correlationId = correlationId.Length > 100 ? correlationId[..100] : correlationId;
        context.Response.Headers["X-Correlation-ID"] = correlationId;

        var requestedOrigin = context.Request.Headers["X-Request-Origin"].FirstOrDefault();
        var origin = AllowedOrigins.Contains(requestedOrigin ?? string.Empty)
            ? requestedOrigin!
            : context.Request.Path.StartsWithSegments("/api/public") ? "portal"
            : context.Request.Headers.ContainsKey("Origin") ? "web" : "api";
        auditContext.Set(
            context.User.FindFirstValue(ClaimTypes.NameIdentifier),
            origin,
            context.Connection.RemoteIpAddress?.ToString(),
            correlationId);
        await _next(context);
    }
}
