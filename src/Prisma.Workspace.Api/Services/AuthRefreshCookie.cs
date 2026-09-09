using Microsoft.AspNetCore.Http;

namespace Prisma.Workspace.Api.Services;

/// <summary>
/// Nomes do cookie de refresh: emite só o nome Prisma; ainda lê os legados Detran
/// para não derrubar sessões já abertas durante a transição.
/// </summary>
internal static class AuthRefreshCookie
{
    public const string LegacyDevelopment = "detran_refresh";
    public const string LegacyHost = "__Host-detran_refresh";
    public const string CurrentDevelopment = "prisma_refresh";
    public const string CurrentHost = "__Host-prisma_refresh";

    public static string EmitName(bool isDevelopment)
        => isDevelopment ? CurrentDevelopment : CurrentHost;

    public static string LegacyName(bool isDevelopment)
        => isDevelopment ? LegacyDevelopment : LegacyHost;

    public static IEnumerable<string> ReadableNames(bool isDevelopment)
    {
        yield return EmitName(isDevelopment);
        yield return LegacyName(isDevelopment);
    }

    public static bool TryRead(IRequestCookieCollection cookies, bool isDevelopment, out string token)
    {
        foreach (var name in ReadableNames(isDevelopment))
        {
            if (cookies.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                token = value;
                return true;
            }
        }

        token = string.Empty;
        return false;
    }
}
