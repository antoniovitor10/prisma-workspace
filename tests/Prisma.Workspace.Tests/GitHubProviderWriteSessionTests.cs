using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Infrastructure.Services.MethodGit;
using Xunit;

namespace Prisma.Workspace.Tests;

/// <summary>HU-0007 · T-0027 (D3, CA-10, CA-11): sessão de escrita do GitHubProvider, classificação das recusas e redação do token.</summary>
public sealed class GitHubProviderWriteSessionTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-27T12:00:00Z");
    private const string Branch = "task/T-0027-abrir-pr";
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly WriteFakeGitHub _github = new();
    private readonly GitHubProvider _provider;
    private readonly GitRepositoryDescriptor _repository = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "github", "github.com", 900001,
        "qa/metodo", "main", 424242, 1, "app-key", "secret", null, null, Guid.NewGuid(), true);

    public GitHubProviderWriteSessionTests()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["MethodGit:GitHubApiBaseUrl"] = "http://fixture:3134/", ["MethodGit:Secrets:app-key"] = _rsa.ExportRSAPrivateKeyPem() }).Build();
        _provider = new GitHubProvider(_github, new ConfigurationMethodGitSecretStore(config), config, new GitHttpTestEnvironment(), new GitTestClock(Now));
    }
    public void Dispose() => _rsa.Dispose();

    private Task<GitPullRequestSnapshot> Create(bool draft = false)
        => _provider.CreatePullRequestAsync(_repository, new(Branch, "main", "[T-0027] Corrige X", "Corpo\n\nTask no Prisma: https://x/c", draft));

    [Fact]
    public async Task CA11_write_session_asks_exactly_pull_requests_write_and_metadata_read_for_one_repository_without_cache()
    {
        var pr = await Create(draft: true);
        Assert.Equal(7, pr.Number); Assert.True(pr.IsDraft); Assert.Equal(Branch, pr.HeadBranch);
        var write = Assert.Single(_github.TokenRequests, x => x.Contains("\"write\""));
        using (var json = JsonDocument.Parse(write))
        {
            Assert.Equal(900001, Assert.Single(json.RootElement.GetProperty("repository_ids").EnumerateArray()).GetInt64());
            var permissions = json.RootElement.GetProperty("permissions").EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString());
            Assert.Equal(new Dictionary<string, string?> { ["pull_requests"] = "write", ["metadata"] = "read" }, permissions);
        }
        // A leitura continua pedindo só leitura; a criação usa o token de escrita.
        var read = Assert.Single(_github.TokenRequests, x => !x.Contains("\"write\""));
        Assert.Contains("\"pull_requests\":\"read\"", read);
        Assert.Equal(WriteFakeGitHub.WriteToken, _github.CreateAuthorization);
        using (var body = JsonDocument.Parse(_github.CreateBody!))
        {
            Assert.Equal(Branch, body.RootElement.GetProperty("head").GetString());
            Assert.Equal("main", body.RootElement.GetProperty("base").GetString());
            Assert.True(body.RootElement.GetProperty("draft").GetBoolean());
            Assert.Equal("[T-0027] Corrige X", body.RootElement.GetProperty("title").GetString());
        }
        // Sem cache: o segundo PR pede outro token de escrita; a sessão de leitura é reaproveitada.
        await Create();
        Assert.Equal(2, _github.TokenRequests.Count(x => x.Contains("\"write\"")));
        Assert.Single(_github.TokenRequests, x => !x.Contains("\"write\""));
    }

    [Theory]
    [InlineData("{\"pull_requests\":\"write\",\"metadata\":\"read\",\"contents\":\"read\"}")]
    [InlineData("{\"pull_requests\":\"read\",\"metadata\":\"read\"}")]
    [InlineData("{\"pull_requests\":\"write\"}")]
    public async Task CA11_write_token_with_more_or_less_permissions_is_refused(string granted)
    {
        _github.WritePermissions = granted;
        var error = await Assert.ThrowsAsync<GitPullRequestRejectedException>(() => Create());
        Assert.Equal(GitPullRequestErrorCodes.PermissionMissing, error.Code);
        Assert.Null(_github.CreateBody);
    }

    [Fact]
    public async Task CA11_read_session_still_refuses_write_permissions()
    {
        _github.ReadPermissions = "{\"pull_requests\":\"write\",\"metadata\":\"read\"}";
        var error = await Assert.ThrowsAsync<GitPullRequestRejectedException>(() => _provider.FindOpenPullRequestByHeadAsync(_repository, Branch));
        Assert.Equal(GitPullRequestErrorCodes.PermissionMissing, error.Code);
    }

    [Theory]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task CA10_token_refused_means_permission_missing(HttpStatusCode status)
    {
        _github.WriteTokenStatus = status;
        var error = await Assert.ThrowsAsync<GitPullRequestRejectedException>(() => Create());
        Assert.Equal(GitPullRequestErrorCodes.PermissionMissing, error.Code);
        Assert.Null(_github.CreateBody);
    }

    [Theory]
    [InlineData("{\"message\":\"Validation Failed\",\"errors\":[{\"resource\":\"PullRequest\",\"code\":\"custom\",\"message\":\"A pull request already exists for qa:task/T-0027-abrir-pr.\"}]}", GitPullRequestErrorCodes.AlreadyExists)]
    [InlineData("{\"message\":\"Validation Failed\",\"errors\":[{\"resource\":\"PullRequest\",\"code\":\"custom\",\"message\":\"No commits between main and task/T-0027-abrir-pr\"}]}", GitPullRequestErrorCodes.NothingToCompare)]
    [InlineData("{\"message\":\"Validation Failed\",\"errors\":[{\"resource\":\"PullRequest\",\"field\":\"head\",\"code\":\"invalid\"}]}", GitPullRequestErrorCodes.HeadMissing)]
    [InlineData("{\"message\":\"Validation Failed\",\"errors\":[{\"resource\":\"PullRequest\",\"code\":\"custom\",\"message\":\"Draft pull requests are not supported in this repository.\"}]}", GitPullRequestErrorCodes.DraftUnsupported)]
    [InlineData("{\"message\":\"Validation Failed\",\"errors\":[{\"resource\":\"PullRequest\",\"field\":\"title\",\"code\":\"missing_field\"}]}", GitPullRequestErrorCodes.Invalid)]
    [InlineData("não é json", GitPullRequestErrorCodes.Invalid)]
    public async Task CA10_each_422_is_classified_without_github_text(string body, string code)
    {
        _github.CreateStatus = HttpStatusCode.UnprocessableEntity; _github.CreateError = body;
        var error = await Assert.ThrowsAsync<GitPullRequestRejectedException>(() => Create());
        Assert.Equal(code, error.Code);
        Assert.DoesNotContain("pull request", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CA10_unprocessable_body_over_64kb_is_invalid()
        => Assert.Equal(GitPullRequestErrorCodes.Invalid, GitHubProvider.ClassifyUnprocessable(null));

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task CA10_403_or_404_on_creation_means_permission_missing(HttpStatusCode status)
    {
        _github.CreateStatus = status;
        Assert.Equal(GitPullRequestErrorCodes.PermissionMissing, (await Assert.ThrowsAsync<GitPullRequestRejectedException>(() => Create())).Code);
    }

    [Fact]
    public async Task CA10_rate_limit_carries_retry_after_and_server_error_is_unavailable()
    {
        _github.CreateStatus = HttpStatusCode.TooManyRequests; _github.RetryAfterSeconds = 120;
        var limited = await Assert.ThrowsAsync<GitPullRequestRejectedException>(() => Create());
        Assert.Equal(GitPullRequestErrorCodes.RateLimited, limited.Code);
        Assert.Equal(Now.AddSeconds(120), limited.RetryAtUtc);
        // Enquanto vale o limite, nem pede token: o orçamento da instalação é respeitado.
        var requests = _github.Requests;
        Assert.Equal(GitPullRequestErrorCodes.RateLimited, (await Assert.ThrowsAsync<GitPullRequestRejectedException>(() => Create())).Code);
        Assert.Equal(requests, _github.Requests);
    }

    [Fact]
    public async Task CA10_server_error_is_unavailable_and_network_failure_too()
    {
        _github.CreateStatus = HttpStatusCode.BadGateway;
        Assert.Equal(GitPullRequestErrorCodes.Unavailable, (await Assert.ThrowsAsync<GitPullRequestRejectedException>(() => Create())).Code);
        _github.CreateStatus = HttpStatusCode.Created; _github.ThrowOnCreate = true;
        Assert.Equal(GitPullRequestErrorCodes.Unavailable, (await Assert.ThrowsAsync<GitPullRequestRejectedException>(() => Create())).Code);
    }

    [Fact]
    public async Task CA10_write_token_never_appears_in_exception_messages()
    {
        var messages = new List<string>();
        foreach (var status in new[] { HttpStatusCode.UnprocessableEntity, HttpStatusCode.Forbidden, HttpStatusCode.BadGateway })
        {
            _github.CreateStatus = status; _github.CreateError = "{\"message\":\"" + WriteFakeGitHub.WriteToken + "\"}";
            var error = await Assert.ThrowsAsync<GitPullRequestRejectedException>(() => Create());
            messages.Add(error.ToString());
        }
        Assert.All(messages, m => Assert.DoesNotContain(WriteFakeGitHub.WriteToken, m));
        Assert.All(messages, m => Assert.DoesNotContain(WriteFakeGitHub.ReadToken, m));
    }

    [Fact]
    public async Task Find_by_head_uses_the_head_filter_and_ignores_forks_with_the_same_branch()
    {
        _github.OpenPullRequests.Add((12, Branch, 555));   // fork com o mesmo nome de branch
        _github.OpenPullRequests.Add((13, Branch, 900001));
        var pr = await _provider.FindOpenPullRequestByHeadAsync(_repository, Branch);
        Assert.Equal(13, pr!.Number);
        Assert.Contains($"head={Uri.EscapeDataString("qa:" + Branch)}", _github.ListPath);
        Assert.Contains("state=open", _github.ListPath);
        _github.OpenPullRequests.Clear();
        Assert.Null(await _provider.FindOpenPullRequestByHeadAsync(_repository, Branch));
    }

    /// <summary>GitHub falso por HTTP: sessão de leitura, token de escrita, lista por head e criação, com falhas configuráveis.</summary>
    private sealed class WriteFakeGitHub : HttpMessageHandler, IHttpClientFactory
    {
        public const string ReadToken = "ghs_leitura_0123456789abcdefghijklmnop";
        public const string WriteToken = "ghs_escrita_0123456789abcdefghijklmnop";
        public List<string> TokenRequests { get; } = [];
        public string ReadPermissions { get; set; } = "{\"pull_requests\":\"read\",\"metadata\":\"read\"}";
        public string WritePermissions { get; set; } = "{\"pull_requests\":\"write\",\"metadata\":\"read\"}";
        public HttpStatusCode WriteTokenStatus { get; set; } = HttpStatusCode.Created;
        public HttpStatusCode CreateStatus { get; set; } = HttpStatusCode.Created;
        public string? CreateError { get; set; }
        public int? RetryAfterSeconds { get; set; }
        public bool ThrowOnCreate { get; set; }
        public string? CreateBody { get; private set; }
        public string? CreateAuthorization { get; private set; }
        public string? ListPath { get; private set; }
        public int Requests { get; private set; }
        public List<(int Number, string Head, long HeadRepo)> OpenPullRequests { get; } = [];
        private int _next = 7;
        public HttpClient CreateClient(string name) => new(this, false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests++;
            var path = request.RequestUri!.PathAndQuery;
            if (path == "/app/installations/1/access_tokens")
            {
                var body = await request.Content!.ReadAsStringAsync(ct);
                TokenRequests.Add(body);
                var write = body.Contains("\"write\"");
                if (write && WriteTokenStatus != HttpStatusCode.Created) return Json(WriteTokenStatus, "{\"message\":\"permissão\"}");
                var token = write ? WriteToken : ReadToken;
                return Json(HttpStatusCode.Created, $"{{\"token\":\"{token}\",\"expires_at\":\"{Now.AddHours(1):O}\",\"permissions\":{(write ? WritePermissions : ReadPermissions)}}}");
            }
            if (path == "/installation/repositories?per_page=100&page=1")
                return Json(HttpStatusCode.OK, "{\"total_count\":1,\"repositories\":[{\"id\":900001,\"full_name\":\"qa/metodo\"}]}");
            if (path.StartsWith("/repos/qa/metodo/pulls?", StringComparison.Ordinal))
            {
                ListPath = path;
                Assert.Equal(ReadToken, request.Headers.Authorization!.Parameter);
                return Json(HttpStatusCode.OK, "[" + string.Join(',', OpenPullRequests.Select(x => Pr(x.Number, x.Head, x.HeadRepo, false))) + "]");
            }
            if (path == "/repos/qa/metodo/pulls" && request.Method == HttpMethod.Post)
            {
                if (ThrowOnCreate) throw new HttpRequestException("rede");
                CreateAuthorization = request.Headers.Authorization!.Parameter;
                var body = await request.Content!.ReadAsStringAsync(ct);
                if (CreateStatus != HttpStatusCode.Created)
                {
                    var response = Json(CreateStatus, CreateError ?? "{\"message\":\"erro\"}");
                    if (RetryAfterSeconds is { } seconds) response.Headers.RetryAfter = new(TimeSpan.FromSeconds(seconds));
                    return response;
                }
                CreateBody = body;
                using var json = JsonDocument.Parse(body);
                return Json(HttpStatusCode.Created, Pr(_next++, json.RootElement.GetProperty("head").GetString()!, 900001, json.RootElement.GetProperty("draft").GetBoolean()));
            }
            return Json(HttpStatusCode.NotFound, "{}");
        }
        private static string Pr(int number, string head, long headRepo, bool draft) => JsonSerializer.Serialize(new
        {
            number, html_url = $"https://github.com/qa/metodo/pull/{number}", title = "PR", state = "open", draft, created_at = Now, updated_at = Now,
            closed_at = (DateTimeOffset?)null, merged = false, merged_at = (DateTimeOffset?)null, merge_commit_sha = (string?)null,
            head = new { @ref = head, sha = new string('b', 40), repo = new { id = headRepo } }, @base = new { @ref = "main", repo = new { id = 900001 } },
            user = new { id = 99, login = "prisma-metodo-vitor[bot]" },
        });
        private static HttpResponseMessage Json(HttpStatusCode status, string body)
            => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
