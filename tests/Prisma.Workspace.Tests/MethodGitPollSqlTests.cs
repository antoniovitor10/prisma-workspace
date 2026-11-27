using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Repositories;
using Prisma.Workspace.Application.Features.MethodGit.Webhooks;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Identity.MethodTokens;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Repositories;
using Prisma.Workspace.Infrastructure.Services.MethodGit;
using Prisma.Workspace.Infrastructure.Services.MethodSecurity;
using Prisma.Workspace.Infrastructure.Tenancy;
using Xunit;

namespace Prisma.Workspace.Tests;

/// <summary>T-0007 e T-0008 (HU-0002): token de leitura, cadastro no modo consulta e consulta com movimento, no SQL descartável.</summary>
[Collection("MethodGit SQL")]
public sealed class MethodGitPollSqlTests : IClassFixture<GitDeliverySqlFixture>
{
    private const string Token = "github_pat_E2Efake0123456789abcdefghijklmnopqrstu";
    private static readonly IDataProtectionProvider Protection = new EphemeralDataProtectionProvider();
    private readonly GitDeliverySqlFixture _fixture;
    public MethodGitPollSqlTests(GitDeliverySqlFixture fixture) => _fixture = fixture;

    [MethodGitSqlFact]
    public async Task CA13b_token_is_stored_only_encrypted_and_never_returned()
    {
        await using var c = await _fixture.CaseAsync();
        var human = c.Actor!.ActorId;
        var saved = await Credentials(c).SaveAsync(c.ProjectId, "detranse", Token, human);
        Assert.Equal("github_pat_E2Efa", saved.TokenPrefix); Assert.Equal("detranse", saved.GitHubOwner); Assert.Equal("Humano SQL", saved.CreatedByName);
        var listed = await Credentials(c).ListAsync(c.ProjectId);
        Assert.DoesNotContain(Token, JsonSerializer.Serialize(saved)); Assert.DoesNotContain(Token, JsonSerializer.Serialize(listed));
        Assert.DoesNotContain(Token, new SaveGitReadCredentialRequest("detranse", Token).ToString());
        var row = await c.Context.Set<GitReadCredential>().AsNoTracking().SingleAsync(x => x.Id == saved.Id);
        Assert.NotEqual(Token, row.ProtectedValue); Assert.DoesNotContain(Token[11..], row.ProtectedValue);
        Assert.Equal(Token, await TokenSource(c).ResolveAsync(saved.Id, c.ProjectId, c.OrganizationId));
        // Outra organização ou outro projeto não decifra.
        await Assert.ThrowsAsync<GitHubUnavailableException>(() => TokenSource(c).ResolveAsync(saved.Id, Guid.NewGuid(), c.OrganizationId));
        // Troca no lugar e revogação: o valor revogado deixa de existir no banco.
        var replaced = await Credentials(c).SaveAsync(c.ProjectId, "detranse", Token.Replace("E2E", "XYZ"), human);
        Assert.Equal(saved.Id, replaced.Id); Assert.Equal("github_pat_XYZfa", replaced.TokenPrefix);
        var revoked = await Credentials(c).RevokeAsync(c.ProjectId, saved.Id, human);
        Assert.NotNull(revoked.RevokedAtUtc);
        Assert.Equal("revoked", (await c.Context.Set<GitReadCredential>().AsNoTracking().SingleAsync(x => x.Id == saved.Id)).ProtectedValue);
        var error = await Assert.ThrowsAsync<GitHubUnavailableException>(() => TokenSource(c).ResolveAsync(saved.Id, c.ProjectId, c.OrganizationId));
        Assert.Equal("git_credential_revoked", error.Code);
        var invalid = await Assert.ThrowsAsync<MethodOperationException>(() => Credentials(c).SaveAsync(c.ProjectId, "detranse", "ghp_classico123", human));
        Assert.Equal("invalid_git_read_token", invalid.Code); Assert.DoesNotContain("ghp_classico123", invalid.Message);
    }

