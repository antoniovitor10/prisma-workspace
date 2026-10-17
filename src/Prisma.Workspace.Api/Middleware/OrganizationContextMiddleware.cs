using System.Security.Claims;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Api.OpenApi;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Api.Middleware;

/// <summary>
/// Resolve a organização ativa e recusa a requisição antes que qualquer
/// controller de negócio seja executado fora de um tenant válido.
/// </summary>
public class OrganizationContextMiddleware
{
    public const string HeaderName = "X-Organization-Id";
    private readonly RequestDelegate _next;

    public OrganizationContextMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext httpContext,
        AppDbContext dbContext,
        CurrentOrganizationContext organizationContext,
        IMethodAccessContext? method = null)
    {
        if (method?.Current is { AuthenticationKind: MethodAuthenticationKind.MethodToken or MethodAuthenticationKind.IntegrationWorker } actor)
        {
            var requested = httpContext.Request.Headers[HeaderName];
            if (actor.OrganizationId is not { } tenant || requested.Count > 1
                || requested.Count == 1 && (!Guid.TryParse(requested[0], out var headerTenant) || headerTenant != tenant))
            {
                await WriteAsync(httpContext, ApiErrors.Problem(403, "method_tenant_scope", "Acesso negado",
                    "A organização não corresponde à credencial do método. Pare e avise.", httpContext.TraceIdentifier));
                return;
            }
            organizationContext.Set(tenant);
            await _next(httpContext);
            return;
        }
        if (!RequiresOrganization(httpContext))
        {
            await _next(httpContext);
            return;
        }

        if (!Guid.TryParse(httpContext.Request.Headers[HeaderName].FirstOrDefault(), out var organizationId))
        {
            await WriteAsync(httpContext, ApiErrors.Validation(new Dictionary<string, string[]>
            {
                [HeaderName] = [$"Informe uma organização válida no cabeçalho {HeaderName}."]
            }, httpContext.TraceIdentifier));
            return;
        }

        var userId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var hasAccess = userId is not null && await dbContext.OrganizationMembers
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(member => member.OrganizationId == organizationId
                && member.UserId == userId
                && member.IsActive
                && member.Organization.IsActive,
                httpContext.RequestAborted);

        if (!hasAccess)
        {
            await WriteAsync(httpContext, ApiErrors.Problem(403, "forbidden", "Acesso negado",
                "A organização não existe ou não está disponível para este usuário.",
                httpContext.TraceIdentifier));
            return;
        }

        organizationContext.Set(organizationId);
        await _next(httpContext);
    }

    private static bool RequiresOrganization(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated != true)
            return false;
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.StartsWith("/api/public", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.StartsWith("/api/admin/ai", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.Equals("/api/organizations", StringComparison.OrdinalIgnoreCase))
            return false;
        if (path.Equals("/api/organizations/invitations/accept", StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    private static async Task WriteAsync(HttpContext context, Microsoft.AspNetCore.Mvc.ProblemDetails problem)
    {
        context.Response.StatusCode = problem.Status ?? 500;
        context.Response.ContentType = "application/problem+json; charset=utf-8";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
