using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.WorkItems;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Infrastructure.Persistence;
using Prisma.Workspace.Infrastructure.Repositories;
using Prisma.Workspace.Infrastructure.Services.WorkItems;

namespace Prisma.Workspace.Tests;

/// <summary>
/// T-0019: editar um item pela gaveta (PUT /api/workitems/{id}) nunca troca o tipo por conta própria,
/// e a troca pedida respeita a hierarquia (B-1 a B-5). Nasceu do teste de reprodução do diagnóstico.
/// Os testes SQL (B-1 persistido e B-6, rotina de correção) são opt-in por ConnectionStrings__DefaultConnection
/// num banco Prisma_BoardColumns_*.
/// </summary>
public class WorkItemKindUpdateTests
{
    private const string ActorId = "user-1";

    // ── unitário (repositório em memória) ──────────────────────────────────

    [Fact] // B-1
    public async Task EditarTituloDeTarefaSobHistoria_MantemKindTask()
    {
        var (board, stage) = Quadro();
        var historia = Item(board, stage, WorkItemKind.UserStory, null);
        var tarefa = Item(board, stage, WorkItemKind.Task, historia);

        // A gaveta reenvia o payload inteiro (inclusive kind = Task) a cada edição de campo.
        await Handler(tarefa, stage).Handle(
            Comando(tarefa, stage.Id, WorkItemKind.Task) with { Title = "Só mudei o título" },
            CancellationToken.None);

        Assert.Equal(WorkItemKind.Task, tarefa.Kind);
        Assert.Equal("Só mudei o título", tarefa.Title);
    }

    [Theory] // B-2
    [InlineData(WorkItemKind.Task)]
    [InlineData(WorkItemKind.Subtask)]
    public async Task EditarSubtarefaSobTarefa_ContinuaSubtarefa(WorkItemKind enviado)
    {
        var (board, stage) = Quadro();
        var pai = Item(board, stage, WorkItemKind.Task, null);
        var sub = Item(board, stage, WorkItemKind.Subtask, pai);

        await Handler(sub, stage).Handle(Comando(sub, stage.Id, enviado) with { Title = "Editada" }, CancellationToken.None);

        Assert.Equal(WorkItemKind.Subtask, sub.Kind);
    }

    [Fact] // B-3
    public async Task EditarTipoParaEpicoSobHistoria_EhRecusado()
    {
        var (board, stage) = Quadro();
        var historia = Item(board, stage, WorkItemKind.UserStory, null);
        var tarefa = Item(board, stage, WorkItemKind.Task, historia);

        // UserStory → Epic não pertence a WorkItemHierarchyRules; a criação recusaria.
        await Assert.ThrowsAsync<DomainException>(() => Handler(tarefa, stage).Handle(
            Comando(tarefa, stage.Id, WorkItemKind.Epic) with { Title = "Não grava" }, CancellationToken.None));

        Assert.Equal(WorkItemKind.Task, tarefa.Kind);
        Assert.NotEqual("Não grava", tarefa.Title);
    }

    [Fact] // B-4
    public async Task EditarHistoriaComTarefasParaBug_EhRecusado()
    {
        var (board, stage) = Quadro();
        var historia = Item(board, stage, WorkItemKind.UserStory, null);
        var tarefa = Item(board, stage, WorkItemKind.Task, historia);
        historia.SubItems.Add(tarefa);

        await Assert.ThrowsAsync<DomainException>(() => Handler(historia, stage).Handle(
            Comando(historia, stage.Id, WorkItemKind.Bug), CancellationToken.None));

        Assert.Equal(WorkItemKind.UserStory, historia.Kind);
    }

    [Fact] // B-4: filho arquivado não impede a troca
    public async Task EditarHistoriaComFilhoArquivado_PermiteTroca()
    {
        var (board, stage) = Quadro();
        var historia = Item(board, stage, WorkItemKind.UserStory, null);
        var tarefa = Item(board, stage, WorkItemKind.Task, historia);
        tarefa.IsArchived = true;
        historia.SubItems.Add(tarefa);

        await Handler(historia, stage).Handle(Comando(historia, stage.Id, WorkItemKind.Bug), CancellationToken.None);

        Assert.Equal(WorkItemKind.Bug, historia.Kind);
    }

