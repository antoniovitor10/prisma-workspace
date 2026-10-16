using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Prisma.Workspace.Api.OpenApi;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Application.Interfaces;

namespace Prisma.Workspace.Api.Authentication;

public sealed class MethodTokenAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IMethodTokenService tokens, IMethodContextInitializer initializer)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "MethodToken";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.Authorization.Count == 0) return AuthenticateResult.NoResult();
        if (Request.Headers.Authorization.Count != 1
            || !AuthenticationHeaderValue.TryParse(Request.Headers.Authorization.ToString(), out var header)
            || !string.Equals(header.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) || header.Parameter is null)
            return AuthenticateResult.Fail("Credencial do método inválida.");
        var actor = await tokens.AuthenticateAsync(header.Parameter, Context.TraceIdentifier, Context.RequestAborted);
        if (actor is null) return AuthenticateResult.Fail("Token do método inválido, expirado ou revogado.");
        initializer.Initialize(actor);
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, actor.ActorId), new Claim(ClaimTypes.Name, actor.DisplayName),
            new Claim("method_principal", actor.PrincipalId!.Value.ToString("D")),
            new Claim("method_project", actor.ProjectId!.Value.ToString("D")),
            new Claim("method_organization", actor.OrganizationId!.Value.ToString("D"))
        };
        // Sem papel administrativo Identity, e-mail humano ou interpretação de role enviado pelo cliente.
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName)), SchemeName));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 401;
        Response.Headers.WWWAuthenticate = "Bearer";
        return Response.WriteAsJsonAsync(ApiErrors.Problem(401, "method_token_invalid", "Autenticação necessária",
            "Token do método inválido, expirado ou revogado. Pare e avise.", Context.TraceIdentifier), Context.RequestAborted);
    }

    protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = 403;
        return Response.WriteAsJsonAsync(ApiErrors.Problem(403, "method_forbidden", "Acesso negado",
            "A credencial não permite esta operação. Pare e avise.", Context.TraceIdentifier), Context.RequestAborted);
    }
}
