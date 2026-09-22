using System.Collections;
using Microsoft.AspNetCore.Http;
using Prisma.Workspace.Api.Services;

namespace Prisma.Workspace.Tests;

public sealed class AuthRefreshCookieTests
{
    [Fact]
    public void EmitName_UsesPrismaNames_NotDetran()
    {
        Assert.Equal("prisma_refresh", AuthRefreshCookie.EmitName(isDevelopment: true));
        Assert.Equal("__Host-prisma_refresh", AuthRefreshCookie.EmitName(isDevelopment: false));
        Assert.DoesNotContain("detran", AuthRefreshCookie.EmitName(true), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("detran", AuthRefreshCookie.EmitName(false), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryRead_AcceptsLegacyDevelopmentCookie_SoOldSessionSurvives()
    {
        var cookies = new FakeCookies();
        cookies.Add(AuthRefreshCookie.LegacyDevelopment, "legacy-token-value");

        var found = AuthRefreshCookie.TryRead(cookies, isDevelopment: true, out var token);

        Assert.True(found);
        Assert.Equal("legacy-token-value", token);
    }

    [Fact]
    public void TryRead_PrefersCurrentCookie_WhenBothPresent()
    {
        var cookies = new FakeCookies();
        cookies.Add(AuthRefreshCookie.LegacyDevelopment, "legacy");
        cookies.Add(AuthRefreshCookie.CurrentDevelopment, "current");

        var found = AuthRefreshCookie.TryRead(cookies, isDevelopment: true, out var token);

        Assert.True(found);
        Assert.Equal("current", token);
    }

    [Fact]
    public void TryRead_AcceptsLegacyHostCookie_InNonDevelopment()
    {
        var cookies = new FakeCookies();
        cookies.Add(AuthRefreshCookie.LegacyHost, "host-legacy-token");

        var found = AuthRefreshCookie.TryRead(cookies, isDevelopment: false, out var token);

        Assert.True(found);
        Assert.Equal("host-legacy-token", token);
    }

    private sealed class FakeCookies : IRequestCookieCollection
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

        public string this[string key] => _values.TryGetValue(key, out var value) ? value : string.Empty;
        public int Count => _values.Count;
        public ICollection<string> Keys => _values.Keys;
        public bool ContainsKey(string key) => _values.ContainsKey(key);
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _values.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public bool TryGetValue(string key, out string value) => _values.TryGetValue(key, out value!);
        public void Add(string key, string value) => _values[key] = value;
    }
}