    [Fact] // B-5
    public async Task SubtarefaSobHistoria_EscolherTarefa_GravaTarefa()
    {
        var (board, stage) = Quadro();
        var historia = Item(board, stage, WorkItemKind.UserStory, null);
        var afetada = Item(board, stage, WorkItemKind.Subtask, historia);

        await Handler(afetada, stage).Handle(Comando(afetada, stage.Id, WorkItemKind.Task), CancellationToken.None);

        Assert.Equal(WorkItemKind.Task, afetada.Kind);
    }

    [Fact] // D-1: item legado fora da hierarquia continua editável quando o tipo não muda
    public async Task ItemLegadoForaDaHierarquia_SemTrocaDeTipo_ContinuaEditavel()
    {
        var (board, stage) = Quadro();
        var historia = Item(board, stage, WorkItemKind.UserStory, null);
        var afetada = Item(board, stage, WorkItemKind.Subtask, historia);

        await Handler(afetada, stage).Handle(Comando(afetada, stage.Id, WorkItemKind.Subtask) with { Title = "Novo" },
            CancellationToken.None);

        Assert.Equal(WorkItemKind.Subtask, afetada.Kind);
        Assert.Equal("Novo", afetada.Title);
    }

    [Fact]
    public async Task SubtarefaSemPai_EhRecusada()
    {
        var (board, stage) = Quadro();
        var tarefa = Item(board, stage, WorkItemKind.Task, null);

        await Assert.ThrowsAsync<DomainException>(() => Handler(tarefa, stage).Handle(
            Comando(tarefa, stage.Id, WorkItemKind.Subtask), CancellationToken.None));
    }

    // ── integração (SQL Server de teste, opt-in) ───────────────────────────