    [MethodGitSqlFact]
    public async Task CA9_poll_repository_is_registered_without_app_ids_or_webhook_after_a_test_read()
    {
        await using var c = await _fixture.CaseAsync();
        var human = c.Actor!.ActorId;
        var provider = new FakePollProvider { RepositoryId = Random.Shared.NextInt64(1_000_000, long.MaxValue) };
        var missing = await Assert.ThrowsAsync<MethodOperationException>(() => Repositories(c, provider).RegisterPollAsync(c.ProjectId,
            new("detranse/canais-mobile", "main", c.Repository.SystemPrincipalId), human, default));
        Assert.Equal("git_read_credential_missing", missing.Code);
        var credential = await Credentials(c).SaveAsync(c.ProjectId, "detranse", Token, human);
        var dto = await Repositories(c, provider).RegisterPollAsync(c.ProjectId, new("detranse/canais-mobile", "main", c.Repository.SystemPrincipalId), human, default);
        Assert.Equal("poll", dto.Mode); Assert.Null(dto.AppId); Assert.Null(dto.InstallationId); Assert.Equal("ok", dto.LastPollStatus);
        Assert.Equal(provider.RepositoryId, dto.RepositoryId); Assert.Equal(credential.Id, dto.ReadCredentialId);
        var row = await c.Context.Set<GitRepositoryIntegration>().AsNoTracking().SingleAsync(x => x.Id == dto.Id);
        Assert.Equal(GitIntegrationModes.Poll, row.Mode); Assert.Null(row.AppId); Assert.Null(row.InstallationId);
        Assert.Null(row.WebhookSecretRef); Assert.Null(row.AppPrivateKeySecretRef); Assert.NotNull(row.LastPolledAtUtc);
        Assert.Equal(1, (await Credentials(c).ListAsync(c.ProjectId)).Single().RepositoriesCount);
        // Webhook para integração no modo consulta é recusado como não autenticado (não há segredo).
        var descriptor = (await c.Store(c.Context).GetRepositoryForIngressAsync(dto.Id))!;
        Assert.True(descriptor.IsPoll);
        var ingress = new GitWebhookIngress(c.Store(c.Context), new TestGitProvider(), new TestGitSecrets(), new TestWorkerInitializer(),
            new GitWebhookIngressLimiter(), NullLogger<GitWebhookIngress>.Instance, c.Clock);
        var refused = await Assert.ThrowsAsync<MethodOperationException>(() => ingress.AcceptAsync(new(dto.Id, Guid.NewGuid().ToString("D"), "ping", "sha256=x", "{}"u8.ToArray(), "c")));
        Assert.Equal(401, refused.StatusCode);
    }

