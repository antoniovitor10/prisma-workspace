using System.Security.Cryptography;
using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Webhooks;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Infrastructure.Services.MethodGit;
using Xunit;

namespace Prisma.Workspace.Tests;

public class GitWebhookTests
{
    [Fact]
    public void Official_GitHub_HMAC_vector_and_tampering()
    {
        const string signature = "sha256=757107ea0eb2509fc211221cce984b8a37570b6d7586c22c46f4379c8b043e17";
        Assert.True(GitHubCryptography.VerifySignature("Hello, World!"u8, signature, "It's a Secret to Everybody"));
        Assert.False(GitHubCryptography.VerifySignature("Hello, World?"u8, signature, "It's a Secret to Everybody"));
        Assert.False(GitHubCryptography.VerifySignature("Hello, World!"u8, signature, "segredo diferente"));
    }
    [Theory]
    [InlineData("")][InlineData("sha1=0000")][InlineData("SHA256=757107ea0eb2509fc211221cce984b8a37570b6d7586c22c46f4379c8b043e17")]
    [InlineData("sha256=zzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzzz")]
    public void Malformed_signatures_are_rejected(string signature) => Assert.False(GitHubCryptography.VerifySignature("body"u8, signature, "secret"));
    [Fact]
    public void Raw_UTF8_bytes_are_not_reserialized()
    {
        var raw = Encoding.UTF8.GetBytes("{\r\n \"title\":\"ação\" }");
        var signature = Sign(raw, "segredo");
        Assert.True(GitHubCryptography.VerifySignature(raw, signature, "segredo"));
        Assert.False(GitHubCryptography.VerifySignature("{\"title\":\"ação\"}"u8, signature, "segredo"));
    }
    [Fact]
    public void Normalization_drops_raw_description_and_allows_repository_webhook_without_installation()
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(new { repository = new { id = 900001 }, zen = "body must not persist" });
        var normalized = GitEventNormalizer.Normalize("ping", body);
        Assert.Null(normalized.InstallationId); Assert.Equal(900001, normalized.RepositoryId);
        Assert.DoesNotContain("body must not persist", JsonSerializer.Serialize(normalized));
    }
    [Fact]
    public async Task Invalid_signature_never_initializes_tenant_or_accepts_inbox()
    {
        var store = new GitTestStore(); var initializer = new TestWorkerInitializer();
        var ingress = Ingress(store, initializer);
        var body = Ping(store.Repository.RepositoryId);
        var error = await Assert.ThrowsAsync<MethodOperationException>(() => ingress.AcceptAsync(Input(store, body, Sign(body, "wrong"))));
        Assert.Equal(401, error.StatusCode); Assert.Equal(0, store.Accepted); Assert.Equal(0, initializer.Calls);
    }
    [Theory]
    [InlineData(true, true)][InlineData(false, false)]
    public async Task Previous_secret_only_works_inside_rotation_window(bool active, bool accepted)
    {
        var store = new GitTestStore(); var initializer = new TestWorkerInitializer();
        store.Repository = store.Repository with { PreviousWebhookSecretRef = "previous-secret", PreviousSecretValidUntilUtc = store.Now.AddSeconds(active ? 1 : -1) };
        var body = Ping(store.Repository.RepositoryId); var ingress = Ingress(store, initializer);
        if (accepted) { await ingress.AcceptAsync(Input(store, body, Sign(body, "old"))); Assert.Equal(1, store.Accepted); Assert.Equal(1, initializer.Calls); }
        else { Assert.Equal(401, (await Assert.ThrowsAsync<MethodOperationException>(() => ingress.AcceptAsync(Input(store, body, Sign(body, "old"))))).StatusCode); Assert.Equal(0, store.Accepted); }
    }
    [Fact]
    public async Task Valid_signature_for_wrong_repository_is_forbidden_without_tenant_or_inbox()
    {
        var store = new GitTestStore(); var initializer = new TestWorkerInitializer(); var body = Ping(999999);
        var error = await Assert.ThrowsAsync<MethodOperationException>(() => Ingress(store, initializer).AcceptAsync(Input(store, body, Sign(body, "current"))));
        Assert.Equal(403, error.StatusCode); Assert.Equal(0, store.Accepted); Assert.Equal(0, initializer.Calls);
    }
    [Fact]
    public async Task Oversize_body_is_413_without_effect()
    {
        var store = new GitTestStore(); var initializer = new TestWorkerInitializer();
        var body = new byte[GitWebhookIngress.MaximumBodyBytes + 1];
        var error = await Assert.ThrowsAsync<MethodOperationException>(() => Ingress(store, initializer).AcceptAsync(Input(store, body, "")));
        Assert.Equal(413, error.StatusCode); Assert.Equal(0, store.Accepted);
    }
    [Fact]
    public async Task Invalid_mapping_preserves_authenticated_delivery_for_later_processing()
    {
        var store = new GitTestStore(); store.Project = store.Project with { Enabled = false };
        var initializer = new TestWorkerInitializer(); var body = Ping(store.Repository.RepositoryId);
        var result = await Ingress(store, initializer).AcceptAsync(Input(store, body, Sign(body, "current")));
        Assert.False(result.Conflict); Assert.Equal(1, store.Accepted); Assert.Equal(1, initializer.Calls);
    }
    [Fact]
    public async Task App_JWT_is_RSA_signed_and_installation_token_is_limited_and_opaque()
    {
        using var rsa = RSA.Create(2048);
        var store = new GitTestStore();
        var factory = new GitHttpTestFactory(rsa, store.Now);
        var key = rsa.ExportRSAPrivateKeyPem().Replace("\n", "\\n", StringComparison.Ordinal);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["MethodGit:GitHubApiBaseUrl"] = "http://fixture:3134/", ["MethodGit:Secrets:app-key"] = key }).Build();
        var provider = new GitHubProvider(factory, new ConfigurationMethodGitSecretStore(config), config, new GitHttpTestEnvironment(), new GitTestClock(store.Now));
        Assert.True((await provider.ValidateInstallationAsync(store.Repository)).Allowed);
        Assert.Equal(1, (await provider.GetPullRequestAsync(store.Repository, 1)).Number);
        Assert.True((await provider.ListPullRequestsAsync(store.Repository, "all")).Complete);
        Assert.Equal("changes_requested", Assert.Single(await provider.GetReviewsAsync(store.Repository, 1)).State);
        Assert.Equal(1, factory.TokenRequests); Assert.Equal(1, factory.RepositoryRequests);
        Assert.All(factory.Paths, path => Assert.DoesNotContain("/contents", path));
    }
    [Fact]
    public async Task Secret_reference_cannot_escape_external_namespace()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:DefaultConnection"] = "not-a-git-secret", ["MethodGit:Secrets:hook"] = "dedicated" }).Build();
        var secrets = new ConfigurationMethodGitSecretStore(config);
        Assert.Equal("dedicated", await secrets.ResolveAsync("hook"));
        Assert.Equal(400, (await Assert.ThrowsAsync<MethodOperationException>(() => secrets.ResolveAsync("ConnectionStrings:DefaultConnection"))).StatusCode);
    }
    [Fact]
    public void Git_sender_is_preserved_as_metadata_without_becoming_authority()
    {
        var raw = JsonSerializer.SerializeToUtf8Bytes(new { repository = new { id = 900001 }, sender = new { id = 42, login = "observado" } });
        var value = GitEventNormalizer.Normalize("ping", raw);
        Assert.Equal("42", value.ObservedGitActorId); Assert.Equal("observado", value.ObservedGitActorName);
    }
    private static GitWebhookIngress Ingress(GitTestStore store, TestWorkerInitializer initializer) => new(store, new TestGitProvider(), new TestGitSecrets(), initializer,
        new GitWebhookIngressLimiter(), NullLogger<GitWebhookIngress>.Instance, new GitTestClock(store.Now));
    private static GitWebhookInput Input(GitTestStore store, byte[] body, string signature) => new(store.Repository.Id, Guid.NewGuid().ToString("D"), "ping", signature, body, "test-correlation");
    private static byte[] Ping(long repository) => JsonSerializer.SerializeToUtf8Bytes(new { repository = new { id = repository } });
    internal static string Sign(byte[] body, string secret) => "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body)).ToLowerInvariant();
}

