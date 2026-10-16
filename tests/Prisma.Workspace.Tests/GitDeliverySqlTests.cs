using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Services.MethodGit;
using Prisma.Workspace.Infrastructure.Tenancy;
using Xunit;

namespace Prisma.Workspace.Tests;

[CollectionDefinition("MethodGit SQL", DisableParallelization = true)]
public sealed class MethodGitSqlCollection { }
public sealed class MethodGitSqlFactAttribute : FactAttribute
{
    public MethodGitSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PRISMA_METHOD_GIT_SQL")))
            Skip = "Defina PRISMA_METHOD_GIT_SQL para executar no SQL isolado3132; nunca usa ambiente implícito.";
    }
}

[Collection("MethodGit SQL")]
public class GitDeliverySqlTests : IClassFixture<GitDeliverySqlFixture>
{
    private readonly GitDeliverySqlFixture _fixture;
    public GitDeliverySqlTests(GitDeliverySqlFixture fixture) => _fixture = fixture;
    [MethodGitSqlFact]
    public async Task Concurrent_delivery_is_unique_and_conflicting_hash_does_not_overwrite()
    {
        await using var c = await _fixture.CaseAsync();
        var id = Guid.NewGuid().ToString("D"); var hash = SHA256.HashData("original"u8);
        await using var first = c.NewContext(); await using var second = c.NewContext();
        var receipts = await Task.WhenAll(c.Store(first).AcceptDeliveryAsync(c.Repository.Id, id, hash, c.Event, "sql-correlation"),
            c.Store(second).AcceptDeliveryAsync(c.Repository.Id, id, hash, c.Event, "sql-correlation"));
        Assert.Single(receipts, r => !r.Duplicate); Assert.Single(receipts, r => r.Duplicate); Assert.All(receipts, r => Assert.False(r.Conflict));
        var changed = SHA256.HashData("changed"u8);
        var conflict = await c.Store(c.Context).AcceptDeliveryAsync(c.Repository.Id, id, changed, c.Event, "sql-correlation");
        Assert.True(conflict.Conflict);
        var row = Assert.Single(await c.Context.Set<GitWebhookDelivery>().IgnoreQueryFilters().AsNoTracking().Where(x => x.IntegrationId == c.Repository.Id).ToListAsync());
        Assert.Equal(hash, row.BodyHash); Assert.Equal(GitDeliveryStates.Received, row.Status);
    }
    [MethodGitSqlFact]
    public async Task Lease_reclaim_changes_rowversion_and_old_worker_cannot_complete()
    {
        await using var c = await _fixture.CaseAsync(); var store = c.Store(c.Context);
        await store.AcceptDeliveryAsync(c.Repository.Id, Guid.NewGuid().ToString("D"), SHA256.HashData("lease"u8), c.Event, "sql-correlation");
        var old = await store.ClaimNextDeliveryAsync(c.Now, TimeSpan.FromSeconds(1)); Assert.NotNull(old);
        Assert.Null(await store.ClaimNextDeliveryAsync(c.Now, TimeSpan.FromMinutes(1)));
        c.Clock.Now = c.Now.AddSeconds(2);
        var next = await store.ClaimNextDeliveryAsync(c.Clock.Now, TimeSpan.FromMinutes(1)); Assert.NotNull(next);
        Assert.Equal(old!.Id, next!.Id); Assert.NotEqual(Convert.ToHexString(old.RowVersion), Convert.ToHexString(next.RowVersion));
        Assert.False(await store.CompleteDeliveryAsync(old.Id, old.RowVersion, GitDeliveryStates.Processed, null, null));
        Assert.True(await store.CompleteDeliveryAsync(next.Id, next.RowVersion, GitDeliveryStates.Processed, null, null));
        Assert.Equal(2, (await c.Context.Set<GitWebhookDelivery>().IgnoreQueryFilters().AsNoTracking().SingleAsync(x => x.Id == next.Id)).Attempts);
    }
    [MethodGitSqlFact]
    public async Task Link_field_comment_and_evaluation_inbox_rollback_together_and_retry_is_idempotent()
    {
        await using var c = await _fixture.CaseAsync();
        var pr = new GitPullRequestSnapshot(7, "https://github.com/qa/metodo/pull/7", "Legado", "feature/livre", "main", new string('a', 40), null, "open", false, false, c.Now, c.Now);
        var observation = await c.Store(c.Context).UpsertObservationAsync(c.Repository.Id, pr, "legacy_confirmation_required");
        c.Failure.Enabled = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => c.Store(c.Context).LinkExplicitAsync(c.Repository.Id, c.ItemId, observation.Id, "Confirmação humana."));
        c.Context.ChangeTracker.Clear();
        Assert.Empty(await c.Context.Set<TaskGitLink>().ToListAsync());
        Assert.Empty(await c.Context.Comments.Where(x => x.WorkItemId == c.ItemId).ToListAsync());
        Assert.Empty(await c.Context.Set<GitWebhookDelivery>().IgnoreQueryFilters().Where(x => x.IntegrationId == c.Repository.Id).ToListAsync());
        Assert.Equal("original preservado", await c.Context.Set<WorkItemCustomFieldValue>().Where(x => x.WorkItemId == c.ItemId && x.FieldDefinitionId == c.PrFieldId).Select(x => x.Value).SingleAsync());
        var link = await c.Store(c.Context).LinkExplicitAsync(c.Repository.Id, c.ItemId, observation.Id, "Confirmação humana.");
        await c.Store(c.Context).LinkExplicitAsync(c.Repository.Id, c.ItemId, observation.Id, "Repetição.");
        Assert.Equal(c.ItemId, link.WorkItemId);
        Assert.Single(await c.Context.Set<TaskGitLink>().ToListAsync()); Assert.Single(await c.Context.Comments.Where(x => x.WorkItemId == c.ItemId).ToListAsync());
        var queue = Assert.Single(await c.Context.Set<GitWebhookDelivery>().IgnoreQueryFilters().Where(x => x.IntegrationId == c.Repository.Id).ToListAsync());
        Assert.StartsWith("link:", queue.DeliveryId); Assert.Equal(GitDeliveryProcessor.LinkEvaluationEvent, queue.EventName);
        Assert.Single(await c.Context.Set<MethodAuthorizationEvent>().Where(x => x.WorkItemId == c.ItemId).ToListAsync());
    }
    [MethodGitSqlFact]
    public async Task Invalid_column_category_suspends_automation_without_repairing_human_change()
    {
        await using var c = await _fixture.CaseAsync(); var store = c.Store(c.Context);
        var before = await store.GetProjectAsync(c.ProjectId); Assert.True(before!.Enabled);
        var stageId = before.StageMappings[MethodStatusCodes.InDevelopment];
        var stage = await c.Context.Stages.SingleAsync(x => x.Id == stageId); stage.Category = StageCategory.Done;
        await c.Context.SaveChangesAsync();
        Assert.False((await store.GetProjectAsync(c.ProjectId))!.Enabled);
        Assert.Equal(StageCategory.Done, (await c.Context.Stages.AsNoTracking().SingleAsync(x => x.Id == stageId)).Category);
        Assert.True((await c.Context.Set<MethodProjectSettings>().AsNoTracking().SingleAsync(x => x.ProjectId == c.ProjectId)).Enabled);
    }
    [MethodGitSqlFact]
    public async Task Human_user_id_with_method_token_is_manual_but_not_interactive_authorization()
    {
        await using var c = await _fixture.CaseAsync();
        var user = (await c.Context.Set<MethodAuthorizationEvent>().SingleAsync(x => x.WorkItemId == c.ItemId)).ActorUserId!;
        c.Context.Add(new TaskEvent { Id = Guid.NewGuid(), WorkItemId = c.ItemId, ActorId = user, Kind = "stage_changed", CreatedAt = c.Now,
            Payload = "{\"authenticationKind\":\"methodToken\",\"fromStatus\":\"em-revisao\",\"toStatus\":\"em-desenvolvimento\"}" });
        await c.Context.SaveChangesAsync();
        var task = await c.Store(c.Context).GetTaskAsync(c.ItemId); Assert.NotNull(task);
        Assert.Equal(c.Now.AddMinutes(-5), task!.LastInteractiveChangeAtUtc); Assert.Equal(c.Now, task.LastManualChangeAtUtc);
    }
    [MethodGitSqlFact]
    public async Task Direct_observation_lookup_is_project_scoped_and_link_records_Git_author_separately()
    {
        await using var c = await _fixture.CaseAsync(); var limiter = new GitWebhookIngressLimiter(); var store = c.Store(c.Context, limiter);
        var pr = new GitPullRequestSnapshot(7, "https://github.com/qa/metodo/pull/7", "Autor externo", "feature/livre", "main", new string('a', 40), null,
            "open", false, false, c.Now, c.Now, HeadRepositoryId: 777777, PullRequestAuthorId: "42", PullRequestAuthorName: "outro-autor-git");
        var observation = await store.UpsertObservationAsync(c.Repository.Id, pr, "head_repository_requires_explicit_link");
        Assert.Null(await store.GetPullRequestObservationAsync(Guid.NewGuid(), observation.Id));
        Assert.Equal(observation.Id, (await store.GetPullRequestObservationAsync(c.ProjectId, observation.Id))!.Id);
        await store.LinkExplicitAsync(c.Repository.Id, c.ItemId, observation.Id, "Confirmação humana do fork.");
        var entry = await c.Context.TaskEvents.SingleAsync(x => x.WorkItemId == c.ItemId && x.Kind == "git_pr_linked");
        using var payload = System.Text.Json.JsonDocument.Parse(entry.Payload!);
        Assert.Equal("42", payload.RootElement.GetProperty("gitPullRequestAuthorId").GetString());
        Assert.Equal("outro-autor-git", payload.RootElement.GetProperty("gitPullRequestAuthorName").GetString());
        Assert.Equal("uiSession", payload.RootElement.GetProperty("authenticationKind").GetString());
        Assert.NotEqual("42", entry.ActorId);
        for (var i = 0; i < 61; i++) limiter.TryConsumeInvalid(c.Repository.Id, c.Now);
        Assert.Contains("429", (await store.GetRepositoryAsync(c.Repository.Id))!.OperationalError);
        Assert.Contains("reenvie manualmente", (await store.GetRepositoryAsync(c.Repository.Id))!.OperationalError);
    }
    [MethodGitSqlFact]
    public async Task Repository_update_rejects_stale_version_and_operational_error_is_safe()
    {
        await using var c = await _fixture.CaseAsync(); var store = c.Store(c.Context);
        var original = await store.GetRepositoryAsync(c.Repository.Id); Assert.NotNull(original);
        await using var other = c.NewContext();
        await c.Store(other).SaveRepositoryAsync(original! with { DefaultBranch = "release" });
        var conflict = await Assert.ThrowsAsync<MethodOperationException>(() => store.SaveRepositoryAsync(original! with { DefaultBranch = "stale" }));
        Assert.Equal(409, conflict.StatusCode);
        c.Context.ChangeTracker.Clear();
        Assert.Equal("release", (await store.GetRepositoryAsync(c.Repository.Id))!.DefaultBranch);
        await store.AcceptDeliveryAsync(c.Repository.Id, Guid.NewGuid().ToString("D"), SHA256.HashData("status"u8), c.Event, "sql-correlation");
        var lease = await store.ClaimNextDeliveryAsync(c.Now, TimeSpan.FromMinutes(1)); Assert.NotNull(lease);
        Assert.True(await store.CompleteDeliveryAsync(lease!.Id, lease.RowVersion, GitDeliveryStates.RetryableFailure,
            "raw-secret-provider-response", c.Now.AddMinutes(1)));
        var safe = (await store.GetRepositoryAsync(c.Repository.Id))!.OperationalError;
        Assert.Equal("Há uma entrega pendente ou com falha. Confira a integração e o histórico do projeto.", safe);
        Assert.Null((await store.GetRepositoryForIngressAsync(c.Repository.Id))!.OperationalError);
    }
    [MethodGitSqlFact]
    public async Task Composite_foreign_key_rejects_link_to_integration_from_other_project()
    {
        await using var c = await _fixture.CaseAsync(); await using var other = await _fixture.CaseAsync();
        c.Context.Add(new TaskGitLink { Id = Guid.NewGuid(), ProjectId = c.ProjectId, IntegrationId = other.Repository.Id,
            WorkItemId = c.ItemId, BranchName = "task/T-0001-x", Source = "branch", CreatedAtUtc = c.Now });
        await Assert.ThrowsAsync<DbUpdateException>(() => c.Context.SaveChangesAsync());
        c.Context.ChangeTracker.Clear(); Assert.Empty(await c.Context.Set<TaskGitLink>().Where(x => x.ProjectId == c.ProjectId).ToListAsync());
    }
}