    [MethodGitSqlFact]
    public async Task CA11_CA12b_poll_links_pr_of_branch_without_id_and_moves_it_through_the_webhook_queue_with_channel_consulta()
    {
        await using var c = await _fixture.CaseAsync();
        var repository = await ToPollAsync(c);
        await c.UseAgentAsync();
        await Links(c).LinkBranchAsync(c.ProjectId, "T-0001", new(repository.RepositoryFullName, "feat/pagamento"), c.Actor!.ActorId, default);
        var provider = new FakePollProvider { RepositoryId = repository.RepositoryId };
        provider.Pulls.Add(Pr(c, 5, "feat/pagamento", "open", c.Now));

        var first = await PollAsync(c, provider);
        Assert.True(first.Success); Assert.Equal(1, first.PullRequestsEvaluated); Assert.Equal(1, first.BranchesPushed);
        var outcome = Assert.Single(await DrainAsync(c, provider));
        Assert.True(outcome.Moved, outcome.Code);
        Assert.Equal(c.InReviewStageId, await StageAsync(c));
        var events = await c.NewContext().TaskEvents.AsNoTracking().Where(x => x.WorkItemId == c.ItemId).ToListAsync();
        var move = Assert.Single(events, x => x.Payload?.Contains("\"factType\"") == true);
        using (var payload = JsonDocument.Parse(move.Payload!))
        {
            Assert.Equal(MethodChannels.Poll, payload.RootElement.GetProperty("channel").GetString());
            Assert.Equal(MethodGitFactCodes.PullRequestOpened, payload.RootElement.GetProperty("factType").GetString());
        }
        var link = await c.NewContext().Set<TaskGitLink>().AsNoTracking().SingleAsync(x => x.WorkItemId == c.ItemId && x.PullRequestObservationId == null);
        Assert.Equal(GitBranchStates.Remote, link.BranchState);
        Assert.Single(await c.NewContext().Set<TaskGitLink>().AsNoTracking().Where(x => x.WorkItemId == c.ItemId && x.PullRequestObservationId != null).ToListAsync());
        // Consulta agendada seguinte, sem mudança no GitHub: nada novo na fila.
        c.Clock.Now = c.Now.AddHours(1).AddMinutes(1);
        Assert.Equal(0, (await PollAsync(c, provider)).PullRequestsEvaluated);

        // Merge na branch principal: vai para em-teste.
        provider.Pulls[0] = Pr(c, 5, "feat/pagamento", "closed", c.Now.AddHours(1).AddMinutes(5), merged: true);
        c.Clock.Now = c.Now.AddHours(2).AddMinutes(2);
        Assert.Equal(1, (await PollAsync(c, provider, manual: true)).PullRequestsEvaluated);
        Assert.True(Assert.Single(await DrainAsync(c, provider)).Moved);
        Assert.Equal(c.StageOf(MethodStatusCodes.InTest), await StageAsync(c));
    }

    [MethodGitSqlFact]
    public async Task CA11_changes_requested_and_closed_without_merge_return_to_development()
    {
        await using var c = await _fixture.CaseAsync();
        var repository = await ToPollAsync(c);
        var provider = new FakePollProvider { RepositoryId = repository.RepositoryId };
        provider.Pulls.Add(Pr(c, 8, "task/T-0001-consulta", "open", c.Now));
        await PollAsync(c, provider); await DrainAsync(c, provider);
        Assert.Equal(c.InReviewStageId, await StageAsync(c));
        provider.Reviews.Add(new(31, "4343", "changes_requested", new string('b', 40), c.Now.AddMinutes(20)));
        provider.Pulls[0] = Pr(c, 8, "task/T-0001-consulta", "open", c.Now.AddMinutes(20));
        c.Clock.Now = c.Now.AddMinutes(30);
        await PollAsync(c, provider, manual: true);
        var drainedCr = await DrainAsync(c, provider);
        Assert.True(drainedCr.Count == 1, await DumpAsync(c) + " / " + string.Join(",", drainedCr));
        Assert.True(drainedCr[0].Moved);
        Assert.Equal(c.InDevelopmentStageId, await StageAsync(c));
        var observation = await c.NewContext().Set<GitPullRequestObservation>().AsNoTracking().SingleAsync(x => x.IntegrationId == repository.Id && x.PullRequestNumber == 8);
        Assert.True(observation.ChangesRequested);

        // Reaberto para revisão sem mudanças pedidas e depois fechado sem merge: volta para desenvolvimento.
        provider.Reviews.Clear();
        provider.Pulls[0] = Pr(c, 8, "task/T-0001-consulta", "open", c.Now.AddMinutes(40), headSha: "d");
        c.Clock.Now = c.Now.AddMinutes(45);
        await PollAsync(c, provider, manual: true); await DrainAsync(c, provider);
        Assert.Equal(c.InReviewStageId, await StageAsync(c));
        provider.Pulls[0] = Pr(c, 8, "task/T-0001-consulta", "closed", c.Now.AddMinutes(50), headSha: "d");
        c.Clock.Now = c.Now.AddMinutes(55);
        await PollAsync(c, provider, manual: true);
        Assert.True(Assert.Single(await DrainAsync(c, provider)).Moved);
        Assert.Equal(c.InDevelopmentStageId, await StageAsync(c));
    }