internal sealed class GitTestClock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
internal sealed class TestWorkerInitializer : IMethodWorkerContextInitializer
{
    public int Calls { get; private set; }
    public Task InitializeAsync(Guid integration, Guid? delivery, string correlation, CancellationToken ct = default) { Calls++; return Task.CompletedTask; }
}
internal sealed class TestGitSecrets : IMethodGitSecretStore
{
    public Task<string> ResolveAsync(string reference, CancellationToken ct = default) => Task.FromResult(reference == "previous-secret" ? "old" : "current");
}
internal class TestGitProvider : IGitProvider
{
    public string Provider => "github";
    public bool VerifyWebhookSignature(ReadOnlySpan<byte> body, string signature, string secret) => GitHubCryptography.VerifySignature(body, signature, secret);
    public NormalizedGitEvent NormalizeWebhook(string name, ReadOnlySpan<byte> body) => GitEventNormalizer.Normalize(name, body);
    public virtual Task<GitInstallationValidation> ValidateInstallationAsync(GitRepositoryDescriptor repository, CancellationToken ct = default) => Task.FromResult(new GitInstallationValidation(true, null, null));
    public virtual Task<GitPullRequestSnapshot> GetPullRequestAsync(GitRepositoryDescriptor repository, int number, CancellationToken ct = default) => throw new NotSupportedException();
    public virtual Task<IReadOnlyList<GitReviewSnapshot>> GetReviewsAsync(GitRepositoryDescriptor repository, int number, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<GitReviewSnapshot>>([]);
    public virtual Task<GitPullRequestReadSet> ListPullRequestsAsync(GitRepositoryDescriptor repository, string state, CancellationToken ct = default) => Task.FromResult(new GitPullRequestReadSet([], true));
}

internal sealed class GitHttpTestEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Test";
    public string ApplicationName { get; set; } = "Prisma-Git-Test";
    public string ContentRootPath { get; set; } = "/tmp";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
internal sealed class GitHttpTestFactory(RSA rsa, DateTimeOffset now) : HttpMessageHandler, IHttpClientFactory
{
    private readonly string _token = "ghs_" + new string('x', 1024);
    public int TokenRequests { get; private set; }
    public int RepositoryRequests { get; private set; }
    public List<string> Paths { get; } = [];
    public HttpClient CreateClient(string name) { Assert.Equal(GitHubProvider.HttpClientName, name); return new HttpClient(this, false); }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var path = request.RequestUri!.PathAndQuery; Paths.Add(path);
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        object body;
        if (path == "/app/installations/1/access_tokens")
        {
            TokenRequests++; var jwt = request.Headers.Authorization!.Parameter!.Split('.'); Assert.Equal(3, jwt.Length);
            using var header = JsonDocument.Parse(Decode(jwt[0])); Assert.Equal("RS256", header.RootElement.GetProperty("alg").GetString());
            Assert.True(rsa.VerifyData(Encoding.ASCII.GetBytes(jwt[0] + "." + jwt[1]), Decode(jwt[2]), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            using var claims = JsonDocument.Parse(Decode(jwt[1])); Assert.Equal("424242", claims.RootElement.GetProperty("iss").GetString());
            Assert.InRange(claims.RootElement.GetProperty("exp").GetInt64() - now.ToUnixTimeSeconds(), 1, 600);
            using var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            Assert.Equal(900001, Assert.Single(payload.RootElement.GetProperty("repository_ids").EnumerateArray()).GetInt64());
            var granted = payload.RootElement.GetProperty("permissions").EnumerateObject().ToArray();
            Assert.Equal(2, granted.Length); Assert.All(granted, permission => Assert.Equal("read", permission.Value.GetString()));
            Assert.Contains(granted, permission => permission.Name == "pull_requests"); Assert.Contains(granted, permission => permission.Name == "metadata");
            body = new { token = _token, expires_at = now.AddHours(1), permissions = new { pull_requests = "read", metadata = "read" } };
        }
        else
        {
            Assert.Equal(_token, request.Headers.Authorization?.Parameter);
            if (path == "/installation/repositories?per_page=100&page=1")
            { RepositoryRequests++; body = new { total_count = 1, repositories = new[] { new { id = 900001, full_name = "qa/metodo", default_branch = "main" } } }; }
            else if (path.Contains("/reviews", StringComparison.Ordinal))
                body = new[] { new { id = 10, state = "CHANGES_REQUESTED", commit_id = new string('b', 40), submitted_at = now, user = new { id = 42, login = "reviewer" } } };
            else
            {
                var pr = new { number = 1, html_url = "https://github.com/qa/metodo/pull/1", title = "Teste", state = "open", draft = false,
                    created_at = now, updated_at = now, closed_at = (DateTimeOffset?)null, merged = false, merged_at = (DateTimeOffset?)null, merge_commit_sha = (string?)null,
                    head = new { @ref = "task/T-0001-branch", sha = new string('b', 40) }, @base = new { @ref = "main", repo = new { id = 900001 } } };
                body = path.Contains("?state=", StringComparison.Ordinal) ? (object)new[] { pr } : pr;
            }
        }
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
    }
    private static byte[] Decode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight((base64.Length + 3) / 4 * 4, '='));
    }
}
