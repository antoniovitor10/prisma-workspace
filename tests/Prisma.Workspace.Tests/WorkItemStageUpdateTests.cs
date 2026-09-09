using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.WorkItems;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;

namespace Prisma.Workspace.Tests;

/// <summary>
/// Cobre a alteração de etapa pela tela de detalhe.
///
/// O defeito original era a divergência entre <see cref="WorkItem.StageId"/> e a projeção
/// por quadro, que deixava o card preso na coluna antiga. A projeção foi eliminada pela
/// D83 e a tarefa passou a ter etapa e posição únicas, o que torna a divergência
/// impossível por construção. Estes testes protegem o comportamento observável que
/// motivou a correção: etapa e conclusão precisam refletir o destino escolhido.
/// </summary>
public class WorkItemStageUpdateTests
{
    private const string ActorId = "user-1";

    // ── helpers ────────────────────────────────────────────────────────────

    private static Board CriarBoard()
        => new()
        {
            Id = Guid.NewGuid(),
            ProjectId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Name = "Quadro",
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static Stage CriarStage(Guid projectId, string nome, StageCategory categoria, Guid? statusId = null)
        => new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = nome,
            Category = categoria,
            WorkflowStatusId = statusId,
            Position = 100,
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static WorkItem CriarItem(Board board, Stage stageAtual)
    {
        var item = new WorkItem
        {
            Id = Guid.NewGuid(),
            Number = 1,
            BoardId = board.Id,
            Board = board,
            StageId = stageAtual.Id,
            Title = "Tarefa",
            Priority = Priority.Medium,
            Position = 100,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        return item;
    }

    private static UpdateWorkItemCommand Comando(WorkItem item, Guid destinoStageId)
        => new(
            WorkItemId: item.Id,
            Title: item.Title,
            Description: null,
            Kind: WorkItemKind.Task,
            StageId: destinoStageId,
            Priority: Priority.Medium,
            ResponsibleId: null,
            TeamId: null,
            Origin: WorkItemOrigin.Internal,
            RequesterId: null,
            RequesterName: null,
            RequesterEmail: null,
            StartDate: null,
            DueDate: null,
            EstimatedHours: null,
            RemainingHours: null,
            Points: null,
            AcceptanceCriteria: null,
            ActorId: ActorId,
            ActorName: "Usuária de teste");

    private static UpdateWorkItemCommandHandler CriarHandler(WorkItem item, params Stage[] stages)
        => new(
            new FakeManagementRepo(item),
            new FakeProjectAccess(),
            new FakePermissions(),
            new FakeStages(stages),
            new FakeTeams(),
            new FakeUsers(),
            new FakeFeed());

    // ── testes ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_AoMudarEtapa_GravaEtapaEConclusao()
    {
        var board = CriarBoard();
        var emAndamento = CriarStage(board.ProjectId, "Em andamento", StageCategory.InProgress);
        var concluido = CriarStage(board.ProjectId, "Concluído", StageCategory.Done);
        var item = CriarItem(board, emAndamento);

        var handler = CriarHandler(item, emAndamento, concluido);
        await handler.Handle(Comando(item, concluido.Id), CancellationToken.None);

        Assert.Equal(concluido.Id, item.StageId);
        Assert.NotNull(item.CompletedAt);
    }

    [Fact]
    public async Task Update_AoSairDaConclusao_LimpaCompletedAt()
    {
        var board = CriarBoard();
        var backlog = CriarStage(board.ProjectId, "Backlog", StageCategory.Ready);
        var concluido = CriarStage(board.ProjectId, "Concluído", StageCategory.Done);
        var item = CriarItem(board, backlog);

        var handler = CriarHandler(item, backlog, concluido);

        await handler.Handle(Comando(item, concluido.Id), CancellationToken.None);
        Assert.NotNull(item.CompletedAt);

        // Voltar para uma coluna aberta precisa reabrir a tarefa.
        await handler.Handle(Comando(item, backlog.Id), CancellationToken.None);
        Assert.Equal(backlog.Id, item.StageId);
        Assert.Null(item.CompletedAt);
    }

    // ── fakes ──────────────────────────────────────────────────────────────

    private sealed class FakeManagementRepo(WorkItem item) : IWorkItemManagementRepository
    {
        public Task<WorkItem?> GetDetailedAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<WorkItem?>(item);
        public Task<WorkItem?> GetEditableAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<WorkItem?>(item);
        public Task<WorkItemLink?> GetLinkAsync(Guid workItemId, Guid linkId, CancellationToken cancellationToken = default)
            => Task.FromResult<WorkItemLink?>(null);
        public Task AddLinkAsync(WorkItemLink link, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<bool> TryAddLinkAcyclicAsync(WorkItemLink link, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
        public Task RemoveLinkAsync(WorkItemLink link, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task AddFollowerAsync(Guid workItemId, string userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveFollowerAsync(Guid workItemId, string userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveWithEventAsync(TaskEvent taskEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeProjectAccess : IProjectAccessService
    {
        public Task<ProjectRole?> GetRoleAsync(Guid projectId, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectRole?>(ProjectRole.ProjectAdmin);
        public Task EnsureAtLeastAsync(Guid projectId, string userId, ProjectRole minimumRole, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakePermissions : IPermissionService
    {
        public Task<bool> HasAsync(string userId, PlatformPermission permission,
            PermissionScope scope = PermissionScope.Organization, Guid? scopeId = null,
            CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task EnsureAsync(string userId, PlatformPermission permission,
            PermissionScope scope = PermissionScope.Organization, Guid? scopeId = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeStages(Stage[] stages) : IStageRepository
    {
        public Task<Stage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(stages.FirstOrDefault(s => s.Id == id));
        public Task<IReadOnlyList<Stage>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Stage>>(stages.Where(s => s.ProjectId == projectId).ToList());
        public Task<Stage> AddAsync(Stage stage, CancellationToken cancellationToken = default) => Task.FromResult(stage);
        public Task UpdateAsync(Stage stage, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Stage stage, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeTeams : ITeamRepository
    {
        public Task<IReadOnlyList<Team>> GetAllWithMembersAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Team>>([]);
        public Task<Team?> GetByIdWithMembersAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Team?>(null);
        public Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<Team?>(null);
        public Task<TeamMember?> GetMemberAsync(Guid teamId, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<TeamMember?>(null);
        public Task<bool> NameExistsAsync(string name, Guid? exceptId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task AddAsync(Team team, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Team team, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveMemberAsync(TeamMember member, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeUsers : IUserDirectory
    {
        public Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(
            IEnumerable<string> userIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        public Task<IReadOnlyList<UserSummary>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserSummary>>([]);
        public Task<IReadOnlyList<UserSummary>> GetByIdsAsync(
            IEnumerable<string> userIds, bool includeInactive = false, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserSummary>>([]);
        public Task<UserSummary?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<UserSummary?>(null);
        public Task<UserSummary?> GetByIdUnscopedAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<UserSummary?>(null);
        public Task<UserSummary?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult<UserSummary?>(null);
    }

    private sealed class FakeFeed : ITaskFeedRepository
    {
        public Task<IReadOnlyList<Comment>> GetCommentsAsync(Guid workItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Comment>>([]);
        public Task<Comment> AddCommentAsync(Comment comment, CancellationToken cancellationToken = default)
            => Task.FromResult(comment);
        public Task<IReadOnlyList<TaskEvent>> GetEventsAsync(Guid workItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TaskEvent>>([]);
        public Task<IReadOnlyList<StageHistory>> GetStageHistoriesAsync(Guid workItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StageHistory>>([]);
        public Task AddEventAsync(TaskEvent taskEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