    [MethodGitSqlFact]
    public async Task CA12_poll_never_authorizes_never_moves_without_pr_and_does_not_reapply_a_webhook_fact()
    {
        await using var c = await _fixture.CaseAsync();
        var repository = await ToPollAsync(c);
        var backlog = await c.AddTaskAsync("T-0002");
        await c.Context.WorkItems.Where(x => x.Id == backlog).ExecuteUpdateAsync(set => set.SetProperty(x => x.StageId, c.StageOf(MethodStatusCodes.Backlog)));
        await c.UseAgentAsync();
        // Só branch, sem PR: nada se move.
        await Links(c).LinkBranchAsync(c.ProjectId, "T-0001", new(repository.RepositoryFullName, "feat/sem-pr"), c.Actor!.ActorId, default);
        var provider = new FakePollProvider { RepositoryId = repository.RepositoryId };
        provider.Branches.Add("feat/sem-pr");
        provider.Pulls.Add(Pr(c, 12, "task/T-0002-backlog", "open", c.Now));
        var result = await PollAsync(c, provider);
        Assert.Equal(1, result.BranchesPushed);
        var outcomes = await DrainAsync(c, provider);
        Assert.DoesNotContain(outcomes, o => o.Moved);
        Assert.Equal(c.StageOf(MethodStatusCodes.Backlog), (await c.NewContext().WorkItems.AsNoTracking().SingleAsync(x => x.Id == backlog)).StageId);
        Assert.Equal(c.InDevelopmentStageId, await StageAsync(c));

        // Fato aplicado pelo webhook não é reaplicado pela consulta.
        provider.Pulls.Add(Pr(c, 13, "task/T-0001-webhook", "open", c.Now));
        Assert.True((await ProcessWebhookOpenedAsync(c, provider, 13)).Moved);
        Assert.Equal(c.InReviewStageId, await StageAsync(c));
        c.Clock.Now = c.Now.AddMinutes(5);
        await PollAsync(c, provider, manual: true);
        Assert.DoesNotContain(await DrainAsync(c, provider), o => o.Moved);
        Assert.Equal(c.InReviewStageId, await StageAsync(c));
        Assert.Single(await c.NewContext().Set<GitAppliedFact>().AsNoTracking().Where(x => x.WorkItemId == c.ItemId).ToListAsync());
    }

    [MethodGitSqlFact]
    public async Task CA13_token_failure_records_the_code_keeps_last_success_and_moves_nothing()
    {
        await using var c = await _fixture.CaseAsync();
        var repository = await ToPollAsync(c);
        var lastSuccess = repository.LastPolledAtUtc;
        var logs = new ListLogger();
        foreach (var (status, expected) in new[] { (401, GitPollStatus.TokenInvalid), (403, GitPollStatus.TokenForbidden), (404, GitPollStatus.RepositoryNotFound) })
        {
            var provider = new FakePollProvider { RepositoryId = repository.RepositoryId,
                Fail = new GitHubUnavailableException(status == 404 ? "git_not_found" : "git_access_unavailable", null, status) };
            provider.Pulls.Add(Pr(c, 20, "task/T-0001-falha", "open", c.Now));
            var result = await PollAsync(c, provider, logger: logs);
            Assert.False(result.Success); Assert.Equal(expected, result.Status);
            var row = await c.NewContext().Set<GitRepositoryIntegration>().AsNoTracking().SingleAsync(x => x.Id == repository.Id);
            Assert.Equal(expected, row.LastPollStatus); Assert.Equal(lastSuccess, row.LastPolledAtUtc);
        }
        Assert.Empty(await c.NewContext().Set<GitWebhookDelivery>().AsNoTracking().Where(x => x.IntegrationId == repository.Id).ToListAsync());
        Assert.Equal(c.InDevelopmentStageId, await StageAsync(c));
        Assert.DoesNotContain(logs.Messages, m => m.Contains(Token, StringComparison.Ordinal));
        Assert.NotEmpty(logs.Messages);
    }