public sealed class GitDeliverySqlFixture : IAsyncLifetime
{
    private string? _connection;
    public async Task InitializeAsync()
    {
        var value = Environment.GetEnvironmentVariable("PRISMA_METHOD_GIT_SQL"); if (string.IsNullOrWhiteSpace(value)) return;
        var builder = new SqlConnectionStringBuilder(value);
        if (builder.DataSource is not ("127.0.0.1,3132" or "localhost,3132")) throw new InvalidOperationException("SQL do método deve ser o servidor isolado3132.");
        builder.InitialCatalog = "Prisma_MethodGit_Ponte_" + Guid.NewGuid().ToString("N");
        _connection = builder.ConnectionString;
        await using var context = new AppDbContext(Options());
        await context.Database.EnsureCreatedAsync();
    }
    private DbContextOptions<AppDbContext> Options(FailBeforeCommit? failure = null)
    {
        var builder = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(_connection ?? throw new InvalidOperationException("SQL focado não configurado."));
        if (failure is not null) builder.AddInterceptors(failure);
        return builder.Options;
    }
    public async Task<SqlCase> CaseAsync()
    {
        var c = new SqlCase(Options, new FailBeforeCommit()); await c.SeedAsync(); return c;
    }
    public async Task DisposeAsync()
    {
        if (_connection is null) return;
        var target = new SqlConnectionStringBuilder(_connection);
        if (target.DataSource is not ("127.0.0.1,3132" or "localhost,3132") || !target.InitialCatalog.StartsWith("Prisma_MethodGit_Ponte_", StringComparison.Ordinal))
            throw new InvalidOperationException("Limpeza recusada fora do banco descartável próprio.");
        await using var context = new AppDbContext(Options()); await context.Database.EnsureDeletedAsync();
    }
    public sealed class FailBeforeCommit : SaveChangesInterceptor
    {
        public bool Enabled { get; set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (Enabled) { Enabled = false; throw new InvalidOperationException("simulated_failure_before_commit"); }
            return ValueTask.FromResult(result);
        }
    }
    public sealed class SqlClock(DateTimeOffset now) : TimeProvider { public DateTimeOffset Now { get; set; } = now; public override DateTimeOffset GetUtcNow() => Now; }
    private sealed class SqlTenant(Guid id) : IOrganizationContext { public Guid? OrganizationId => id; }
    private sealed class SqlActor : IMethodAccessContext { public MethodActorIdentity? Current { get; set; } }
    private sealed class SqlPermissions : IPermissionService
    {
        public Task<bool> HasAsync(string actor, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? id = null, CancellationToken ct = default) => Task.FromResult(true);
        public Task EnsureAsync(string actor, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? id = null, CancellationToken ct = default) => Task.CompletedTask;
    }
    public sealed class SqlCase : IAsyncDisposable
    {
        private readonly Func<FailBeforeCommit?, DbContextOptions<AppDbContext>> _options;
        private readonly Guid _organization = Guid.NewGuid();
        private readonly string _human = Guid.NewGuid().ToString("D");
        private readonly SqlActor _actor = new();
        private readonly CurrentAuditContext _audit = new();
        public DateTimeOffset Now { get; } = DateTimeOffset.UtcNow;
        public SqlClock Clock { get; }
        public Guid ProjectId { get; private set; }
        public Guid ItemId { get; private set; }
        public Guid PrFieldId { get; private set; }
        public AppDbContext Context { get; }
        public GitRepositoryDescriptor Repository { get; private set; } = null!;
        public FailBeforeCommit Failure { get; }
        public NormalizedGitEvent Event => new("github", Repository.RepositoryId, Repository.InstallationId, "ping", null, Now, "ignored", null, null, null, null, null, null, null, null, null);
        public SqlCase(Func<FailBeforeCommit?, DbContextOptions<AppDbContext>> options, FailBeforeCommit failure)
        {
            _options = options; Failure = failure; Clock = new(Now); _audit.Set(_human, "ui", null, "sql-correlation");
            Context = new(_options(failure), new SqlTenant(_organization), _audit);
        }
        public AppDbContext NewContext() => new(_options(null), new SqlTenant(_organization), _audit);
        public EfMethodGitStore Store(AppDbContext context, GitWebhookIngressLimiter? limiter = null) => new(context, new SqlTenant(_organization), _actor, new SqlPermissions(), Clock, limiter);
        public async Task SeedAsync()
        {
            var org = new Organization { Id = _organization, Name = "Método SQL próprio", Slug = "method-" + _organization.ToString("N"), CreatedAt = Now, UpdatedAt = Now };
            var project = Project.Criar("M" + Guid.NewGuid().ToString("N")[..8], "Método SQL", _human, WorkNature.Project, WorkType.Development); project.OrganizationId = _organization; ProjectId = project.Id;
            var board = new Board { Id = Guid.NewGuid(), OrganizationId = _organization, ProjectId = ProjectId, Name = "Método", OwnerId = _human, CreatedAt = Now };
            var stages = MethodStatusCodes.All.ToDictionary(s => s, s => new Stage { Id = Guid.NewGuid(), ProjectId = ProjectId, BoardId = board.Id,
                Name = s, Category = s == MethodStatusCodes.Completed ? StageCategory.Done : StageCategory.Backlog, CreatedAt = Now });
            var idField = ProjectCustomFieldDefinition.Create(ProjectId, "ID do metodo", CustomFieldType.Text, false, null, 100);
            var huField = ProjectCustomFieldDefinition.Create(ProjectId, "HU", CustomFieldType.Text, false, null, 200);
            var prField = ProjectCustomFieldDefinition.Create(ProjectId, "PR", CustomFieldType.Text, false, null, 300); PrFieldId = prField.Id;
            var settings = new MethodProjectSettings { ProjectId = ProjectId, OrganizationId = _organization, BoardId = board.Id, MethodIdFieldId = idField.Id,
                HuFieldId = huField.Id, PrFieldId = prField.Id, Enabled = true, GitPhase = MethodGitPhase.Webhooks, CreatedAtUtc = Now, UpdatedAtUtc = Now };
            foreach (var pair in stages) settings.StageMappings.Add(new MethodStageMapping { ProjectId = ProjectId, StatusCode = pair.Key, StageId = pair.Value.Id, CreatedAtUtc = Now });
            var principal = new MethodPrincipal { Id = Guid.NewGuid(), OrganizationId = _organization, ProjectId = ProjectId, Role = MethodRole.System,
                DisplayName = "Integração SQL", DelegatedByUserId = _human, PermissionCeilingJson = "[1,3,6,7]", CreatedAtUtc = Now };
            var item = new WorkItem { Id = Guid.NewGuid(), BoardId = board.Id, StageId = stages[MethodStatusCodes.InDevelopment].Id, Title = "T-0001 · SQL", CreatedAt = Now, UpdatedAt = Now }; ItemId = item.Id;
            var state = new MethodTaskState { WorkItemId = ItemId, OrganizationId = _organization, ProjectId = ProjectId, MethodTaskId = "T-0001", CycleId = Guid.NewGuid() };
            var integration = new GitRepositoryIntegration { Id = Guid.NewGuid(), ProjectId = ProjectId, OrganizationId = _organization, Provider = "github", Host = "github.com",
                RepositoryId = Random.Shared.NextInt64(1000000, long.MaxValue), RepositoryFullName = "qa/metodo", DefaultBranch = "main", AppId = 424242, InstallationId = 1,
                SystemPrincipalId = principal.Id, AppPrivateKeySecretRef = "test-app-key", WebhookSecretRef = "test-webhook-secret", Enabled = true, CreatedAtUtc = Now, UpdatedAtUtc = Now };
            _actor.Current = new(MethodAuthenticationKind.UiSession, MethodRole.Human, null, _human, "Humano SQL", _organization, ProjectId, null, null,
                Enum.GetValues<PlatformPermission>().ToHashSet(), "ui", "sql-correlation");
            Context.AddRange(new IdentityUser { Id = _human, UserName = _human }, org, project, board, idField, huField, prField, settings, principal, item, state, integration);
            Context.AddRange(stages.Values);
            Context.AddRange(new WorkItemCustomFieldValue { WorkItemId = ItemId, FieldDefinitionId = idField.Id, Value = "T-0001", UpdatedBy = _human, UpdatedAt = Now },
                new WorkItemCustomFieldValue { WorkItemId = ItemId, FieldDefinitionId = prField.Id, Value = "original preservado", UpdatedBy = _human, UpdatedAt = Now });
            await Context.SaveChangesAsync();
            var grantEvent = new TaskEvent { Id = Guid.NewGuid(), WorkItemId = ItemId, ActorId = _human, Kind = "stage_changed", CreatedAt = Now.AddMinutes(-5),
                Payload = "{\"authenticationKind\":\"uiSession\",\"fromStatus\":\"backlog\",\"toStatus\":\"em-desenvolvimento\"}" };
            var grant = new MethodAuthorizationEvent { Id = Guid.NewGuid(), OrganizationId = _organization, ProjectId = ProjectId, WorkItemId = ItemId, CycleId = state.CycleId,
                EventKind = "Granted", ActorUserId = _human, ActorName = "Humano SQL", TaskEventId = grantEvent.Id, Reason = "Fixture própria", CreatedAtUtc = Now.AddMinutes(-5) };
            Context.AddRange(grantEvent, grant); await Context.SaveChangesAsync(); state.ActiveAuthorizationEventId = grant.Id; await Context.SaveChangesAsync();
            Repository = new(integration.Id, _organization, ProjectId, "github", "github.com", integration.RepositoryId, "qa/metodo", "main", 424242, 1,
                "test-app-key", "test-webhook-secret", null, null, principal.Id, true);
        }
        public async ValueTask DisposeAsync()
        {
            Failure.Enabled = false; Context.ChangeTracker.Clear();
            await Context.Set<GitWebhookDelivery>().IgnoreQueryFilters().Where(x => x.IntegrationId == Repository.Id
                && (x.Status == GitDeliveryStates.Received || x.Status == GitDeliveryStates.RetryableFailure || x.Status == GitDeliveryStates.Processing))
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, GitDeliveryStates.Ignored).SetProperty(x => x.ProcessedAtUtc, DateTimeOffset.UtcNow)
                    .SetProperty(x => x.LeaseUntilUtc, (DateTimeOffset?)null));
            await Context.DisposeAsync();
        }
    }
}
