using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
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

/// <summary>T-0004 (HU-0002): vínculo de branch antes do push, create do webhook e migração única da HU, no SQL isolado 3132.</summary>
[Collection("MethodGit SQL")]
public sealed class MethodGitBranchLinkSqlTests : IClassFixture<GitDeliverySqlFixture>
{
    private const string Branch = "task/T-0001-dados-vinculo";
    private readonly GitDeliverySqlFixture _fixture;
    public MethodGitBranchLinkSqlTests(GitDeliverySqlFixture fixture) => _fixture = fixture;

    [MethodGitSqlFact]
    public async Task CA8_agent_token_links_branch_as_local_visible_in_task_links_and_history()
    {
        await using var c = await _fixture.CaseAsync();
        await c.UseAgentAsync();
        var agent = c.Actor!;
        var link = await Operations(c).LinkBranchAsync(c.ProjectId, "T-0001", new("qa/metodo", Branch), agent.ActorId, default);
        Assert.Equal(GitBranchStates.Local, link.BranchState); Assert.Equal(Branch, link.BranchName);
        Assert.Equal("qa/metodo", link.RepositoryFullName); Assert.Null(link.LastSeenAtUtc); Assert.Null(link.ObservationId);
        Assert.Equal(agent.PrincipalId, link.CreatedByPrincipalId); Assert.Null(link.CreatedByUserId);
        // Repetir o mesmo vínculo é idempotente: nem linha nem evento duplicados.
        await Operations(c).LinkBranchAsync(c.ProjectId, "T-0001", new("QA/Metodo", Branch), agent.ActorId, default);
        c.Context.ChangeTracker.Clear();
        // obter_task usa GetTaskLinksAsync (MethodTaskOperations.GetAsync).
        var stored = Assert.Single(await c.Store(c.Context).GetTaskLinksAsync(c.ItemId));
        Assert.Equal(GitBranchStates.Local, stored.BranchState); Assert.Equal(Branch, stored.BranchName); Assert.Equal("qa/metodo", stored.RepositoryFullName);
        var entry = Assert.Single(await c.Context.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == c.ItemId && x.Kind == "git_branch_linked").ToListAsync());
        Assert.Equal(agent.ActorId, entry.ActorId);
        using var payload = JsonDocument.Parse(entry.Payload!);
        Assert.Equal("agente", payload.RootElement.GetProperty("methodRole").GetString());
        Assert.Equal("methodToken", payload.RootElement.GetProperty("authenticationKind").GetString());
        Assert.Equal(Branch, payload.RootElement.GetProperty("branch").GetString());
        Assert.Equal("local", payload.RootElement.GetProperty("branchState").GetString());
        Assert.Equal("qa/metodo", payload.RootElement.GetProperty("repositoryFullName").GetString());
        // Branch local não move card.
        Assert.Equal(c.InDevelopmentStageId, (await c.Context.WorkItems.AsNoTracking().SingleAsync(x => x.Id == c.ItemId)).StageId);
    }

    [MethodGitSqlFact]
    public async Task CA8_foreign_repository_invalid_name_and_out_of_scope_token_are_refused_without_writing()
    {
        await using var c = await _fixture.CaseAsync(); await using var other = await _fixture.CaseAsync();
        // Repositório cadastrado, mas em outro projeto (mesma organização não importa: é outro tenant aqui).
        await other.Context.Set<GitRepositoryIntegration>().Where(x => x.Id == other.Repository.Id)
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.RepositoryFullName, "qa/alheio"));
        await c.UseAgentAsync();
        var agent = c.Actor!.ActorId;
        async Task Refused(int status, string code, LinkMethodBranchRequest request, string methodId = "T-0001", Guid? project = null)
        {
            var error = await Assert.ThrowsAsync<MethodOperationException>(() =>
                Operations(c).LinkBranchAsync(project ?? c.ProjectId, methodId, request, c.Actor!.ActorId, default));
            Assert.Equal(status, error.StatusCode); Assert.Equal(code, error.Code);
        }
        await Refused(400, "unregistered_git_repository", new("qa/alheio", Branch));
        await Refused(400, "unregistered_git_repository", new("qa/nao-cadastrado", Branch));
        await Refused(400, "invalid_git_repository", new("qa", Branch));
        await Refused(400, "invalid_git_repository", new("qa/../metodo", Branch));
        foreach (var invalid in new[] { "", "com espaço", "a..b", "x~1", "x^1", "x:1", "x?", "x*", "x[1", "a\\b", "-x", "/x", "x/", "a//b",
                     "ref.lock", "x/.oculta", "fim.", "@", "HEAD", "a@{1}", "tab\tx", new string('a', 256) })
            await Refused(400, "invalid_git_branch", new("qa/metodo", invalid));
        Assert.True(GitBranchName.IsValid(new string('a', 255)));
        // Token de agente de outro projeto não alcança este projeto.
        c.UseActor(await other.UseAgentAsync());
        await Refused(403, "git_project_permission", new("qa/metodo", Branch));
        // Principal de sistema (integração) não confirma vínculo.
        c.UseActor(c.Actor! with { Role = MethodRole.System, ProjectId = c.ProjectId });
        await Refused(403, "git_link_permission", new("qa/metodo", Branch));
        // Integração desabilitada deixa de ser repositório do projeto.
        await c.UseAgentAsync();
        await c.Context.Set<GitRepositoryIntegration>().Where(x => x.Id == c.Repository.Id).ExecuteUpdateAsync(set => set.SetProperty(x => x.Enabled, false));
        await Refused(400, "unregistered_git_repository", new("qa/metodo", Branch));
        await Refused(400, "invalid_method_task_id", new("qa/metodo", Branch), "X-1");
        c.Context.ChangeTracker.Clear();
        Assert.Empty(await c.Context.Set<TaskGitLink>().AsNoTracking().Where(x => x.ProjectId == c.ProjectId || x.ProjectId == other.ProjectId).ToListAsync());
        Assert.Empty(await c.Context.TaskEvents.AsNoTracking().Where(x => x.Kind == "git_branch_linked" && (x.WorkItemId == c.ItemId || x.WorkItemId == other.ItemId)).ToListAsync());
        Assert.NotEqual(agent, c.Actor!.ActorId);
    }

    [MethodGitSqlFact]
    public async Task CA8_branch_linked_to_another_task_is_conflict_and_keeps_original()
    {
        await using var c = await _fixture.CaseAsync();
        var second = await c.AddTaskAsync("T-0002");
        await c.UseAgentAsync();
        const string shared = "feature/compartilhada";
        await Operations(c).LinkBranchAsync(c.ProjectId, "T-0001", new("qa/metodo", shared), c.Actor!.ActorId, default);
        var error = await Assert.ThrowsAsync<MethodOperationException>(() =>
            Operations(c).LinkBranchAsync(c.ProjectId, "T-0002", new("qa/metodo", shared), c.Actor!.ActorId, default));
        Assert.Equal(409, error.StatusCode); Assert.Equal("git_link_conflict", error.Code);
        c.Context.ChangeTracker.Clear();
        var link = Assert.Single(await c.Context.Set<TaskGitLink>().AsNoTracking().Where(x => x.ProjectId == c.ProjectId).ToListAsync());
        Assert.Equal(c.ItemId, link.WorkItemId);
        Assert.Empty(await c.Context.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == second && x.Kind == "git_branch_linked").ToListAsync());
    }

    [MethodGitSqlFact]
    public async Task CA8_task_changed_after_validation_is_concurrency_conflict_without_writing()
    {
        await using var c = await _fixture.CaseAsync();
        await c.UseAgentAsync();
        // Entre a validação do ID (ReferencesOtherTask) e a gravação, uma sessão humana troca o ID da task.
        var permissions = new ChangeTaskOnFirstCheck(async () =>
        {
            await using var human = c.NewContext();
            await human.Set<MethodTaskState>().Where(x => x.WorkItemId == c.ItemId).ExecuteUpdateAsync(set => set.SetProperty(x => x.MethodTaskId, "T-0002"));
        });
        var store = new EfMethodGitStore(c.Context, new Tenant(c.Repository.OrganizationId), c.Access, permissions, c.Clock);
        var error = await Assert.ThrowsAsync<MethodOperationException>(() => store.LinkBranchAsync(c.Repository.Id, c.ItemId, Branch));
        Assert.Equal(409, error.StatusCode); Assert.Equal("git_concurrency", error.Code);
        c.Context.ChangeTracker.Clear();
        Assert.Empty(await c.Context.Set<TaskGitLink>().AsNoTracking().Where(x => x.ProjectId == c.ProjectId).ToListAsync());
        Assert.Empty(await c.Context.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == c.ItemId && x.Kind == "git_branch_linked").ToListAsync());
    }

    [MethodGitSqlFact]
    public async Task CA8_branch_names_differing_only_by_case_are_distinct_links()
    {
        await using var c = await _fixture.CaseAsync();
        var second = await c.AddTaskAsync("T-0002");
        await c.UseAgentAsync();
        await Operations(c).LinkBranchAsync(c.ProjectId, "T-0001", new("qa/metodo", "feature/Foo"), c.Actor!.ActorId, default);
        // Refs do Git diferenciam maiúsculas: feature/foo é outra branch e pode ir para outra task (sem 409).
        var other = await Operations(c).LinkBranchAsync(c.ProjectId, "T-0002", new("qa/metodo", "feature/foo"), c.Actor!.ActorId, default);
        Assert.Equal("feature/foo", other.BranchName);
        c.Context.ChangeTracker.Clear();
        var links = await c.Context.Set<TaskGitLink>().AsNoTracking().Where(x => x.ProjectId == c.ProjectId).ToListAsync();
        Assert.Equal(2, links.Count);
        Assert.Equal(c.ItemId, Assert.Single(links, x => x.BranchName == "feature/Foo").WorkItemId);
        Assert.Equal(second, Assert.Single(links, x => x.BranchName == "feature/foo").WorkItemId);
        // Repetir o nome exato continua idempotente.
        await Operations(c).LinkBranchAsync(c.ProjectId, "T-0001", new("qa/metodo", "feature/Foo"), c.Actor!.ActorId, default);
        Assert.Equal(2, await c.Context.Set<TaskGitLink>().AsNoTracking().CountAsync(x => x.ProjectId == c.ProjectId));
    }

    [MethodGitSqlFact]
    public async Task CA8b_webhook_create_of_case_variant_does_not_mark_original_branch_remote()
    {
        await using var c = await _fixture.CaseAsync();
        await c.UseAgentAsync();
        await Operations(c).LinkBranchAsync(c.ProjectId, "T-0001", new("qa/metodo", "feature/Foo"), c.Actor!.ActorId, default);
        c.UseActor(Worker(c));
        var outcome = await ProcessCreateAsync(c, "feature/foo", new NoMovement());
        // Sem vínculo com a grafia exata, o create cai na resolução pelo nome, que não acha task (feature/foo não cita ID).
        Assert.Equal("task_not_identified", outcome.Code);
        c.Context.ChangeTracker.Clear();
        var link = Assert.Single(await c.Context.Set<TaskGitLink>().AsNoTracking().Where(x => x.ProjectId == c.ProjectId).ToListAsync());
        Assert.Equal("feature/Foo", link.BranchName); Assert.Equal(GitBranchStates.Local, link.BranchState); Assert.Null(link.LastSeenAtUtc);
        Assert.Empty(await c.Context.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == c.ItemId && x.Kind == "git_branch_pushed").ToListAsync());
        // Com o nome exato, o mesmo create marca a branch como remote.
        Assert.Equal("branch_recorded", (await ProcessCreateAsync(c, "feature/Foo", new NoMovement())).Code);
    }

    [MethodGitSqlFact]
    public async Task CA8b_task_changed_before_webhook_marks_branch_is_retryable_concurrency_without_writing()
    {
        await using var c = await _fixture.CaseAsync();
        await c.UseAgentAsync();
        await Operations(c).LinkBranchAsync(c.ProjectId, "T-0001", new("qa/metodo", Branch), c.Actor!.ActorId, default);
        c.UseActor(Worker(c));
        c.Context.ChangeTracker.Clear();
        // A task muda (ID trocado) depois de validada pelo create e antes da gravação.
        var clock = new ChangeTaskOnFirstClockRead(c.Clock.Now, () =>
        {
            using var human = c.NewContext();
            human.Set<MethodTaskState>().Where(x => x.WorkItemId == c.ItemId).ExecuteUpdate(set => set.SetProperty(x => x.MethodTaskId, "T-0002"));
        });
        var store = new EfMethodGitStore(c.Context, new Tenant(c.Repository.OrganizationId), c.Access, new AllowAll(), clock);
        var error = await Assert.ThrowsAsync<MethodOperationException>(() => store.MarkBranchPushedAsync(c.Repository.Id, Branch, "4242", "dev-git"));
        // O worker trata git_concurrency como falha retentável (GitDeliveryWorker): a reentrega relê a task.
        Assert.Equal(409, error.StatusCode); Assert.Equal("git_concurrency", error.Code);
        c.Context.ChangeTracker.Clear();
        var link = Assert.Single(await c.Context.Set<TaskGitLink>().AsNoTracking().Where(x => x.ProjectId == c.ProjectId).ToListAsync());
        Assert.Equal(GitBranchStates.Local, link.BranchState); Assert.Null(link.LastSeenAtUtc);
        Assert.Empty(await c.Context.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == c.ItemId && x.Kind == "git_branch_pushed").ToListAsync());
    }

    [MethodGitSqlFact]
    public async Task CA8b_task_changed_before_inferred_link_is_saved_is_retryable_concurrency_without_writing()
    {
        await using var c = await _fixture.CaseAsync();
        c.UseActor(Worker(c));
        c.Context.ChangeTracker.Clear();
        // D-4: LinkInferredAsync (create/PR do webhook) valida a task e, antes de gravar, a task muda (ID trocado).
        var clock = new ChangeTaskOnFirstClockRead(c.Clock.Now, () =>
        {
            using var human = c.NewContext();
            human.Set<MethodTaskState>().Where(x => x.WorkItemId == c.ItemId).ExecuteUpdate(set => set.SetProperty(x => x.MethodTaskId, "T-0002"));
        });
        var store = new EfMethodGitStore(c.Context, new Tenant(c.Repository.OrganizationId), c.Access, new AllowAll(), clock);
        var error = await Assert.ThrowsAsync<MethodOperationException>(() => store.LinkInferredAsync(c.Repository.Id, c.ItemId, null, Branch, "branch"));
        Assert.Equal(409, error.StatusCode); Assert.Equal("git_concurrency", error.Code);
        c.Context.ChangeTracker.Clear();
        Assert.Equal("T-0002", await c.Context.Set<MethodTaskState>().AsNoTracking().Where(x => x.WorkItemId == c.ItemId).Select(x => x.MethodTaskId).SingleAsync());
        Assert.Empty(await c.Context.Set<TaskGitLink>().AsNoTracking().Where(x => x.ProjectId == c.ProjectId).ToListAsync());
        // A reentrega relê a task: com o ID de volta e sem troca concorrente, o mesmo vínculo inferido grava normalmente.
        await using (var human = c.NewContext())
            await human.Set<MethodTaskState>().Where(x => x.WorkItemId == c.ItemId).ExecuteUpdateAsync(set => set.SetProperty(x => x.MethodTaskId, "T-0001"));
        await c.Store(c.NewContext()).LinkInferredAsync(c.Repository.Id, c.ItemId, null, Branch, "branch");
        Assert.Single(await c.Context.Set<TaskGitLink>().AsNoTracking().Where(x => x.ProjectId == c.ProjectId && x.BranchName == Branch).ToListAsync());
    }

    [MethodGitSqlFact]
    public async Task CA8b_webhook_create_turns_local_branch_remote_and_records_git_branch_pushed_once()
    {
        await using var c = await _fixture.CaseAsync();
        await c.UseAgentAsync();
        await Operations(c).LinkBranchAsync(c.ProjectId, "T-0001", new("qa/metodo", Branch), c.Actor!.ActorId, default);
        c.UseActor(Worker(c));
        var move = new NoMovement();
        var first = await ProcessCreateAsync(c, Branch, move);
        Assert.Equal("branch_recorded", first.Code); Assert.False(first.Moved);
        c.Context.ChangeTracker.Clear();
        var link = Assert.Single(await c.Context.Set<TaskGitLink>().AsNoTracking().Where(x => x.ProjectId == c.ProjectId).ToListAsync());
        Assert.Equal(GitBranchStates.Remote, link.BranchState); Assert.Equal(c.Clock.Now, link.LastSeenAtUtc);
        Assert.Equal("explicit", link.Source);
        var pushed = Assert.Single(await c.Context.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == c.ItemId && x.Kind == "git_branch_pushed").ToListAsync());
        using (var payload = JsonDocument.Parse(pushed.Payload!))
        {
            Assert.Equal("local", payload.RootElement.GetProperty("previousBranchState").GetString());
            Assert.Equal("dev-git", payload.RootElement.GetProperty("gitActorName").GetString());
            Assert.Equal(Branch, payload.RootElement.GetProperty("branch").GetString());
        }
        Assert.NotEqual("dev-git", pushed.ActorId);
        // Reenvio do create: continua remote, sem segundo evento; e branch nunca move card.
        c.Clock.Now = c.Now.AddMinutes(1);
        Assert.Equal("branch_recorded", (await ProcessCreateAsync(c, Branch, move)).Code);
        c.Context.ChangeTracker.Clear();
        Assert.Single(await c.Context.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == c.ItemId && x.Kind == "git_branch_pushed").ToListAsync());
        Assert.Equal(c.Clock.Now, (await c.Context.Set<TaskGitLink>().AsNoTracking().SingleAsync(x => x.ProjectId == c.ProjectId)).LastSeenAtUtc);
        Assert.Empty(move.Requests);
        Assert.Equal(c.InDevelopmentStageId, (await c.Context.WorkItems.AsNoTracking().SingleAsync(x => x.Id == c.ItemId)).StageId);
    }

    [MethodGitSqlFact]
    public async Task CA8b_webhook_create_without_prior_link_infers_task_and_marks_remote()
    {
        await using var c = await _fixture.CaseAsync();
        c.UseActor(Worker(c));
        var move = new NoMovement();
        Assert.Equal("branch_recorded", (await ProcessCreateAsync(c, "task/T-0001-inferida", move)).Code);
        c.Context.ChangeTracker.Clear();
        var link = Assert.Single(await c.Context.Set<TaskGitLink>().AsNoTracking().Where(x => x.ProjectId == c.ProjectId).ToListAsync());
        Assert.Equal("branch", link.Source); Assert.Equal(GitBranchStates.Remote, link.BranchState); Assert.NotNull(link.LastSeenAtUtc);
        Assert.Single(await c.Context.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == c.ItemId && x.Kind == "git_branch_pushed").ToListAsync());
        Assert.Empty(move.Requests);
    }

    [MethodGitSqlFact]
    public async Task CA8c_migration_applies_and_reverts_with_existing_app_integration_and_PR_webhook_still_moves_card()
    {
        var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PRISMA_METHOD_GIT_SQL"));
        Assert.True(connection.DataSource is "127.0.0.1,3132" or "localhost,3132", "Migration só pode executar no SQL próprio 3132.");
        connection.InitialCatalog = "Prisma_MethodGit_Migration_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection.ConnectionString).Options;
        await using var db = new AppDbContext(options);
        try
        {
            var migrations = db.Database.GetMigrations().ToArray();
            var index = Array.FindIndex(migrations, m => m.EndsWith("_Add_MethodGit_BranchLinks_And_Polling", StringComparison.Ordinal));
            Assert.True(index > 0, "Migração da HU-0002 ausente.");
            var previous = migrations[index - 1];
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync();
            // Integração atual (GitHub App) com vínculo de branch e observação de PR, como em produção antes da HU.
            await using var c = new GitDeliverySqlFixture.SqlCase(_ => options, new GitDeliverySqlFixture.FailBeforeCommit());
            await c.SeedAsync();
            await c.Store(c.Context).LinkInferredAsync(c.Repository.Id, c.ItemId, null, "task/T-0001-legado", "branch");
            await migrator.MigrateAsync(previous);
            Assert.False(await ColumnExistsAsync(db, "GitRepositoryIntegrations", "Mode"));
            Assert.False(await ColumnExistsAsync(db, "TaskGitLinks", "BranchState"));
            Assert.False(await TableExistsAsync(db, "GitReadCredentials"));
            Assert.Equal("424242|1|test-app-key|test-webhook-secret", await ScalarAsync(db,
                $"SELECT CONCAT([AppId],'|',[InstallationId],'|',[AppPrivateKeySecretRef],'|',[WebhookSecretRef]) FROM [GitRepositoryIntegrations] WHERE [Id]='{c.Repository.Id}'"));
            Assert.Equal("1", await ScalarAsync(db, $"SELECT COUNT(*) FROM [TaskGitLinks] WHERE [WorkItemId]='{c.ItemId}'"));
            // Down devolve BranchName à collation padrão do banco, com o índice único filtrado de volta.
            Assert.Equal(await ScalarAsync(db, "SELECT CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), 'Collation'))"), await BranchCollationAsync(db));
            Assert.Equal(BranchIndexShape, await BranchIndexAsync(db));
            Assert.Equal(BranchTargetCheck, await BranchCheckAsync(db));
            // Reaplica sobre o banco com dados: integrações existentes ficam no modo app; vínculos existentes, remote.
            await migrator.MigrateAsync();
            Assert.Equal("app", await ScalarAsync(db, $"SELECT [Mode] FROM [GitRepositoryIntegrations] WHERE [Id]='{c.Repository.Id}'"));
            Assert.Equal("remote|", await ScalarAsync(db, $"SELECT CONCAT([BranchState],'|',[LastSeenAtUtc]) FROM [TaskGitLinks] WHERE [WorkItemId]='{c.ItemId}'"));
            Assert.True(await TableExistsAsync(db, "GitReadCredentials"));
            // Up troca a collation de BranchName para binária, mantendo o vínculo, o índice único filtrado e a check constraint.
            Assert.Equal("Latin1_General_100_BIN2", await BranchCollationAsync(db));
            Assert.Equal(BranchIndexShape, await BranchIndexAsync(db));
            Assert.Equal("nvarchar|510|1", await ScalarAsync(db, "SELECT CONCAT(TYPE_NAME(c.user_type_id),'|',c.max_length,'|',CONVERT(int, c.is_nullable)) FROM sys.columns c WHERE c.object_id = OBJECT_ID('TaskGitLinks') AND c.name = 'BranchName'"));
            Assert.Equal(BranchTargetCheck, await BranchCheckAsync(db));
            // Modo app sem App continua proibido; só o modo consulta dispensa App, instalação e segredos.
            await using (var check = new AppDbContext(options))
            {
                await Assert.ThrowsAsync<SqlException>(() => check.Database.ExecuteSqlRawAsync(
                    "UPDATE [GitRepositoryIntegrations] SET [AppId]=NULL WHERE [Id]={0}", c.Repository.Id));
                await Assert.ThrowsAsync<SqlException>(() => check.Database.ExecuteSqlRawAsync(
                    "UPDATE [GitRepositoryIntegrations] SET [WebhookSecretRef]=NULL WHERE [Id]={0}", c.Repository.Id));
                await Assert.ThrowsAsync<SqlException>(() => check.Database.ExecuteSqlRawAsync(
                    "UPDATE [GitRepositoryIntegrations] SET [Mode]='outro' WHERE [Id]={0}", c.Repository.Id));
            }
            // Webhook de PR da integração atual continua movendo o card (store, resolvedor e movimento reais).
            c.UseActor(Worker(c));
            await using var work = c.NewContext();
            var store = c.Store(work);
            var pr = new GitPullRequestSnapshot(7, "https://github.com/qa/metodo/pull/7", "Entrega", "task/T-0001-legado", "main", new string('c', 40), null,
                "open", false, false, c.Now, c.Now, HeadRepositoryId: c.Repository.RepositoryId);
            var provider = new PullRequestProvider(pr);
            var movement = new MethodMoveService(work, c.Access, store, new WorkflowRepository(work), new AllowAll(), new SilentRealtime(),
                NullLogger<MethodMoveService>.Instance, c.Clock);
            var processor = new GitDeliveryProcessor(store, provider, new GitTaskResolver(store), movement, NullLogger<GitDeliveryProcessor>.Instance, c.Clock);
            var e = new NormalizedGitEvent("github", c.Repository.RepositoryId, c.Repository.InstallationId, "pull_request", "opened", c.Now,
                "rev-pr", null, 7, pr.Url, pr.Title, pr.HeadBranch, pr.BaseBranch, pr.HeadSha, null, "open", HeadRepositoryId: c.Repository.RepositoryId);
            var outcome = await processor.ProcessAsync(await ClaimAsync(c, store, e));
            Assert.True(outcome.Moved, outcome.Code);
            Assert.Equal(c.InReviewStageId, (await c.NewContext().WorkItems.AsNoTracking().SingleAsync(x => x.Id == c.ItemId)).StageId);
            // Reverter com branches que só diferem por maiúsculas é recusado (o índice sem diferenciar quebraria).
            await using (var variant = c.NewContext())
                await c.Store(variant).LinkInferredAsync(c.Repository.Id, c.ItemId, null, "task/T-0001-LEGADO", "branch");
            Assert.Equal("2", await ScalarAsync(db, $"SELECT COUNT(*) FROM [TaskGitLinks] WHERE [WorkItemId]='{c.ItemId}' AND [PullRequestObservationId] IS NULL"));
            var caseRefused = await Assert.ThrowsAsync<SqlException>(() => db.GetService<IMigrator>().MigrateAsync(previous));
            Assert.Equal(50002, caseRefused.Number);
            Assert.Equal("Latin1_General_100_BIN2", await BranchCollationAsync(db));
            Assert.Equal(BranchIndexShape, await BranchIndexAsync(db));
            Assert.Equal(BranchTargetCheck, await BranchCheckAsync(db));
            await db.Database.ExecuteSqlRawAsync("DELETE FROM [TaskGitLinks] WHERE [BranchName] = N'task/T-0001-LEGADO'");
            // Reverter com integração em modo consulta é recusado (não apaga a configuração em silêncio).
            await using (var poll = new AppDbContext(options))
            {
                var credential = await InsertCredentialAsync(poll, c, "github_pat_ABCDE");
                await poll.Database.ExecuteSqlRawAsync("UPDATE [GitRepositoryIntegrations] SET [Mode]='poll', [ReadCredentialId]={1}, [AppPrivateKeySecretRef]=NULL, [WebhookSecretRef]=NULL WHERE [Id]={0}",
                    c.Repository.Id, credential);
            }
            var refused = await Assert.ThrowsAsync<SqlException>(() => db.GetService<IMigrator>().MigrateAsync(previous));
            Assert.Equal(50001, refused.Number);
            Assert.True(await ColumnExistsAsync(db, "GitRepositoryIntegrations", "Mode"));
            Assert.True(await TableExistsAsync(db, "GitReadCredentials"));
            // A reversão recusada desfaz tudo: a integração continua em modo consulta, com a credencial e sem os segredos da App.
            Assert.Equal("poll||", await ScalarAsync(db, $"SELECT CONCAT([Mode],'|',[AppPrivateKeySecretRef],'|',[WebhookSecretRef]) FROM [GitRepositoryIntegrations] WHERE [Id]='{c.Repository.Id}' AND [ReadCredentialId] IS NOT NULL"));
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    [MethodGitSqlFact]
    public async Task CA8_branch_naming_another_task_is_refused_in_both_layers_and_own_or_no_id_is_accepted()
    {
        await using var c = await _fixture.CaseAsync();
        await c.AddTaskAsync("T-0002");
        await c.UseAgentAsync();
        var refused = await Assert.ThrowsAsync<MethodOperationException>(() =>
            Operations(c).LinkBranchAsync(c.ProjectId, "T-0001", new("qa/metodo", "task/T-0002-outra"), c.Actor!.ActorId, default));
        Assert.Equal(400, refused.StatusCode); Assert.Equal("git_branch_other_task", refused.Code);
        // Defesa no store: mesma recusa sem passar pela operação (T-00021 não é a T-0001).
        var direct = await Assert.ThrowsAsync<MethodOperationException>(() => c.Store(c.Context).LinkBranchAsync(c.Repository.Id, c.ItemId, "feat/T-00021-x"));
        Assert.Equal(400, direct.StatusCode); Assert.Equal("git_branch_other_task", direct.Code);
        c.Context.ChangeTracker.Clear();
        Assert.Empty(await c.Context.Set<TaskGitLink>().AsNoTracking().Where(x => x.ProjectId == c.ProjectId).ToListAsync());
        Assert.Empty(await c.Context.TaskEvents.AsNoTracking().Where(x => x.WorkItemId == c.ItemId && x.Kind == "git_branch_linked").ToListAsync());
        // Com o próprio ID ou sem ID, aceita.
        await Operations(c).LinkBranchAsync(c.ProjectId, "T-0001", new("qa/metodo", "task/T-0001-propria"), c.Actor!.ActorId, default);
        await Operations(c).LinkBranchAsync(c.ProjectId, "T-0001", new("qa/metodo", "feature/sem-id"), c.Actor!.ActorId, default);
        c.Context.ChangeTracker.Clear();
        Assert.Equal(2, await c.Context.Set<TaskGitLink>().AsNoTracking().CountAsync(x => x.ProjectId == c.ProjectId && x.WorkItemId == c.ItemId));
    }

    [MethodGitSqlFact]
    public async Task CA8c_poll_mode_requires_read_credential_and_token_prefix_is_limited_to_16()
    {
        await using var c = await _fixture.CaseAsync();
        // Modo consulta sem credencial: o banco recusa (CK_GitRepositoryIntegrations_PollCredential).
        var noCredential = await Assert.ThrowsAsync<SqlException>(() => c.Context.Database.ExecuteSqlRawAsync(
            "UPDATE [GitRepositoryIntegrations] SET [Mode]='poll' WHERE [Id]={0}", c.Repository.Id));
        Assert.Equal(547, noCredential.Number);
        Assert.Contains("CK_GitRepositoryIntegrations_PollCredential", noCredential.Message);
        // Prefixo com mais de 16 caracteres: recusado pela coluna varchar(16); nada é gravado nem truncado.
        var longPrefix = await Assert.ThrowsAsync<SqlException>(() => InsertCredentialAsync(c.Context, c, "github_pat_ABCDEF"));
        Assert.Contains(longPrefix.Number, new[] { 2628, 8152 });
        Assert.Equal("0", await ScalarAsync(c.Context, $"SELECT COUNT(*) FROM [GitReadCredentials] WHERE [ProjectId]='{c.ProjectId}'"));
        // Com credencial do projeto e prefixo de 16, o modo consulta é aceito sem App nem segredos.
        var credential = await InsertCredentialAsync(c.Context, c, "github_pat_ABCDE");
        await c.Context.Database.ExecuteSqlRawAsync("UPDATE [GitRepositoryIntegrations] SET [Mode]='poll', [ReadCredentialId]={1}, [AppPrivateKeySecretRef]=NULL, [WebhookSecretRef]=NULL WHERE [Id]={0}",
            c.Repository.Id, credential);
        Assert.Equal("poll|github_pat_ABCDE", await ScalarAsync(c.Context,
            $"SELECT CONCAT(i.[Mode],'|',r.[TokenPrefix]) FROM [GitRepositoryIntegrations] i JOIN [GitReadCredentials] r ON r.[Id]=i.[ReadCredentialId] WHERE i.[Id]='{c.Repository.Id}'"));
        // Devolve o caso ao modo app para não afetar outros testes do mesmo banco.
        await c.Context.Database.ExecuteSqlRawAsync("UPDATE [GitRepositoryIntegrations] SET [Mode]='app', [ReadCredentialId]=NULL, [AppPrivateKeySecretRef]='test-app-key', [WebhookSecretRef]='test-webhook-secret' WHERE [Id]={0}",
            c.Repository.Id);
    }

    private static async Task<Guid> InsertCredentialAsync(AppDbContext db, GitDeliverySqlFixture.SqlCase c, string prefix)
    {
        var id = Guid.NewGuid();
        await db.Database.ExecuteSqlRawAsync("INSERT INTO [GitReadCredentials] ([Id],[OrganizationId],[ProjectId],[GitHubOwner],[ProtectedValue],[TokenPrefix],[CreatedAtUtc],[CreatedByUserId]) " +
            "VALUES ({0},{1},{2},'qa','valor-cifrado-de-teste',{3},SYSDATETIMEOFFSET(),'qa-humano')", id, c.Repository.OrganizationId, c.ProjectId, prefix);
        return id;
    }
    private static GitPullRequestOperations Operations(GitDeliverySqlFixture.SqlCase c)
    {
        var store = c.Store(c.Context); var provider = new TestGitProvider(); var permissions = new AllowAll();
        return new(store, provider, new GitRepositoryOperations(store, provider, new TestGitSecrets(), c.Access, permissions, c.Clock), c.Access, permissions);
    }
    private static MethodActorIdentity Worker(GitDeliverySqlFixture.SqlCase c) => new(MethodAuthenticationKind.IntegrationWorker, MethodRole.System,
        c.Repository.SystemPrincipalId, $"method:{c.Repository.SystemPrincipalId:D}", "Integração SQL", c.Repository.OrganizationId, c.ProjectId, null, null,
        Enum.GetValues<PlatformPermission>().ToHashSet(), MethodChannels.Webhook, "sql-correlation");
    private static async Task<GitProcessingOutcome> ProcessCreateAsync(GitDeliverySqlFixture.SqlCase c, string branch, IMethodMoveService move)
    {
        c.Context.ChangeTracker.Clear();
        var store = c.Store(c.Context);
        var processor = new GitDeliveryProcessor(store, new TestGitProvider(), new GitTaskResolver(store), move, NullLogger<GitDeliveryProcessor>.Instance, c.Clock);
        var e = new NormalizedGitEvent("github", c.Repository.RepositoryId, c.Repository.InstallationId, "create", "branch", c.Clock.Now,
            "rev-create", branch, null, null, null, null, null, null, null, null, ObservedGitActorId: "4242", ObservedGitActorName: "dev-git");
        return await processor.ProcessAsync(await ClaimAsync(c, store, e));
    }
    private static async Task<GitDeliverySnapshot> ClaimAsync(GitDeliverySqlFixture.SqlCase c, IMethodGitStore store, NormalizedGitEvent e)
    {
        var receipt = await store.AcceptDeliveryAsync(c.Repository.Id, Guid.NewGuid().ToString("D"), SHA256.HashData(Guid.NewGuid().ToByteArray()), e, "sql-correlation");
        var lease = await store.ClaimNextDeliveryAsync(c.Clock.Now, TimeSpan.FromMinutes(5));
        Assert.NotNull(lease); Assert.Equal(receipt.DeliveryId, lease!.Id);
        return (await store.GetDeliveryAsync(lease.Id))!;
    }
    private static async Task<string?> ScalarAsync(AppDbContext db, string sql)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync());
    }
    private static async Task<bool> ColumnExistsAsync(AppDbContext db, string table, string column)
        => await ScalarAsync(db, $"SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('{table}') AND name = '{column}'") == "1";
    private static Task<string?> BranchCollationAsync(AppDbContext db)
        => ScalarAsync(db, "SELECT collation_name FROM sys.columns WHERE object_id = OBJECT_ID('TaskGitLinks') AND name = 'BranchName'");
    // Forma do índice único filtrado como o SQL Server a guarda: único|filtrado|filtro normalizado|colunas-chave na ordem.
    private const string BranchIndexShape = "1|1|([PullRequestObservationId] IS NULL AND [BranchName] IS NOT NULL)|IntegrationId,BranchName";
    // Mesma regra da migração 20261016110439 ("[PullRequestObservationId] IS NOT NULL OR [BranchName] IS NOT NULL"), normalizada pelo SQL Server.
    private const string BranchTargetCheck = "([PullRequestObservationId] IS NOT NULL OR [BranchName] IS NOT NULL)";
    private static Task<string?> BranchIndexAsync(AppDbContext db)
        => ScalarAsync(db, "SELECT CONCAT(CONVERT(int, i.is_unique),'|',CONVERT(int, i.has_filter),'|',i.filter_definition,'|',"
            + "(SELECT STRING_AGG(col.name, ',') WITHIN GROUP (ORDER BY ic.key_ordinal) FROM sys.index_columns ic "
            + "JOIN sys.columns col ON col.object_id = ic.object_id AND col.column_id = ic.column_id "
            + "WHERE ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0)) "
            + "FROM sys.indexes i WHERE i.object_id = OBJECT_ID('TaskGitLinks') AND i.name = 'IX_TaskGitLinks_IntegrationId_BranchName'");
    private static Task<string?> BranchCheckAsync(AppDbContext db)
        => ScalarAsync(db, "SELECT definition FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('TaskGitLinks') AND name = 'CK_TaskGitLinks_Target'");
    private static async Task<bool> TableExistsAsync(AppDbContext db, string table)
        => await ScalarAsync(db, $"SELECT COUNT(*) FROM sys.tables WHERE name = '{table}'") == "1";

    private sealed class AllowAll : IPermissionService
    {
        public Task<bool> HasAsync(string actor, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? id = null, CancellationToken ct = default) => Task.FromResult(true);
        public Task EnsureAsync(string actor, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? id = null, CancellationToken ct = default) => Task.CompletedTask;
    }
    private sealed class ChangeTaskOnFirstClockRead(DateTimeOffset now, Action change) : TimeProvider
    {
        private bool _changed;
        public override DateTimeOffset GetUtcNow()
        {
            if (!_changed) { _changed = true; change(); }
            return now;
        }
    }
    private sealed class Tenant(Guid id) : IOrganizationContext { public Guid? OrganizationId => id; }
    private sealed class ChangeTaskOnFirstCheck(Func<Task> change) : IPermissionService
    {
        private bool _changed;
        public Task<bool> HasAsync(string actor, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? id = null, CancellationToken ct = default) => Task.FromResult(true);
        public async Task EnsureAsync(string actor, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? id = null, CancellationToken ct = default)
        {
            if (_changed) return;
            _changed = true; await change();
        }
    }
    private sealed class NoMovement : IMethodMoveService
    {
        public List<MethodMoveRequest> Requests { get; } = [];
        public Task<MethodMoveResult> MoveAsync(MethodMoveRequest request, CancellationToken ct = default) { Requests.Add(request); throw new InvalidOperationException("branch não move card"); }
    }
    private sealed class PullRequestProvider(GitPullRequestSnapshot pr) : TestGitProvider
    {
        public override Task<GitPullRequestSnapshot> GetPullRequestAsync(GitRepositoryDescriptor repository, int number, CancellationToken ct = default) => Task.FromResult(pr);
    }
    private sealed class SilentRealtime : IBoardRealtimeNotifier
    { public Task BoardChangedAsync(Guid boardId, string changeType, Guid? workItemId = null, CancellationToken ct = default) => Task.CompletedTask; }
}