    [MethodGitSqlFact]
    public async Task CA9_CA10_hourly_poll_runs_once_with_two_concurrent_workers_and_poll_now_is_limited_to_one_per_minute()
    {
        await using var c = await _fixture.CaseAsync();
        var repository = await ToPollAsync(c);
        var provider = new FakePollProvider { RepositoryId = repository.RepositoryId, Delay = TimeSpan.FromMilliseconds(300) };
        await using var services = Services(c, provider);
        var scopes = services.GetRequiredService<IServiceScopeFactory>();
        var locks = new SqlGitPollLock(scopes);
        var one = new GitPollRunner(scopes, locks, NullLogger<GitPollRunner>.Instance, c.Clock);
        var two = new GitPollRunner(scopes, locks, NullLogger<GitPollRunner>.Instance, c.Clock);
        await Task.WhenAll(one.PollDueAsync(), two.PollDueAsync());
        Assert.Equal(1, provider.ListCalls);
        // Antes de 1 hora, nada; depois de 1 hora, de novo uma vez.
        c.Clock.Now = c.Now.AddMinutes(30);
        await Task.WhenAll(one.PollDueAsync(), two.PollDueAsync());
        Assert.Equal(1, provider.ListCalls);
        c.Clock.Now = c.Now.AddMinutes(61);
        await Task.WhenAll(one.PollDueAsync(), two.PollDueAsync());
        Assert.Equal(2, provider.ListCalls);
        // "Consultar agora": roda na hora; um segundo clique dentro de 1 minuto é recusado; depois de 1 minuto, roda de novo.
        var now = await one.PollNowAsync(repository.Id);
        Assert.True(now.Success); Assert.Equal(c.Clock.Now, now.LastPolledAtUtc); Assert.Equal(3, provider.ListCalls);
        var tooSoon = await Assert.ThrowsAsync<MethodOperationException>(() => one.PollNowAsync(repository.Id));
        Assert.Equal(429, tooSoon.StatusCode); Assert.Equal("git_poll_too_soon", tooSoon.Code); Assert.Equal(3, provider.ListCalls);
        c.Clock.Now = c.Now.AddMinutes(62);
        Assert.True((await one.PollNowAsync(repository.Id)).Success); Assert.Equal(4, provider.ListCalls);
    }

    // ---------- apoio ----------
    private static GitPullRequestSnapshot Pr(GitDeliverySqlFixture.SqlCase c, int number, string branch, string state, DateTimeOffset at,
        bool merged = false, string headSha = "b") => new(number, $"https://github.com/qa/metodo/pull/{number}", $"PR {number}", branch, "main",
        new string(headSha[0], 40), merged ? new string('a', 40) : null, state, false, merged, at, OpenedAtUtc: c.Now,
        MergedAtUtc: merged ? at : null, ClosedAtUtc: state == "closed" ? at : null, HeadRepositoryId: c.Repository.RepositoryId, PullRequestAuthorName: "dev");

