using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Repositories;
using Prisma.Workspace.Application.Features.MethodGit.Webhooks;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Repositories;
using Prisma.Workspace.Infrastructure.Services.MethodGit;
using Prisma.Workspace.Infrastructure.Services.MethodSecurity;
using Xunit;

namespace Prisma.Workspace.Tests;

/// <summary>HU-0007 (T-0026 e T-0027) no SQL isolado: rascunho, abrir PR, movimento humano e deduplicação com o webhook.</summary>
[Collection("MethodGit SQL")]
public sealed class MethodPullRequestSqlTests : IClassFixture<GitDeliverySqlFixture>
{
    private const string Branch = "task/T-0001-abrir-pr";
    private readonly GitDeliverySqlFixture _fixture;
    public MethodPullRequestSqlTests(GitDeliverySqlFixture fixture) => _fixture = fixture;

    // ---------- T-0026 · rascunho ----------

    [MethodGitSqlFact]
    public async Task CA2_agent_saves_draft_versions_same_text_is_idempotent_and_get_returns_the_last()
    {
        await using var c = await _fixture.CaseAsync();
        var agent = await c.UseAgentAsync();
        var drafts = Drafts(c, c.Context);
        var missing = await Assert.ThrowsAsync<MethodOperationException>(() => drafts.GetAsync(c.ProjectId, "T-0001", agent.ActorId, default));
        Assert.Equal(404, missing.StatusCode); Assert.Equal("pr_draft_not_found", missing.Code);
        var first = await drafts.SaveAsync(c.ProjectId, "T-0001", new("Corrige consulta", "Corpo 1"), agent.ActorId, default);
        Assert.True(first.Changed);
        c.Clock.Now = c.Now.AddSeconds(1);
        Assert.False((await drafts.SaveAsync(c.ProjectId, "T-0001", new("Corrige consulta", "Corpo 1"), agent.ActorId, default)).Changed);
        c.Clock.Now = c.Now.AddSeconds(2);
        await drafts.SaveAsync(c.ProjectId, "T-0001", new("Corrige consulta v2", "Corpo 2"), agent.ActorId, default);
        var current = await drafts.GetAsync(c.ProjectId, "T-0001", agent.ActorId, default);
        Assert.Equal(("Corrige consulta v2", "Corpo 2", "Agente SQL"), (current.Title, current.Body, current.ActorName));
        c.Context.ChangeTracker.Clear();
        var events = await c.Context.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == c.ItemId && x.Kind == EfMethodGitStore.PullRequestDraftSavedEvent).ToListAsync();
        Assert.Equal(2, events.Count);
        Assert.All(events, e => Assert.Equal(agent.ActorId, e.ActorId));
        using var payload = JsonDocument.Parse(events.OrderBy(x => x.CreatedAt).First().Payload!);
        Assert.Equal("Corpo 1", payload.RootElement.GetProperty("body").GetString());
        Assert.Equal(agent.PrincipalId, payload.RootElement.GetProperty("principalId").GetGuid());
        Assert.Equal("mcp", payload.RootElement.GetProperty("channel").GetString());
        Assert.DoesNotContain(payload.RootElement.EnumerateObject(), p => p.Name.Contains("token", StringComparison.OrdinalIgnoreCase));
    }

    [MethodGitSqlFact]
    public async Task CA2_task_of_another_project_or_organization_is_refused_without_writing()
    {
        await using var c = await _fixture.CaseAsync(); await using var other = await _fixture.CaseAsync();
        var agent = await other.UseAgentAsync();
        c.UseActor(agent);
        var error = await Assert.ThrowsAsync<MethodOperationException>(() =>
            Drafts(c, c.Context).SaveAsync(c.ProjectId, "T-0001", new("Título", "Corpo"), agent.ActorId, default));
        Assert.Equal(403, error.StatusCode);
        // Na própria organização do token, o ID do método só existe lá: nada é gravado no caso alheio.
        c.Context.ChangeTracker.Clear();
        Assert.Empty(await c.Context.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == c.ItemId && x.Kind == EfMethodGitStore.PullRequestDraftSavedEvent).ToListAsync());
    }

    // ---------- T-0027 · abrir PR ----------

    [MethodGitSqlFact]
    public async Task CA5_CA6_open_links_explicitly_fills_pr_field_records_event_and_moves_as_the_person_and_later_webhook_changes_nothing()
    {
        await using var c = await _fixture.CaseAsync();
        var link = await RemoteBranchAsync(c);
        var provider = Provider(c);
        var ui = c.Actor!;
        var before = await StageChangesAsync(c);
        c.Clock.Now = c.Now.AddSeconds(2);
        await using var work = c.MethodContext();
        var result = await Opening(c, work, provider).OpenAsync(c.ProjectId, "T-0001", new(link.Id, "Corrige X", "Corpo"), ui.ActorId, default);
        Assert.Equal((MethodPullRequestOutcomes.Opened, true), (result.Outcome, result.Moved));
        var created = Assert.Single(provider.Created);
        Assert.Equal(("[T-0001] Corrige X", Branch, "main"), (created.Title, created.Head, created.Base));
        Assert.EndsWith($"Task no Prisma: https://prisma.example.invalid/projects/{c.ProjectId:D}/backlog?item={c.ItemId:D}", created.Body);
        await using (var read = c.NewContext())
        {
            Assert.Equal(c.InReviewStageId, (await read.WorkItems.AsNoTracking().SingleAsync(x => x.Id == c.ItemId)).StageId);
            var prLink = Assert.Single(await read.Set<TaskGitLink>().AsNoTracking().Include(x => x.Observation).Where(x => x.WorkItemId == c.ItemId && x.PullRequestObservationId != null).ToListAsync());
            Assert.Equal("explicit", prLink.Source); Assert.Equal(ui.ActorId, prLink.CreatedByUserId); Assert.Equal(result.Number, prLink.Observation!.PullRequestNumber);
            Assert.Equal(result.Url, await read.Set<WorkItemCustomFieldValue>().Where(x => x.WorkItemId == c.ItemId && x.FieldDefinitionId == c.PrFieldId).Select(x => x.Value).SingleAsync());
            var opened = Assert.Single(await read.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == c.ItemId && x.Kind == EfMethodGitStore.PullRequestOpenedEvent).ToListAsync());
            Assert.Equal(ui.ActorId, opened.ActorId);
            using var payload = JsonDocument.Parse(opened.Payload!);
            Assert.Equal(result.Number, payload.RootElement.GetProperty("number").GetInt32());
            Assert.False(payload.RootElement.GetProperty("draft").GetBoolean());
            Assert.True(payload.RootElement.GetProperty("moved").GetBoolean());
            Assert.Equal("ui", payload.RootElement.GetProperty("channel").GetString());
            Assert.False(payload.RootElement.TryGetProperty("body", out _));
            var move = Assert.Single(await read.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == c.ItemId && x.Kind == "stage_changed").ToListAsync(), x => !before.Contains(x.Id));
            Assert.Equal(ui.ActorId, move.ActorId);
        }
        // O webhook "opened" que chega depois e a reavaliação "link:" não criam vínculo nem movimento novos.
        var codes = await ProcessPendingAsync(c, provider, includeOpenedWebhook: result.Number);
        Assert.NotEmpty(codes);
        Assert.DoesNotContain("moved", codes);
        await using var after = c.NewContext();
        Assert.Single(await after.Set<TaskGitLink>().AsNoTracking().Where(x => x.WorkItemId == c.ItemId && x.PullRequestObservationId != null).ToListAsync());
        Assert.Equal(before.Count + 1, (await StageChangesAsync(c)).Count);
        Assert.Equal(c.InReviewStageId, (await after.WorkItems.AsNoTracking().SingleAsync(x => x.Id == c.ItemId)).StageId);
    }

    [MethodGitSqlFact]
    public async Task CA6_webhook_processed_before_the_link_moves_as_system_and_the_endpoint_only_promotes_and_records()
    {
        await using var c = await _fixture.CaseAsync();
        var link = await RemoteBranchAsync(c);
        var provider = Provider(c);
        var ui = c.Actor!;
        var before = await StageChangesAsync(c);
        string? webhookCode = null;
        provider.AfterCreate = async pr =>
        {
            c.UseActor(Worker(c));
            webhookCode = string.Join(',', await ProcessPendingAsync(c, provider, includeOpenedWebhook: pr.Number));
            c.UseActor(ui);
        };
        c.Clock.Now = c.Now.AddSeconds(2);
        await using var work = c.MethodContext();
        var result = await Opening(c, work, provider).OpenAsync(c.ProjectId, "T-0001", new(link.Id, "Corrige X", "Corpo"), ui.ActorId, default);
        Assert.Equal("moved", webhookCode);
        Assert.False(result.Moved); Assert.Equal(MethodPullRequestOutcomes.Opened, result.Outcome);
        await using var read = c.NewContext();
        // O card andou só pelo webhook (sistema): o endpoint não registrou movimento da pessoa; o vínculo inferido virou explícito.
        Assert.DoesNotContain(await read.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == c.ItemId && x.Kind == "stage_changed").ToListAsync(),
            x => !before.Contains(x.Id) && x.ActorId == ui.ActorId);
        var prLink = Assert.Single(await read.Set<TaskGitLink>().AsNoTracking().Where(x => x.WorkItemId == c.ItemId && x.PullRequestObservationId != null).ToListAsync());
        Assert.Equal("explicit", prLink.Source);
        Assert.Single(await read.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == c.ItemId && x.Kind == EfMethodGitStore.PullRequestOpenedEvent).ToListAsync());
        Assert.Equal(c.InReviewStageId, (await read.WorkItems.AsNoTracking().SingleAsync(x => x.Id == c.ItemId)).StageId);
    }

    [MethodGitSqlFact]
    public async Task CA6_draft_pr_does_not_move_and_CA7_existing_pr_is_linked_without_post()
    {
        await using var c = await _fixture.CaseAsync();
        var link = await RemoteBranchAsync(c);
        var provider = Provider(c);
        provider.OpenPullRequests.Add(PullRequestTestProvider.Pr(41, Branch, true) with { HeadRepositoryId = c.Repository.RepositoryId, UpdatedAtUtc = c.Now, OpenedAtUtc = c.Now });
        c.Clock.Now = c.Now.AddSeconds(2);
        await using var work = c.MethodContext();
        var result = await Opening(c, work, provider).OpenAsync(c.ProjectId, "T-0001", new(link.Id, "Corrige X", "Corpo", Draft: true), c.Actor!.ActorId, default);
        Assert.Empty(provider.Created);
        Assert.Equal((MethodPullRequestOutcomes.LinkedExisting, 41, false), (result.Outcome, result.Number, result.Moved));
        Assert.StartsWith("Já havia um PR, vinculei o #41", result.Message);
        await using var read = c.NewContext();
        Assert.Equal(c.InDevelopmentStageId, (await read.WorkItems.AsNoTracking().SingleAsync(x => x.Id == c.ItemId)).StageId);
        Assert.Single(await read.Set<TaskGitLink>().AsNoTracking().Where(x => x.WorkItemId == c.ItemId && x.PullRequestObservationId != null && x.Source == "explicit").ToListAsync());
    }

    [MethodGitSqlFact]
    public async Task CA10_github_refusal_writes_nothing_and_keeps_the_column()
    {
        await using var c = await _fixture.CaseAsync();
        var link = await RemoteBranchAsync(c);
        var provider = Provider(c);
        provider.CreateFailure = GitPullRequestErrorCodes.NothingToCompare;
        await using var counts = c.NewContext();
        async Task<(int Events, int Links, int Observations)> CountAsync() => (
            await counts.TaskEvents.AsNoTracking().CountAsync(x => x.WorkItemId == c.ItemId),
            await counts.Set<TaskGitLink>().AsNoTracking().CountAsync(x => x.WorkItemId == c.ItemId),
            await counts.Set<GitPullRequestObservation>().IgnoreQueryFilters().AsNoTracking().CountAsync(x => x.IntegrationId == c.Repository.Id));
        var before = await CountAsync();
        await using var work = c.MethodContext();
        var error = await Assert.ThrowsAsync<MethodOperationException>(() =>
            Opening(c, work, provider).OpenAsync(c.ProjectId, "T-0001", new(link.Id, "Corrige X", "Corpo"), c.Actor!.ActorId, default));
        Assert.Equal(GitPullRequestErrorCodes.NothingToCompare, error.Code);
        Assert.Equal($"Não há diferença entre {Branch} e main: nada para abrir no PR.", error.Message);
        Assert.Equal(before, await CountAsync());
        Assert.Equal(c.InDevelopmentStageId, (await counts.WorkItems.AsNoTracking().SingleAsync(x => x.Id == c.ItemId)).StageId);
    }

    [MethodGitSqlFact]
    public async Task CA8_method_token_is_refused_with_interactive_pr_required()
    {
        await using var c = await _fixture.CaseAsync();
        var link = await RemoteBranchAsync(c);
        var agent = await c.UseAgentAsync();
        var provider = Provider(c);
        await using var work = c.MethodContext();
        var error = await Assert.ThrowsAsync<MethodOperationException>(() =>
            Opening(c, work, provider).OpenAsync(c.ProjectId, "T-0001", new(link.Id, "Corrige X", "Corpo"), agent.ActorId, default));
        Assert.Equal((403, "interactive_pr_required"), (error.StatusCode, error.Code));
        Assert.Equal(0, provider.Finds); Assert.Empty(provider.Created);
    }

    // ---------- montagem ----------

    private static PullRequestTestProvider Provider(GitDeliverySqlFixture.SqlCase c)
        => new() { HeadRepositoryId = c.Repository.RepositoryId, Time = c.Now.AddSeconds(1) };

    /// <summary>Branch da task vinculada pela pessoa e já vista no GitHub (BranchState = remote).</summary>
    private static async Task<TaskGitLinkDto> RemoteBranchAsync(GitDeliverySqlFixture.SqlCase c)
    {
        var link = await c.Store(c.Context).LinkBranchAsync(c.Repository.Id, c.ItemId, Branch);
        await c.Context.Set<TaskGitLink>().Where(x => x.Id == link.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.BranchState, GitBranchStates.Remote).SetProperty(x => x.LastSeenAtUtc, c.Now));
        c.Context.ChangeTracker.Clear();
        return link;
    }

    private static GitPullRequestDrafts Drafts(GitDeliverySqlFixture.SqlCase c, AppDbContext db)
    {
        var store = c.Store(db); var provider = new TestGitProvider(); var permissions = new AllowAll();
        var projects = new GitRepositoryOperations(store, provider, new TestGitSecrets(), c.Access, permissions, c.Clock);
        return new(store, projects, new GitPullRequestOperations(store, provider, projects, c.Access, permissions), c.Access, permissions);
    }

    private static GitPullRequestOpening Opening(GitDeliverySqlFixture.SqlCase c, AppDbContext db, PullRequestTestProvider provider)
    {
        var store = c.Store(db); var permissions = new AllowAll();
        var projects = new GitRepositoryOperations(store, provider, new TestGitSecrets(), c.Access, permissions, c.Clock);
        var movement = new MethodMoveService(db, c.Access, store, new WorkflowRepository(db), permissions, new SilentRealtime(),
            NullLogger<MethodMoveService>.Instance, c.Clock);
        return new(store, provider, projects, new GitPullRequestOperations(store, provider, projects, c.Access, permissions), c.Access, permissions,
            movement, new PullRequestTestCardLinks(), new PullRequestTestAutomation(), c.Clock);
    }

    private static async Task<HashSet<Guid>> StageChangesAsync(GitDeliverySqlFixture.SqlCase c)
    {
        await using var db = c.NewContext();
        return (await db.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == c.ItemId && x.Kind == "stage_changed").Select(x => x.Id).ToListAsync()).ToHashSet();
    }

    /// <summary>Processa, como worker, o webhook "opened" (opcional) e tudo o que estiver na fila da integração (ex.: reavaliação link:).</summary>
    private static async Task<List<string>> ProcessPendingAsync(GitDeliverySqlFixture.SqlCase c, PullRequestTestProvider provider, int? includeOpenedWebhook = null)
    {
        var previous = c.Actor!;
        c.UseActor(Worker(c));
        try
        {
            var codes = new List<string>();
            // Worker como nos demais testes SQL do método Git (sem a guarda de escrita, que exige a concessão real do principal de sistema).
            await using var db = c.NewContext();
            var store = c.Store(db);
            if (includeOpenedWebhook is { } number)
            {
                var pr = provider.OpenPullRequests.Single(x => x.Number == number);
                var e = new NormalizedGitEvent("github", c.Repository.RepositoryId, c.Repository.InstallationId, "pull_request", "opened", pr.UpdatedAtUtc,
                    $"rev-opened-{number}", null, number, pr.Url, pr.Title, pr.HeadBranch, pr.BaseBranch, pr.HeadSha, null, "open", pr.IsDraft,
                    HeadRepositoryId: c.Repository.RepositoryId);
                await store.AcceptDeliveryAsync(c.Repository.Id, Guid.NewGuid().ToString("D"), SHA256.HashData(Guid.NewGuid().ToByteArray()), e, "sql-correlation");
            }
            while (await store.ClaimNextDeliveryAsync(c.Clock.Now.AddMinutes(10), TimeSpan.FromMinutes(5)) is { } lease)
            {
                db.ChangeTracker.Clear();
                var movement = new MethodMoveService(db, c.Access, store, new WorkflowRepository(db), new AllowAll(), new SilentRealtime(),
                    NullLogger<MethodMoveService>.Instance, c.Clock);
                var processor = new GitDeliveryProcessor(store, provider, new GitTaskResolver(store), movement, NullLogger<GitDeliveryProcessor>.Instance, c.Clock);
                var outcome = await processor.ProcessAsync((await store.GetDeliveryAsync(lease.Id))!);
                codes.Add(outcome.Code);
            }
            return codes;
        }
        finally { c.UseActor(previous); }
    }

    private static MethodActorIdentity Worker(GitDeliverySqlFixture.SqlCase c) => new(MethodAuthenticationKind.IntegrationWorker, MethodRole.System,
        c.Repository.SystemPrincipalId, $"method:{c.Repository.SystemPrincipalId:D}", "Integração SQL", c.Repository.OrganizationId, c.ProjectId, null, null,
        Enum.GetValues<PlatformPermission>().ToHashSet(), MethodChannels.Webhook, "sql-correlation");

    private sealed class AllowAll : IPermissionService
    {
        public Task<bool> HasAsync(string actor, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? id = null, CancellationToken ct = default) => Task.FromResult(true);
        public Task EnsureAsync(string actor, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? id = null, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class SilentRealtime : IBoardRealtimeNotifier
    { public Task BoardChangedAsync(Guid boardId, string changeType, Guid? workItemId = null, CancellationToken ct = default) => Task.CompletedTask; }
}
