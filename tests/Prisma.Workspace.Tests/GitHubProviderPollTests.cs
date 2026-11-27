using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Infrastructure.Services.MethodGit;
using Xunit;

namespace Prisma.Workspace.Tests;

/// <summary>T-0007 (HU-0002): sessão do modo consulta no provedor GitHub, com token de leitura e só GET.</summary>
public sealed class GitHubProviderPollTests
{
    private const string Token = "github_pat_FAKE0123456789abcdefghijklmnopqrstuvwxyz";
    private static readonly DateTimeOffset Now = new(2026, 11, 27, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Credential = Guid.NewGuid();

    private static GitRepositoryDescriptor Repository(long id = 777001, string name = "detranse/canais-mobile") => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "github", "github.com", id, name, "main", 0, 0, string.Empty, string.Empty, null, null, Guid.NewGuid(), true,
        Mode: "poll", ReadCredentialId: Credential);

    private static GitHubProvider Provider(FakeGitHub github, FakeTokens? tokens = null)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["MethodGit:GitHubApiBaseUrl"] = "http://fixture:3134/" }).Build();
        return new GitHubProvider(github, new ConfigurationMethodGitSecretStore(config), config, new GitHttpTestEnvironment(), new GitTestClock(Now), tokens ?? new FakeTokens());
    }

    [Fact]
    public async Task CA9_read_token_validates_repository_by_stable_id_and_reads_pull_requests_with_get_only()
    {
        var github = new FakeGitHub();
        var provider = Provider(github);
        var identity = await provider.GetRepositoryIdentityAsync(Repository(0));
        Assert.Equal(777001, identity.Id); Assert.Equal("detranse/canais-mobile", identity.FullName); Assert.Equal("main", identity.DefaultBranch);
        Assert.True((await provider.ValidateInstallationAsync(Repository())).Allowed);
        var read = await provider.ListPullRequestsAsync(Repository(), "all");
        Assert.True(read.Complete); Assert.Equal(5, Assert.Single(read.PullRequests).Number);
        Assert.True(await provider.BranchExistsAsync(Repository(), "feat/pagamento"));
        Assert.False(await provider.BranchExistsAsync(Repository(), "feat/ainda-local"));
        // Só GET, sempre com o token de leitura como Bearer; nada de App, JWT ou instalação.
        Assert.NotEmpty(github.Requests);
        Assert.All(github.Requests, r => { Assert.Equal(HttpMethod.Get, r.Method); Assert.Equal(Token, r.Bearer); });
        Assert.DoesNotContain(github.Requests, r => r.Path.Contains("installation", StringComparison.Ordinal) || r.Path.StartsWith("/app/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CA9_repository_with_another_id_behind_the_same_name_is_refused()
    {
        var provider = Provider(new FakeGitHub());
        var validation = await provider.ValidateInstallationAsync(Repository(id: 999));
        Assert.False(validation.Allowed);
        var error = await Assert.ThrowsAsync<GitHubUnavailableException>(() => provider.ListPullRequestsAsync(Repository(id: 999), "open"));
        Assert.Equal("git_repository_access_mismatch", error.Code);
        Assert.Equal(GitPollStatus.RepositoryMismatch, GitPollStatus.From(error));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, GitPollStatus.TokenInvalid)]
    [InlineData(HttpStatusCode.Forbidden, GitPollStatus.TokenForbidden)]
    [InlineData(HttpStatusCode.NotFound, GitPollStatus.RepositoryNotFound)]
    public async Task CA13_token_failures_become_codes_without_github_text_or_token(HttpStatusCode status, string expected)
    {
        var github = new FakeGitHub { Fail = status };
        var error = await Assert.ThrowsAsync<GitHubUnavailableException>(() => Provider(github).GetRepositoryIdentityAsync(Repository(0)));
        Assert.Equal(expected, GitPollStatus.From(error));
        Assert.DoesNotContain(Token, error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Bad credentials", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CA13_revoked_credential_never_calls_github()
    {
        var github = new FakeGitHub();
        var error = await Assert.ThrowsAsync<GitHubUnavailableException>(() => Provider(github, new FakeTokens { Revoked = true }).GetRepositoryIdentityAsync(Repository(0)));
        Assert.Equal(GitPollStatus.CredentialRevoked, GitPollStatus.From(error));
        Assert.Empty(github.Requests);
    }

    internal sealed class FakeTokens : IGitReadTokenSource
    {
        public bool Revoked { get; init; }
        public Task<string> ResolveAsync(Guid credentialId, Guid projectId, Guid organizationId, CancellationToken ct = default)
            => Revoked || credentialId != Credential ? throw new GitHubUnavailableException("git_credential_revoked") : Task.FromResult(Token);
    }

    internal sealed class FakeGitHub : HttpMessageHandler, IHttpClientFactory
    {
        public HttpStatusCode? Fail { get; init; }
        public List<(HttpMethod Method, string Path, string? Bearer)> Requests { get; } = [];
        public HttpClient CreateClient(string name) => new(this, false);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.PathAndQuery;
            Requests.Add((request.Method, path, request.Headers.Authorization?.Parameter));
            if (Fail is { } status) return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("{\"message\":\"Bad credentials\"}") });
            object? body = path switch
            {
                "/repos/detranse/canais-mobile" => new { id = 777001, full_name = "detranse/canais-mobile", default_branch = "main" },
                "/repos/detranse/canais-mobile/branches/feat/pagamento" => new { name = "feat/pagamento" },
                _ when path.StartsWith("/repos/detranse/canais-mobile/pulls?", StringComparison.Ordinal) => new[] { new
                {
                    number = 5, html_url = "https://github.com/detranse/canais-mobile/pull/5", title = "Pagamento", state = "open", draft = false,
                    created_at = Now, updated_at = Now, closed_at = (DateTimeOffset?)null, merged_at = (DateTimeOffset?)null, merge_commit_sha = (string?)null,
                    head = new { @ref = "feat/pagamento", sha = new string('b', 40), repo = new { id = 777001 } },
                    @base = new { @ref = "main", repo = new { id = 777001 } }, user = new { id = 1, login = "dev" },
                } },
                _ => null,
            };
            return Task.FromResult(body is null ? new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("{}") }
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") });
        }
    }
}