    private static EfGitReadCredentialStore Credentials(GitDeliverySqlFixture.SqlCase c)
        => new(c.Context, new Tenant(c.OrganizationId), Protection, new Names(), c.Clock);
    private static GitReadTokenSource TokenSource(GitDeliverySqlFixture.SqlCase c)
    {
        var services = new ServiceCollection();
        var options = c.Options();
        services.AddScoped(_ => new AppDbContext(options));
        return new GitReadTokenSource(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), Protection);
    }
    private static async Task<GitRepositoryDescriptor> ToPollAsync(GitDeliverySqlFixture.SqlCase c)
    {
        var credential = await Credentials(c).SaveAsync(c.ProjectId, "qa", Token, c.Actor!.ActorId);
        // Delegação real do principal de sistema (o guarda de escrita avalia a permissão de quem delegou).
        c.Context.Add(OrganizationMember.Create(c.OrganizationId, c.Actor!.ActorId, OrganizationRole.Administrator));
        await c.Context.SaveChangesAsync();
        await c.Context.Database.ExecuteSqlRawAsync("UPDATE [AspNetUsers] SET [EmailConfirmed]=1 WHERE [Id]={0}", c.Actor!.ActorId);
        await c.Context.Database.ExecuteSqlRawAsync("UPDATE [GitRepositoryIntegrations] SET [Mode]='poll', [ReadCredentialId]={1}, [AppId]=NULL, [InstallationId]=NULL, "
            + "[AppPrivateKeySecretRef]=NULL, [WebhookSecretRef]=NULL, [LastPolledAtUtc]={2}, [LastPollStatus]='ok' WHERE [Id]={0}",
            c.Repository.Id, credential.Id, c.Now.AddHours(-2));
        c.Context.ChangeTracker.Clear();
        return (await c.Store(c.Context).GetRepositoryAsync(c.Repository.Id))!;
    }
    private static GitRepositoryOperations Repositories(GitDeliverySqlFixture.SqlCase c, IGitProvider provider)
        => new(c.Store(c.Context), provider, new TestGitSecrets(), c.Access, new AllowAll(), c.Clock, Credentials(c));
    private static GitPullRequestOperations Links(GitDeliverySqlFixture.SqlCase c)
    {
        var store = c.Store(c.Context); var provider = new TestGitProvider();
        return new(store, provider, new GitRepositoryOperations(store, provider, new TestGitSecrets(), c.Access, new AllowAll(), c.Clock), c.Access, new AllowAll());
    }
    private static MethodActorIdentity Worker(GitDeliverySqlFixture.SqlCase c, string channel) => new(MethodAuthenticationKind.IntegrationWorker, MethodRole.System,
        c.Repository.SystemPrincipalId, $"method:{c.Repository.SystemPrincipalId:D}", "Integração SQL", c.Repository.OrganizationId, c.ProjectId, null, null,
        Enum.GetValues<PlatformPermission>().ToHashSet(), channel, "poll:sql");
    private static async Task<GitPollResultDto> PollAsync(GitDeliverySqlFixture.SqlCase c, IGitProvider provider, bool manual = false, ILogger<GitPollService>? logger = null)
    {
        c.UseActor(Worker(c, MethodChannels.Poll)); c.Context.ChangeTracker.Clear();
        return await new GitPollService(c.Store(c.Context), provider, logger ?? NullLogger<GitPollService>.Instance, c.Clock)
            .PollAsync(c.Repository.Id, manual, $"poll:{Guid.NewGuid():N}");
    }
    private static async Task<List<GitProcessingOutcome>> DrainAsync(GitDeliverySqlFixture.SqlCase c, IGitProvider provider)
    {
        var outcomes = new List<GitProcessingOutcome>();
        for (var i = 0; i < 20; i++)
        {
            // Contexto como o da API: com a identidade do método, o gravador registra histórico, fato aplicado e entrega processada.
            await using var work = new AppDbContext(c.Options(), new Tenant(c.OrganizationId), new CurrentAuditContext(), c.Access, new MethodOn(), c.Clock);
            var store = c.Store(work);
            var lease = await store.ClaimNextDeliveryAsync(c.Clock.Now, TimeSpan.FromMinutes(5));
            if (lease is null) break;
            var snapshot = (await store.GetDeliveryAsync(lease.Id))!;
            c.UseActor(Worker(c, snapshot.DeliveryId.StartsWith("poll:", StringComparison.Ordinal) ? MethodChannels.Poll : MethodChannels.Webhook));
            var movement = new MethodMoveService(work, c.Access, store, new WorkflowRepository(work), new AllowAll(), new SilentRealtime(),
                NullLogger<MethodMoveService>.Instance, c.Clock);
            outcomes.Add(await new GitDeliveryProcessor(store, provider, new GitTaskResolver(store), movement, NullLogger<GitDeliveryProcessor>.Instance, c.Clock)
                .ProcessAsync(snapshot));
        }
        return outcomes;
    }
    private static async Task<GitProcessingOutcome> ProcessWebhookOpenedAsync(GitDeliverySqlFixture.SqlCase c, FakePollProvider provider, int number)
    {
        var pr = provider.Pulls.Single(x => x.Number == number);
        c.UseActor(Worker(c, MethodChannels.Webhook));
        var store = c.Store(c.Context);
        var e = new NormalizedGitEvent("github", c.Repository.RepositoryId, null, "pull_request", "opened", c.Now.AddSeconds(1), "rev-wh", null, number,
            pr.Url, pr.Title, pr.HeadBranch, pr.BaseBranch, pr.HeadSha, null, "open");
        await store.AcceptDeliveryAsync(c.Repository.Id, Guid.NewGuid().ToString("D"), System.Security.Cryptography.SHA256.HashData(Guid.NewGuid().ToByteArray()), e, "wh");
        var drained = await DrainAsync(c, provider);
        Assert.True(drained.Count == 1, await DumpAsync(c) + " / " + string.Join(",", drained));
        return drained[0];
    }
    private static async Task<string> DumpAsync(GitDeliverySqlFixture.SqlCase c)
        => string.Join(" | ", (await c.NewContext().Set<GitWebhookDelivery>().AsNoTracking().Where(x => x.IntegrationId == c.Repository.Id).OrderBy(x => x.ReceivedAtUtc).ToListAsync())
            .Select(x => $"{x.DeliveryId}:{x.Status}:{x.LastErrorCode}:{x.NextAttemptAtUtc:O}"));
    private static async Task<Guid?> StageAsync(GitDeliverySqlFixture.SqlCase c)
        => (await c.NewContext().WorkItems.AsNoTracking().SingleAsync(x => x.Id == c.ItemId)).StageId;

    private static ServiceProvider Services(GitDeliverySqlFixture.SqlCase c, FakePollProvider provider)
    {
        var services = new ServiceCollection();
        var options = c.Options();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(c.Clock);
        services.AddScoped<CurrentOrganizationContext>(); services.AddScoped<IOrganizationContext>(sp => sp.GetRequiredService<CurrentOrganizationContext>());
        services.AddScoped<CurrentAuditContext>(); services.AddScoped<IAuditContext>(sp => sp.GetRequiredService<CurrentAuditContext>());
        services.AddScoped(sp => new AppDbContext(options, sp.GetRequiredService<IOrganizationContext>(), sp.GetRequiredService<IAuditContext>()));
        services.AddMethodCore();
        services.AddScoped<IMethodWorkerContextInitializer>(sp => new PollWorkerInitializer(c, sp.GetRequiredService<CurrentOrganizationContext>(),
            sp.GetRequiredService<IMethodContextInitializer>()));
        services.AddScoped<IPermissionService, AllowAll>();
        services.AddScoped<IMethodGitStore>(sp => new EfMethodGitStore(sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<IOrganizationContext>(),
            sp.GetRequiredService<IMethodAccessContext>(), sp.GetRequiredService<IPermissionService>(), c.Clock));
        services.AddSingleton<IGitProvider>(provider);
        services.AddScoped<GitPollService>();
        return services.BuildServiceProvider();
    }
    /// <summary>Identidade de sistema da integração com o canal da correlação, como o inicializador real (sem a delegação humana do banco de teste).</summary>
    private sealed class PollWorkerInitializer(GitDeliverySqlFixture.SqlCase c, CurrentOrganizationContext tenant, IMethodContextInitializer initializer) : IMethodWorkerContextInitializer
    {
        public Task InitializeAsync(Guid integration, Guid? delivery, string correlation, CancellationToken ct = default)
        {
            tenant.Set(c.OrganizationId);
            initializer.Initialize(Worker(c, correlation.StartsWith("poll:", StringComparison.Ordinal) ? MethodChannels.Poll : MethodChannels.Webhook));
            return Task.CompletedTask;
        }
    }

    internal sealed class FakePollProvider : TestGitProvider, IGitProvider
    {
        public long RepositoryId { get; init; }
        public List<GitPullRequestSnapshot> Pulls { get; } = [];
        public List<GitReviewSnapshot> Reviews { get; } = [];
        public HashSet<string> Branches { get; } = new(StringComparer.Ordinal);
        public Exception? Fail { get; init; }
        public TimeSpan Delay { get; init; }
        private int _listCalls;
        public int ListCalls => _listCalls;
        public override Task<GitInstallationValidation> ValidateInstallationAsync(GitRepositoryDescriptor repository, CancellationToken ct = default)
            => Task.FromResult(Fail is null && repository.RepositoryId == RepositoryId ? new GitInstallationValidation(true, null, null)
                : new GitInstallationValidation(false, "git_installation_unavailable", "x"));
        public override async Task<GitPullRequestReadSet> ListPullRequestsAsync(GitRepositoryDescriptor repository, string state, CancellationToken ct = default)
        {
            Interlocked.Increment(ref _listCalls);
            if (Delay > TimeSpan.Zero) await Task.Delay(Delay, ct);
            if (Fail is not null) throw Fail;
            return new(Pulls.Where(pr => state == "all" || pr.State == state).ToArray(), true);
        }
        public override Task<GitPullRequestSnapshot> GetPullRequestAsync(GitRepositoryDescriptor repository, int number, CancellationToken ct = default)
            => Fail is not null ? throw Fail : Task.FromResult(Pulls.Single(pr => pr.Number == number));
        public override Task<IReadOnlyList<GitReviewSnapshot>> GetReviewsAsync(GitRepositoryDescriptor repository, int number, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<GitReviewSnapshot>>(Reviews.ToArray());
        public Task<bool> BranchExistsAsync(GitRepositoryDescriptor repository, string branch, CancellationToken ct = default)
            => Fail is not null ? throw Fail : Task.FromResult(Branches.Contains(branch));
        public Task<GitRepositoryIdentity> GetRepositoryIdentityAsync(GitRepositoryDescriptor repository, CancellationToken ct = default)
            => Fail is not null ? throw Fail : Task.FromResult(new GitRepositoryIdentity(RepositoryId, repository.RepositoryFullName, "main"));
    }
    private sealed class ListLogger : ILogger<GitPollService>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Add(formatter(state, exception) + (exception?.ToString() ?? string.Empty));
    }
    private sealed class Names : IUserDirectory
    {
        public Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(IEnumerable<string> userIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(userIds.ToDictionary(x => x, _ => "Humano SQL"));
        public Task<IReadOnlyList<UserSummary>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<UserSummary>>([]);
        public Task<IReadOnlyList<UserSummary>> GetByIdsAsync(IEnumerable<string> userIds, bool includeInactive = false, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<UserSummary>>([]);
        public Task<UserSummary?> GetByIdAsync(string userId, CancellationToken ct = default) => Task.FromResult<UserSummary?>(null);
        public Task<UserSummary?> GetByIdUnscopedAsync(string userId, CancellationToken ct = default) => Task.FromResult<UserSummary?>(null);
        public Task<UserSummary?> GetByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult<UserSummary?>(null);
    }
    private sealed class AllowAll : IPermissionService
    {
        public Task<bool> HasAsync(string actor, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? id = null, CancellationToken ct = default) => Task.FromResult(true);
        public Task EnsureAsync(string actor, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? id = null, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class SilentRealtime : IBoardRealtimeNotifier
    { public Task BoardChangedAsync(Guid boardId, string changeType, Guid? workItemId = null, CancellationToken ct = default) => Task.CompletedTask; }
    private sealed class MethodOn : Prisma.Workspace.Application.Features.MethodSecurity.IMethodFeatureConfiguration { public bool Enabled => true; }
    private sealed class Tenant(Guid id) : IOrganizationContext { public Guid? OrganizationId => id; }
}
