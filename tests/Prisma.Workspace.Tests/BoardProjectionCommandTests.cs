using Prisma.Workspace.Application.Features.Boards.Commands;
using Prisma.Workspace.Application.Features.Stages.Commands;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;

namespace Prisma.Workspace.Tests;

public class BoardProjectionCommandTests
{
    [Fact]
    public async Task CreateBoard_ComProjetoValido_CriaBoardEConfiguraDefaultSemCriarStage()
    {
        var project = new Project { Id = Guid.NewGuid(), Name = "Projeto", Key = "PRJ", OwnerId = "admin" };
        var boards = new BoardRepositoryFake();
        var projects = new ProjectRepositoryFake(project);
        var access = new ProjectAccessFake();
        var handler = new CreateBoardCommandHandler(boards, projects, access);

        var boardId = await handler.Handle(new CreateBoardCommand("Operação", "admin", project.Id), CancellationToken.None);

        var board = Assert.Single(boards.Added);
        Assert.Equal(boardId, board.Id);
        Assert.Equal(project.Id, board.ProjectId);
        Assert.Equal(boardId, project.DefaultBoardId);
        Assert.Equal(1, projects.SaveCount);
    }

    [Fact]
    public async Task CreateBoard_SemAcessoAoProjeto_RejeitaAntesDePersistir()
    {
        var projectId = Guid.NewGuid();
        var boards = new BoardRepositoryFake();
        var projects = new ProjectRepositoryFake();
        var access = new ProjectAccessFake(new UnauthorizedAccessException("Acesso negado"));
        var handler = new CreateBoardCommandHandler(boards, projects, access);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new CreateBoardCommand("Proibido", "user", projectId), CancellationToken.None));

        Assert.Empty(boards.Added);
        Assert.Equal(0, projects.SaveCount);
    }

    [Fact]
    public async Task ReorderStages_AlteraSomenteStagesInformadasDoProjeto()
    {
        var project = new Project { Id = Guid.NewGuid(), Name = "Projeto", Key = "PRJ", OwnerId = "admin" };
        var first = NewStage(project.Id, 100);
        var second = NewStage(project.Id, 200);
        var untouched = NewStage(project.Id, 300);
        var otherProjectStage = NewStage(Guid.NewGuid(), 400);
        var stages = new StageRepositoryFake(first, second, untouched, otherProjectStage);
        var workItem = new WorkItem { Id = Guid.NewGuid(), BoardId = Guid.NewGuid(), StageId = first.Id, WorkflowStatusId = Guid.NewGuid() };
        var handler = new ReorderStagesCommandHandler(stages, new ProjectRepositoryFake(project));

        await handler.Handle(new ReorderStagesCommand(project.Id, [second.Id, first.Id], "admin"), CancellationToken.None);

        Assert.Equal(100, second.Position);
        Assert.Equal(200, first.Position);
        Assert.Equal(300, untouched.Position);
        Assert.Equal(400, otherProjectStage.Position);
        Assert.Equal([second.Id, first.Id], stages.Updated.Select(stage => stage.Id).ToArray());
        Assert.Equal(first.Id, workItem.StageId);
        Assert.NotNull(workItem.WorkflowStatusId);
    }

    [Fact]
    public async Task ReorderStages_ComListaVazia_RejeitaSemUpdate()
    {
        var project = new Project { Id = Guid.NewGuid(), Name = "Projeto", Key = "PRJ", OwnerId = "admin" };
        var stages = new StageRepositoryFake(NewStage(project.Id, 100));
        var handler = new ReorderStagesCommandHandler(stages, new ProjectRepositoryFake(project));

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new ReorderStagesCommand(project.Id, [], "admin"), CancellationToken.None));

        Assert.Empty(stages.Updated);
    }

    [Fact]
    public async Task ReorderStages_ComStageDeOutroProjeto_RejeitaSemUpdate()
    {
        var project = new Project { Id = Guid.NewGuid(), Name = "Projeto", Key = "PRJ", OwnerId = "admin" };
        var ownStage = NewStage(project.Id, 100);
        var stages = new StageRepositoryFake(ownStage);
        var handler = new ReorderStagesCommandHandler(stages, new ProjectRepositoryFake(project));

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new ReorderStagesCommand(project.Id, [Guid.NewGuid()], "admin"), CancellationToken.None));

        Assert.Empty(stages.Updated);
        Assert.Equal(100, ownStage.Position);
    }

    [Fact]
    public async Task DeleteBoard_ComItemExclusivo_RealocaSemDeletarWorkItem()
    {
        var project = new Project { Id = Guid.NewGuid(), Name = "Projeto", Key = "PRJ", OwnerId = "admin" };
        var source = NewBoard(project.Id);
        var destination = NewBoard(project.Id);
        project.DefaultBoardId = source.Id;
        var backlog = NewStage(project.Id, 100);
        backlog.Name = "Backlog";
        backlog.Category = StageCategory.Ready;
        var item = new WorkItem { Id = Guid.NewGuid(), BoardId = source.Id, StageId = Guid.NewGuid(), Position = 50 };
        var boards = new BoardRepositoryFake(source, destination);
        var workItems = new WorkItemRepositoryFake(item);
        var projects = new ProjectRepositoryFake(project);
        var handler = new DeleteBoardCommandHandler(boards, workItems, new StageRepositoryFake(backlog), projects, new ProjectAccessFake());

        await handler.Handle(new DeleteBoardCommand(source.Id, "admin", destination.Id), CancellationToken.None);

        Assert.Equal(destination.Id, item.BoardId);
        Assert.Equal(backlog.Id, item.StageId);
        Assert.Equal(1, workItems.UpdateCount);
        Assert.Equal(0, workItems.DeleteCount);
        Assert.Equal(source, Assert.Single(boards.Deleted));
        Assert.Equal(destination.Id, project.DefaultBoardId);
    }

    private static Board NewBoard(Guid projectId) => new()
    {
        Id = Guid.NewGuid(), ProjectId = projectId, Name = "Quadro", CreatedAt = DateTimeOffset.UtcNow
    };

    private static Stage NewStage(Guid projectId, double position) => new()
    {
        Id = Guid.NewGuid(), ProjectId = projectId, Name = "Etapa", Position = position, CreatedAt = DateTimeOffset.UtcNow
    };

    private sealed class BoardRepositoryFake(params Board[] boards) : IBoardRepository
    {
        private readonly List<Board> _boards = boards.ToList();
        public List<Board> Added { get; } = [];
        public List<Board> Deleted { get; } = [];
        public Task<Board?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_boards.FirstOrDefault(board => board.Id == id));
        public Task<IReadOnlyList<Board>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Board>>(_boards);
        public Task<IReadOnlyList<Board>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Board>>(_boards.Where(board => board.ProjectId == projectId).ToList());
        public Task<Board> AddAsync(Board board, CancellationToken cancellationToken = default) { Added.Add(board); _boards.Add(board); return Task.FromResult(board); }
        public Task UpdateAsync(Board board, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task DeleteAsync(Board board, CancellationToken cancellationToken = default) { Deleted.Add(board); return Task.CompletedTask; }
    }

    private sealed class StageRepositoryFake(params Stage[] stages) : IStageRepository
    {
        private readonly List<Stage> _stages = stages.ToList();
        public List<Stage> Added { get; } = [];
        public List<Stage> Updated { get; } = [];
        public Task<Stage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(_stages.FirstOrDefault(stage => stage.Id == id));
        public Task<IReadOnlyList<Stage>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Stage>>(_stages.Where(stage => stage.ProjectId == projectId).ToList());
        public Task<Stage> AddAsync(Stage stage, CancellationToken cancellationToken = default) { Added.Add(stage); _stages.Add(stage); return Task.FromResult(stage); }
        public Task UpdateAsync(Stage stage, CancellationToken cancellationToken = default) { Updated.Add(stage); return Task.CompletedTask; }
        public Task DeleteAsync(Stage stage, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class ProjectRepositoryFake(params Project[] projects) : IProjectRepository
    {
        public int SaveCount { get; private set; }
        public Task<IReadOnlyList<Project>> GetForUserAsync(string userId, bool includeArchived = false, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Project>>(projects);
        public Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult(projects.FirstOrDefault(project => project.Id == id));
        public Task<Project?> GetByIdWithMembersAsync(Guid id, CancellationToken cancellationToken = default) => GetByIdAsync(id, cancellationToken);
        public Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task AddAsync(Project project, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void AddCustomField(ProjectCustomFieldDefinition field) { }
        public void AddEvent(ProjectEvent projectEvent) { }
        public Task SaveAsync(CancellationToken cancellationToken = default) { SaveCount++; return Task.CompletedTask; }
    }

    private sealed class ProjectAccessFake(Exception? failure = null) : IProjectAccessService
    {
        public Task<ProjectRole?> GetRoleAsync(Guid projectId, string userId, CancellationToken cancellationToken = default) => Task.FromResult<ProjectRole?>(failure is null ? ProjectRole.ProjectAdmin : null);
        public Task EnsureAtLeastAsync(Guid projectId, string userId, ProjectRole minimumRole, CancellationToken cancellationToken = default) => failure is null ? Task.CompletedTask : Task.FromException(failure);
        public Task<IReadOnlySet<Guid>> GetAccessibleProjectIdsAsync(IEnumerable<Guid> projectIds, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlySet<Guid>>(projectIds.ToHashSet());
    }

    private sealed class WorkItemRepositoryFake(params WorkItem[] exclusiveItems) : IWorkItemRepository
    {
        public int UpdateCount { get; private set; }
        public int DeleteCount { get; private set; }
        public Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<WorkItem?>(null);
        public Task<WorkItem?> GetForMoveAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<WorkItem?>(null);
        public Task<IReadOnlyList<WorkItem>> GetByBoardIdAsync(Guid boardId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WorkItem>>([]);
        public Task<IReadOnlyList<WorkItem>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WorkItem>>([]);
        public Task<IReadOnlyList<WorkItem>> GetSubItemsAsync(Guid parentId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WorkItem>>([]);
        public Task<IReadOnlyList<WorkItemAssignee>> GetAssigneesAsync(Guid workItemId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WorkItemAssignee>>([]);
        public Task<WorkItem> AddAsync(WorkItem workItem, CancellationToken cancellationToken = default) => Task.FromResult(workItem);
        public Task UpdateAsync(WorkItem workItem, CancellationToken cancellationToken = default) { UpdateCount++; return Task.CompletedTask; }
        public Task UpdateWithEventAsync(WorkItem workItem, TaskEvent taskEvent, CancellationToken cancellationToken = default) { UpdateCount++; return Task.CompletedTask; }
        public Task DeleteAsync(WorkItem workItem, CancellationToken cancellationToken = default) { DeleteCount++; return Task.CompletedTask; }
        public Task AddAssigneeAsync(Guid workItemId, string userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task RemoveAssigneeAsync(Guid workItemId, string userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<IReadOnlyList<WorkItem>> GetAssignedToUserAsync(string userId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WorkItem>>([]);
        public Task<IReadOnlyList<WorkItem>> GetExclusiveToBoardAsync(Guid boardId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WorkItem>>(exclusiveItems);
        public Task UpdatePersonalPrioritiesAsync(string userId, IReadOnlyList<Guid> orderedWorkItemIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
