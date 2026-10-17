using System.Security.Claims;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Api.Middleware;

/// <summary>Executado após autenticação e tenant; cabeçalhos/body nunca estabelecem papel ou sessão UI.</summary>
public sealed class MethodIdentityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext http, IMethodAccessContext access, IMethodContextInitializer initializer,
        IOrganizationContext organization, IUserDirectory users, IConfiguration configuration, IAuditContext audit)
    {
        if (access.Current is { AuthenticationKind: MethodAuthenticationKind.MethodToken } && IsOrganizationControlPath(http.Request.Path))
        {
            await MethodBoundaryMiddleware.RejectAsync(http, 403, "method_project_scope",
                "A credencial do método está limitada ao seu projeto e não permite esta operação de organização. Pare e avise.");
            return;
        }
        if (configuration.GetValue<bool>("MethodGit:Enabled") && access.Current is null
            && http.User.Identity?.IsAuthenticated == true && organization.OrganizationId is not null
            && !http.Request.Path.StartsWithSegments("/api/public", StringComparison.OrdinalIgnoreCase))
        {
            var userId = http.User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!string.IsNullOrWhiteSpace(userId))
            {
                var names = await users.GetDisplayNamesAsync([userId], http.RequestAborted);
                initializer.Initialize(new MethodActorIdentity(MethodAuthenticationKind.UiSession, MethodRole.Human,
                    null, userId, names.GetValueOrDefault(userId) ?? http.User.Identity.Name ?? userId,
                    organization.OrganizationId, null, null, null, Enum.GetValues<PlatformPermission>().ToHashSet(),
                    MethodChannels.Ui, audit.CorrelationId ?? http.TraceIdentifier));
            }
        }
        await next(http);
    }

    private static bool IsOrganizationControlPath(PathString path)
        => new[] { "/api/auth", "/api/setup", "/api/admin", "/api/organizations", "/api/users", "/api/me",
            "/api/notifications", "/api/teams", "/api/tags", "/api/task-types", "/api/clients", "/api/audit", "/api/public" }
            .Any(prefix => path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));
}
