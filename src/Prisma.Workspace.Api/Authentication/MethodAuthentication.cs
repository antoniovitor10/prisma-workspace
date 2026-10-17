using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Api.Authentication;

public static class MethodAuthentication
{
    public const string SchemeName = "PrismaBearer";

    public static AuthenticationBuilder AddMethodBearer(this AuthenticationBuilder builder, bool enabled)
    {
        if (enabled)
            builder.AddScheme<AuthenticationSchemeOptions, MethodTokenAuthenticationHandler>(
                MethodTokenAuthenticationHandler.SchemeName, _ => { });
        return builder.AddPolicyScheme(SchemeName, SchemeName, options =>
            options.ForwardDefaultSelector = context => SelectScheme(context.Request.Headers.Authorization, enabled));
    }

    public static string SelectScheme(IEnumerable<string?> headers, bool enabled)
        => enabled && headers.Any(value => AuthenticationHeaderValue.TryParse(value, out var header)
            && header.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase)
            && header.Parameter?.StartsWith(MethodTokenFormat.Prefix, StringComparison.OrdinalIgnoreCase) == true)
            ? MethodTokenAuthenticationHandler.SchemeName : JwtBearerDefaults.AuthenticationScheme;

    // Inclui credencial malformada/revogada: nenhuma delas pode aproveitar cookie de refresh humano.
    public static bool HasMethodCredential(IEnumerable<string?> headers)
        => headers.Any(value => value?.Contains(MethodTokenFormat.Prefix, StringComparison.OrdinalIgnoreCase) == true);
}