    private static string? SqlConnection()
    {
        var value = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(value)) return null; // opt-in
        Assert.StartsWith("Prisma_BoardColumns_", new SqlConnectionStringBuilder(value).InitialCatalog);
        return value;
    }

    [Fact] // B-1 com EF real
    public async Task Sql_EditarTarefaSobHistoria_PersisteTask()
    {
        var value = SqlConnection();
        if (value is null) return;
        var orgId = Guid.NewGuid();
        AppDbContext NovoContexto() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(value).Options,
            new OrgFake(orgId));

        await using var ctx = NovoContexto();
        await ctx.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow;
        var (_, board, stage) = Estrutura(ctx, orgId, now);
        var historia = new WorkItem { Id = Guid.NewGuid(), BoardId = board.Id, StageId = stage.Id, Kind = WorkItemKind.UserStory, Title = "História", CreatedAt = now, UpdatedAt = now, Position = 100 };
        var tarefa = new WorkItem { Id = Guid.NewGuid(), BoardId = board.Id, StageId = stage.Id, ParentId = historia.Id, Kind = WorkItemKind.Task, Title = "Tarefa", CreatedAt = now, UpdatedAt = now, Position = 200 };
        ctx.AddRange(historia, tarefa);
        await ctx.SaveChangesAsync();

        await using (var edit = NovoContexto())
        {
            var handler = new UpdateWorkItemCommandHandler(
                new WorkItemManagementRepository(edit), new FakeProjectAccess(), new FakePermissions(),
                new StageRepository(edit), new FakeTeams(), new FakeUsers(), new FakeFeed());
            await handler.Handle(Comando(tarefa, stage.Id, WorkItemKind.Task) with { Title = "Tarefa (editada)" },
                CancellationToken.None);
        }

        await using var read = NovoContexto();
        var gravado = await read.WorkItems.AsNoTracking().SingleAsync(x => x.Id == tarefa.Id);
        Assert.Equal(WorkItemKind.Task, gravado.Kind);
        Assert.Equal("Tarefa (editada)", gravado.Title);
    }

    [Fact] // B-6
    public async Task Sql_RotinaDeCorrecao_EnsaioListaECorrecaoSoTocaOsItensDoBug()
    {
        var value = SqlConnection();
        if (value is null) return;
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(value).Options;
        await using (var migrate = new AppDbContext(options)) await migrate.Database.MigrateAsync();

        var now = DateTimeOffset.UtcNow.AddMinutes(-10);
        Guid corrigirA = default, corrigirB = default, subSobTarefa = default, semEvento = default,
            sobEpico = default, mudouDepois = default, arquivado = default;
        foreach (var org in new[] { orgA, orgB })
        {
            await using var seed = new AppDbContext(options, new OrgFake(org));
            var (_, board, stage) = Estrutura(seed, org, now);
            WorkItem Novo(WorkItemKind kind, WorkItem? parent, bool archived = false)
            {
                var item = new WorkItem { Id = Guid.NewGuid(), BoardId = board.Id, StageId = stage.Id, ParentId = parent?.Id,
                    Kind = kind, Title = "Título sigiloso", CreatedAt = now, UpdatedAt = now, Position = 100, IsArchived = archived };
                seed.Add(item);
                return item;
            }
            void Bug(WorkItem item, int minutes) => seed.Add(EventoUpdated(item.Id, 5, 6, now.AddMinutes(minutes)));
            var historia = Novo(WorkItemKind.UserStory, null);
            var tarefaPai = Novo(WorkItemKind.Task, null);
            var epico = Novo(WorkItemKind.Epic, null);
            var a = Novo(WorkItemKind.Subtask, historia); Bug(a, 1);
            if (org == orgA)
            {
                corrigirA = a.Id;
                var b = Novo(WorkItemKind.Subtask, tarefaPai); Bug(b, 1); subSobTarefa = b.Id;
                var c = Novo(WorkItemKind.Subtask, historia); semEvento = c.Id;
                var d = Novo(WorkItemKind.Subtask, epico); Bug(d, 1); sobEpico = d.Id;
                var e = Novo(WorkItemKind.Subtask, historia); Bug(e, 1);
                seed.Add(EventoUpdated(e.Id, 6, 7, now.AddMinutes(2)));
                seed.Add(EventoUpdated(e.Id, 7, 6, now.AddMinutes(3)));
                mudouDepois = e.Id;
                var f = Novo(WorkItemKind.Subtask, historia, archived: true); Bug(f, 1); arquivado = f.Id;
            }
            else corrigirB = a.Id;
            await seed.SaveChangesAsync();
        }
        var ids = new[] { corrigirA, corrigirB, subSobTarefa, semEvento, sobEpico, mudouDepois, arquivado };

        async Task<(Dictionary<Guid, WorkItemKind> Kinds, int Events)> RetratoAsync()
        {
            await using var read = new AppDbContext(options);
            var kinds = await read.WorkItems.IgnoreQueryFilters().AsNoTracking().Where(x => ids.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Kind);
            var events = await read.TaskEvents.IgnoreQueryFilters().CountAsync(x => ids.Contains(x.WorkItemId));
            return (kinds, events);
        }

        // Ensaio: lista e não grava nada (sem organização corrente, como na inicialização da API).
        var antes = await RetratoAsync();
        WorkItemKindRepairResult ensaio;
        await using (var run = new AppDbContext(options)) ensaio = await new WorkItemKindRepairRoutine(run).RunAsync(apply: false);
        var depoisEnsaio = await RetratoAsync();
        Assert.Equal(antes.Events, depoisEnsaio.Events);
        Assert.Equal(antes.Kinds, depoisEnsaio.Kinds);
        var minhas = ensaio.Entries.Where(x => x.OrganizationId == orgA || x.OrganizationId == orgB).ToList();
        Assert.Equal(new[] { arquivado, corrigirA, corrigirB }.OrderBy(x => x),
            minhas.Where(x => x.Repaired).Select(x => x.WorkItemId).OrderBy(x => x));
        var naoCorrigidos = minhas.Where(x => !x.Repaired).ToDictionary(x => x.WorkItemId);
        Assert.Contains("não aceita Tarefa", naoCorrigidos[sobEpico].NotRepairedReason);
        Assert.Contains("mudou de tipo", naoCorrigidos[mudouDepois].NotRepairedReason);
        Assert.DoesNotContain(minhas, x => x.WorkItemId == subSobTarefa || x.WorkItemId == semEvento);
        // A saída só tem identificadores.
        Assert.DoesNotContain("sigiloso", JsonSerializer.Serialize(ensaio));

        // Correção.
        await using (var run = new AppDbContext(options)) await new WorkItemKindRepairRoutine(run).RunAsync(apply: true);
        var depois = await RetratoAsync();
        Assert.Equal(WorkItemKind.Task, depois.Kinds[corrigirA]);
        Assert.Equal(WorkItemKind.Task, depois.Kinds[corrigirB]);
        Assert.Equal(WorkItemKind.Task, depois.Kinds[arquivado]);
        Assert.Equal(WorkItemKind.Subtask, depois.Kinds[subSobTarefa]);
        Assert.Equal(WorkItemKind.Subtask, depois.Kinds[semEvento]);
        Assert.Equal(WorkItemKind.Subtask, depois.Kinds[sobEpico]);
        Assert.Equal(WorkItemKind.Subtask, depois.Kinds[mudouDepois]);
        Assert.Equal(antes.Events + 3, depois.Events);
        await using (var read = new AppDbContext(options))
        {
            var repair = await read.TaskEvents.IgnoreQueryFilters().AsNoTracking()
                .SingleAsync(x => x.WorkItemId == corrigirA && x.Kind == WorkItemKindRepairRoutine.RepairedEventKind);
            Assert.Equal(WorkItemKindRepairRoutine.SystemActorId, repair.ActorId);
            Assert.Contains("\"previousKind\":6", repair.Payload);
            Assert.Contains("\"currentKind\":5", repair.Payload);
            Assert.DoesNotContain("sigiloso", repair.Payload);
        }

        // Segunda execução não muda nada.
        WorkItemKindRepairResult segunda;
        await using (var run = new AppDbContext(options)) segunda = await new WorkItemKindRepairRoutine(run).RunAsync(apply: true);
        var depoisSegunda = await RetratoAsync();
        Assert.Equal(depois.Kinds, depoisSegunda.Kinds);
        Assert.Equal(depois.Events, depoisSegunda.Events);
        Assert.DoesNotContain(segunda.Entries, x => x.Repaired && (x.OrganizationId == orgA || x.OrganizationId == orgB));
    }

    private static TaskEvent EventoUpdated(Guid itemId, int before, int after, DateTimeOffset at)
    {
        var e = TaskEvent.Registrar(itemId, ActorId, "updated", JsonSerializer.Serialize(new
        {
            actorName = "Teste",
            previous = new { Title = "x", Kind = before },
            current = new { Title = "x", Kind = after },
        }));
        e.CreatedAt = at;
        return e;
    }

    private static (Project, Board, Stage) Estrutura(AppDbContext ctx, Guid orgId, DateTimeOffset now)
    {
        var project = Project.Criar("R" + Guid.NewGuid().ToString("N")[..8], "T-0019", "test",
            WorkNature.Project, WorkType.Development);
        project.OrganizationId = orgId;
        var board = new Board { Id = Guid.NewGuid(), ProjectId = project.Id, OrganizationId = orgId, Name = "Q", OwnerId = "test", CreatedAt = now };
        var stage = new Stage { Id = Guid.NewGuid(), ProjectId = project.Id, BoardId = board.Id, Name = "A fazer", Position = 100, Category = StageCategory.Ready, CreatedAt = now };
        ctx.AddRange(new Organization { Id = orgId, Name = "T-0019", Slug = "t0019-" + orgId.ToString("N"), CreatedAt = now, UpdatedAt = now },
            project, board, stage);
        return (project, board, stage);
    }

    // ── helpers ────────────────────────────────────────────────────────────

    private static (Board, Stage) Quadro()
    {
        var board = new Board { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), OrganizationId = Guid.NewGuid(), Name = "Q", CreatedAt = DateTimeOffset.UtcNow };
        var stage = new Stage { Id = Guid.NewGuid(), ProjectId = board.ProjectId, BoardId = board.Id, Name = "A fazer", Category = StageCategory.Ready, Position = 100, CreatedAt = DateTimeOffset.UtcNow };
        return (board, stage);
    }

    private static WorkItem Item(Board board, Stage stage, WorkItemKind kind, WorkItem? parent)
        => new()
        {
            Id = Guid.NewGuid(), Number = 1, BoardId = board.Id, Board = board, StageId = stage.Id,
            ParentId = parent?.Id, Parent = parent, Kind = kind, Title = kind.ToString(), Priority = Priority.Medium,
            Position = 100, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static UpdateWorkItemCommand Comando(WorkItem item, Guid stageId, WorkItemKind kind)
        => new(item.Id, item.Title, null, kind, stageId, Priority.Medium, null, null, WorkItemOrigin.Internal,
            null, null, null, null, null, null, null, null, null, ActorId, "Teste");

    private static UpdateWorkItemCommandHandler Handler(WorkItem item, params Stage[] stages)
        => new(new FakeManagementRepo(item), new FakeProjectAccess(), new FakePermissions(),
            new FakeStages(stages), new FakeTeams(), new FakeUsers(), new FakeFeed());

    private sealed class OrgFake(Guid id) : IOrganizationContext { public Guid? OrganizationId => id; }

    private sealed class FakeManagementRepo(WorkItem item) : IWorkItemManagementRepository
    {
        public Task<WorkItem?> GetDetailedAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<WorkItem?>(item);
        public Task<WorkItem?> GetEditableAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<WorkItem?>(item);
        public Task<WorkItemLink?> GetLinkAsync(Guid workItemId, Guid linkId, CancellationToken cancellationToken = default) => Task.FromResult<WorkItemLink?>(null);
        public Task AddLinkAsync(WorkItemLink link, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> TryAddLinkAcyclicAsync(WorkItemLink link, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task RemoveLinkAsync(WorkItemLink link, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddFollowerAsync(Guid workItemId, string userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFollowerAsync(Guid workItemId, string userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveWithEventAsync(TaskEvent taskEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeProjectAccess : IProjectAccessService
    {
        public Task<ProjectRole?> GetRoleAsync(Guid projectId, string userId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectRole?>(ProjectRole.ProjectAdmin);
        public Task EnsureAtLeastAsync(Guid projectId, string userId, ProjectRole minimumRole, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakePermissions : IPermissionService
    {
        public Task<bool> HasAsync(string userId, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? scopeId = null, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task EnsureAsync(string userId, PlatformPermission permission, PermissionScope scope = PermissionScope.Organization, Guid? scopeId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeStages(Stage[] stages) : IStageRepository
    {
        public Task<Stage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(stages.FirstOrDefault(s => s.Id == id));
        public Task<IReadOnlyList<Stage>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Stage>>(stages.ToList());
        public Task<IReadOnlyList<Stage>> GetByBoardIdAsync(Guid boardId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Stage>>(stages.ToList());
        public Task<Stage> AddAsync(Stage stage, CancellationToken cancellationToken = default) => Task.FromResult(stage);
        public Task UpdateAsync(Stage stage, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Stage stage, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeTeams : ITeamRepository
    {
        public Task<IReadOnlyList<Team>> GetAllWithMembersAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Team>>([]);
        public Task<Team?> GetByIdWithMembersAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Team?>(null);
        public Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Team?>(null);
        public Task<TeamMember?> GetMemberAsync(Guid teamId, string userId, CancellationToken cancellationToken = default) => Task.FromResult<TeamMember?>(null);
        public Task<bool> NameExistsAsync(string name, Guid? exceptId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(Team team, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Team team, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveMemberAsync(TeamMember member, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeUsers : IUserDirectory
    {
        public Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(IEnumerable<string> userIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        public Task<IReadOnlyList<UserSummary>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<UserSummary>>([]);
        public Task<IReadOnlyList<UserSummary>> GetByIdsAsync(IEnumerable<string> userIds, bool includeInactive = false, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<UserSummary>>([]);
        public Task<UserSummary?> GetByIdAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult<UserSummary?>(null);
        public Task<UserSummary?> GetByIdUnscopedAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult<UserSummary?>(null);
        public Task<UserSummary?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => Task.FromResult<UserSummary?>(null);
    }

    private sealed class FakeFeed : ITaskFeedRepository
    {
        public Task<IReadOnlyList<Comment>> GetCommentsAsync(Guid workItemId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Comment>>([]);
        public Task<Comment> AddCommentAsync(Comment comment, CancellationToken cancellationToken = default) => Task.FromResult(comment);
        public Task<IReadOnlyList<TaskEvent>> GetEventsAsync(Guid workItemId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<TaskEvent>>([]);
        public Task<IReadOnlyList<StageHistory>> GetStageHistoriesAsync(Guid workItemId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<StageHistory>>([]);
        public Task AddEventAsync(TaskEvent taskEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
