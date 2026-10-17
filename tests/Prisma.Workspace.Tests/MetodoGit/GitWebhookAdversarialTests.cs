using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Webhooks;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Infrastructure.Services.MethodGit;

namespace Prisma.Workspace.Tests.MetodoGit;

/// <summary>
/// Testes adversariais da Prova sobre a fatia B: assinatura sobre os bytes originais, normalização
/// do payload do GitHub e prioridade de identificação da task (explícito > branch > título).
/// </summary>
public class GitWebhookAdversarialTests
{
    private const string Secret = "segredo-de-teste-da-prova";

    private static string Sign(byte[] body, string secret = Secret)
        => "sha256=" + Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body)).ToLowerInvariant();

    // ---- HMAC ----

    [Fact]
    public void Signature_MatchesGitHubDocumentedVector()
    {
        // Vetor da documentação do GitHub ("Validating webhook deliveries").
        var body = Encoding.UTF8.GetBytes("Hello, World!");
        Assert.True(GitHubCryptography.VerifySignature(body,
            "sha256=757107ea0eb2509fc211221cce984b8a37570b6d7586c22c46f4379c8b043e17", "It's a Secret to Everybody"));
    }

    [Fact]
    public void Signature_IsOverOriginalBytes_NotOverReserializedJson()
    {
        var original = Encoding.UTF8.GetBytes("{\"title\":\"Correção\",  \"n\":1}");
        var reserialized = JsonSerializer.SerializeToUtf8Bytes(JsonDocument.Parse(original).RootElement);
        var sig = Sign(original);
        Assert.True(GitHubCryptography.VerifySignature(original, sig, Secret));
        Assert.False(GitHubCryptography.VerifySignature(reserialized, sig, Secret), "reserializar muda os bytes e precisa falhar");
    }

    [Theory]
    [InlineData("wrong-secret")]
    [InlineData("tamper")]
    [InlineData("sha1")]
    [InlineData("no-prefix")]
    [InlineData("short")]
    [InlineData("long")]
    [InlineData("non-hex")]
    [InlineData("empty")]
    [InlineData("spaces")]
    public void Signature_RejectsEveryMalformedOrForgedVariant(string variant)
    {
        var body = Encoding.UTF8.GetBytes("{\"action\":\"opened\"}");
        var good = Sign(body);
        var (b, sig) = variant switch
        {
            "wrong-secret" => (body, Sign(body, Secret + "x")),
            "tamper" => (Encoding.UTF8.GetBytes("{\"action\":\"closed\"}"), good),
            "sha1" => (body, "sha1=" + Convert.ToHexString(HMACSHA1.HashData(Encoding.UTF8.GetBytes(Secret), body)).ToLowerInvariant()),
            "no-prefix" => (body, good[7..]),
            "short" => (body, good[..^1]),
            "long" => (body, good + "0"),
            "non-hex" => (body, "sha256=" + new string('z', 64)),
            "empty" => (body, ""),
            "spaces" => (body, " " + good),
            _ => throw new ArgumentOutOfRangeException(nameof(variant)),
        };
        Assert.False(GitHubCryptography.VerifySignature(b, sig, Secret), variant);
    }

    [Fact]
    public void Signature_WithEmptySecret_NeverValidates()
    {
        var body = Encoding.UTF8.GetBytes("{}");
        Assert.False(GitHubCryptography.VerifySignature(body, Sign(body, ""), ""));
    }

    // ---- Normalização ----

    private static Dictionary<string, object?> PullRequestPayload(string action = "opened", string branch = "task/T-0001-botao",
        string title = "Ajuste", long headRepoId = 900001, bool draft = false, bool merged = false, string htmlHost = "github.com")
    {
        var repo = new Dictionary<string, object?> { ["id"] = 900001L, ["full_name"] = "qa/metodo" };
        return new()
        {
            ["action"] = action,
            ["repository"] = repo,
            ["installation"] = new Dictionary<string, object?> { ["id"] = 1L },
            ["sender"] = new Dictionary<string, object?> { ["id"] = 4242L, ["login"] = "dev" },
            ["pull_request"] = new Dictionary<string, object?>
            {
                ["number"] = 7, ["state"] = action == "closed" ? "closed" : "open", ["draft"] = draft, ["merged"] = merged,
                ["title"] = title, ["html_url"] = $"https://{htmlHost}/qa/metodo/pull/7",
                ["updated_at"] = "2026-10-16T08:00:00Z", ["closed_at"] = action == "closed" ? "2026-10-16T08:05:00Z" : null,
                ["merge_commit_sha"] = merged ? new string('a', 40) : null,
                ["head"] = new Dictionary<string, object?> { ["ref"] = branch, ["sha"] = new string('b', 40),
                    ["repo"] = new Dictionary<string, object?> { ["id"] = headRepoId, ["full_name"] = headRepoId == 900001 ? "qa/metodo" : "estranho/fork" } },
                ["base"] = new Dictionary<string, object?> { ["ref"] = "main", ["sha"] = new string('c', 40), ["repo"] = repo },
            },
        };
    }

    private static NormalizedGitEvent Normalize(string eventName, object payload)
        => GitEventNormalizer.Normalize(eventName, JsonSerializer.SerializeToUtf8Bytes(payload));

    [Fact]
    public void Normalize_SameFactGivesSameRevision_DifferentFactDiffers()
    {
        var a = Normalize("pull_request", PullRequestPayload());
        var b = Normalize("pull_request", PullRequestPayload());
        Assert.Equal(a.ProviderRevision, b.ProviderRevision);
        var later = PullRequestPayload();
        ((Dictionary<string, object?>)later["pull_request"]!)["updated_at"] = "2026-10-16T09:00:00Z";
        Assert.NotEqual(a.ProviderRevision, Normalize("pull_request", later).ProviderRevision);
        Assert.NotEqual(a.ProviderRevision, Normalize("pull_request", PullRequestPayload("ready_for_review")).ProviderRevision);
    }

    [Fact]
    public void Normalize_DropsBodyAndKeepsOnlyMinimalFields()
    {
        var p = PullRequestPayload();
        ((Dictionary<string, object?>)p["pull_request"]!)["body"] = "descrição longa com segredo-xyz";
        var e = Normalize("pull_request", p);
        Assert.DoesNotContain("segredo-xyz", JsonSerializer.Serialize(e));
    }

    [Theory]
    [InlineData("gitlab.com")]
    [InlineData("github.com.evil.example")]
    public void Normalize_RejectsPullRequestUrlOutsideGitHub(string host)
        => Assert.Throws<MethodOperationException>(() => Normalize("pull_request", PullRequestPayload(htmlHost: host)));

    [Theory]
    [InlineData("Opened")]
    [InlineData("opened;drop")]
    [InlineData("")]
    public void Normalize_RejectsUnexpectedActionText(string action)
        => Assert.Throws<MethodOperationException>(() => Normalize("pull_request", PullRequestPayload(action)));

    [Fact]
    public void Normalize_MissingRepository_IsBadRequestNotCrash()
    {
        var p = PullRequestPayload(); p.Remove("repository");
        var ex = Assert.Throws<MethodOperationException>(() => Normalize("pull_request", p));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void Normalize_CreateOfTag_IsUnsupported_AndPingIsIgnored()
    {
        var tag = Normalize("create", new Dictionary<string, object?> { ["ref"] = "v1.0", ["ref_type"] = "tag", ["repository"] = new Dictionary<string, object?> { ["id"] = 900001L } });
        Assert.Equal("unsupported", tag.Action);
        var ping = Normalize("ping", new Dictionary<string, object?> { ["zen"] = "x", ["repository"] = new Dictionary<string, object?> { ["id"] = 900001L } });
        Assert.Null(ping.PullRequestNumber);
    }

    [Fact]
    public void Normalize_TitleWithTab_DoesNotDiscardTheWholeDelivery()
    {
        // GitHub aceita TAB no título do PR. Recusar o payload inteiro (400) perde a entrega na 2a, que não tem reconciliação.
        var e = Record.Exception(() => Normalize("pull_request", PullRequestPayload(title: "Corrige\tconsulta")));
        Assert.Null(e);
    }

    [Fact]
    public void Normalize_ExposesHeadRepository_SoForkBranchesCanBeIgnored()
    {
        // RF-02.4: payload de outro repositório não gera vínculo nem movimento. Um PR vindo de fork com branch
        // task/T-0001-x tem base no repositório cadastrado, mas a branch é de terceiros.
        var fork = Normalize("pull_request", PullRequestPayload(headRepoId: 777777));
        var same = Normalize("pull_request", PullRequestPayload());
        Assert.False(fork == same with { ProviderRevision = fork.ProviderRevision },
            "o evento normalizado precisa distinguir head de fork (ex.: HeadRepositoryId) para não identificar a task pela branch do fork");
    }

    // ---- Resolução da task ----

    private static readonly Guid Org = Guid.NewGuid(), Project = Guid.NewGuid(), Integration = Guid.NewGuid();
    private static GitRepositoryDescriptor Repo() => new(Integration, Org, Project, "github", "github.com", 900001, "qa/metodo", "main",
        424242, 1, "test-app-key", "test-webhook-secret", null, null, Guid.NewGuid(), true);
    private static MethodTaskSnapshot Task(string id, Guid? project = null, bool top = true) => new(Org, project ?? Project, Guid.NewGuid(),
        Guid.NewGuid(), id, Guid.NewGuid(), "em-desenvolvimento", Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow, null, null, null, null, null,
        top, false, new byte[8]);

    private static GitTaskResolver Resolver(Dictionary<string, List<MethodTaskSnapshot>> byId, MethodTaskSnapshot? explicitTask = null)
    {
        var store = DispatchProxy.Create<IMethodGitStore, ResolverStore>();
        var s = (ResolverStore)(object)store;
        s.ById = byId; s.Explicit = explicitTask;
        return new GitTaskResolver(store);
    }

    [Fact]
    public async Task Resolve_ExplicitWinsOverBranchAndTitle()
    {
        var t1 = Task("T-0001"); var t2 = Task("T-0002"); var t3 = Task("T-0003");
        var r = await Resolver(new() { ["T-0002"] = [t2], ["T-0003"] = [t3] }, t1)
            .ResolveAsync(Repo(), 7, "task/T-0002-x", "[T-0003] título");
        Assert.Same(t1, r.Task);
        Assert.Equal("explicit", r.Source);
    }

    [Fact]
    public async Task Resolve_BranchWinsOverTitle()
    {
        var t2 = Task("T-0002"); var t3 = Task("T-0003");
        var r = await Resolver(new() { ["T-0002"] = [t2], ["T-0003"] = [t3] }).ResolveAsync(Repo(), 7, "task/T-0002-x", "[T-0003] título");
        Assert.Same(t2, r.Task);
    }

    [Theory]
    [InlineData("task/T-9999-inexistente")]
    [InlineData("task/T-001-tres-digitos")]
    [InlineData("task/T-0002")]
    public async Task Resolve_InvalidPriorityBranch_DoesNotFallBackToTitle(string branch)
    {
        var t3 = Task("T-0003");
        var r = await Resolver(new() { ["T-0003"] = [t3] }).ResolveAsync(Repo(), 7, branch, "[T-0003] título");
        Assert.Null(r.Task);
    }

    [Fact]
    public async Task Resolve_DuplicatedIdInProject_IsAmbiguous_NotFirstMatch()
    {
        var r = await Resolver(new() { ["T-0002"] = [Task("T-0002"), Task("T-0002")] }).ResolveAsync(Repo(), 7, "task/T-0002-x", null);
        Assert.Null(r.Task);
        Assert.True(r.Conflict);
    }

    [Fact]
    public async Task Resolve_TaskOfAnotherProjectOrSubitem_IsRejected()
    {
        Assert.Null((await Resolver(new() { ["T-0002"] = [Task("T-0002", project: Guid.NewGuid())] }).ResolveAsync(Repo(), 7, "task/T-0002-x", null)).Task);
        Assert.Null((await Resolver(new() { ["T-0002"] = [Task("T-0002", top: false)] }).ResolveAsync(Repo(), 7, "task/T-0002-x", null)).Task);
    }

    [Fact]
    public async Task Resolve_FreeTitleAndOffPatternBranch_IsUnlinked()
    {
        // Caso DETRAN sem vínculo nem branch no padrão: não vinculado, sem heurística.
        var r = await Resolver(new() { ["T-0007"] = [Task("T-0007")] }).ResolveAsync(Repo(), 7, "feature/corrige-consulta", "Corrige consulta T-0007 de multas");
        Assert.Null(r.Task);
        Assert.Equal("task_not_identified", r.Reason);
    }

    [Fact]
    public async Task Resolve_SameMarkerRepeatedInTitle_IsNotAmbiguous()
    {
        // "[T-0003] ... (revisão do [T-0003])": um único ID, ainda que repetido.
        var t3 = Task("T-0003");
        var r = await Resolver(new() { ["T-0003"] = [t3] }).ResolveAsync(Repo(), 7, "feature/x", "[T-0003] ajuste (continuação do [T-0003])");
        Assert.Same(t3, r.Task);
    }

    public class ResolverStore : DispatchProxy
    {
        public Dictionary<string, List<MethodTaskSnapshot>> ById = new();
        public MethodTaskSnapshot? Explicit;
        protected override object? Invoke(MethodInfo? method, object?[]? args) => method?.Name switch
        {
            nameof(IMethodGitStore.GetExplicitPullRequestLinkAsync) => System.Threading.Tasks.Task.FromResult(Explicit is null ? null
                : new TaskGitLinkDto(Guid.NewGuid(), Integration, Explicit.WorkItemId, null, null, "explicit", null, null, "user", DateTimeOffset.UtcNow)),
            nameof(IMethodGitStore.GetTaskAsync) => System.Threading.Tasks.Task.FromResult(Explicit),
            nameof(IMethodGitStore.FindTasksAsync) => System.Threading.Tasks.Task.FromResult<IReadOnlyList<MethodTaskSnapshot>>(
                ById.TryGetValue((string)args![1]!, out var l) ? l : []),
            _ => throw new InvalidOperationException($"O resolver não deveria chamar {method?.Name}."),
        };
    }
}
