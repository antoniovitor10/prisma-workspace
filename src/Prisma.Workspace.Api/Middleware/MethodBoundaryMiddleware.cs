using Prisma.Workspace.Api.Authentication;
using Prisma.Workspace.Api.OpenApi;

namespace Prisma.Workspace.Api.Middleware;

/// <summary>Fronteira anterior à autenticação, inclusive nas rotas AllowAnonymous de login/refresh.</summary>
public sealed class MethodBoundaryMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IConfiguration configuration)
    {
        if (context.Request.Path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase)
            && MethodAuthentication.HasMethodCredential(context.Request.Headers.Authorization))
        {
            await RejectAsync(context, 403, "method_cannot_mint_session",
                "Uma credencial do método não pode emitir ou renovar uma sessão humana. Pare e avise.");
            return;
        }
        if (IsMethodPath(context.Request.Path))
        {
            context.Response.Headers.CacheControl = "no-store";
            if (!configuration.GetValue<bool>("MethodGit:Enabled"))
            {
                await RejectAsync(context, 404, "method_unavailable", "O método não está habilitado nesta instalação.");
                return;
            }
        }
        await next(context);
    }

    public static bool IsMethodPath(PathString path)
    {
        var segments = (path.Value ?? string.Empty).Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 && segments[0].Equals("api", StringComparison.OrdinalIgnoreCase)
            && (segments[1].Equals("method", StringComparison.OrdinalIgnoreCase)
                || segments.Length >= 4 && segments[1].Equals("projects", StringComparison.OrdinalIgnoreCase)
                    && segments[3].Equals("method", StringComparison.OrdinalIgnoreCase)
                || segments.Length >= 3 && segments[1].Equals("integrations", StringComparison.OrdinalIgnoreCase)
                    && segments[2].Equals("git", StringComparison.OrdinalIgnoreCase));
    }

    internal static async Task RejectAsync(HttpContext context, int status, string code, string message)
    {
        context.Response.StatusCode = status;
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.WriteAsJsonAsync(ApiErrors.Problem(status, code,
            status == 404 ? "Recurso indisponível" : "Acesso negado", message, context.TraceIdentifier), context.RequestAborted);
    }
}
