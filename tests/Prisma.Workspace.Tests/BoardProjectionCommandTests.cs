using Prisma.Workspace.Application.Features.Boards.Commands;
using Prisma.Workspace.Application.Features.Stages.Commands;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Tests;

public class BoardProjectionCommandTests
{
    [Fact]
    public async Task CreateBoard_ComProjetoValido_CriaBoardEBacklogEConfiguraDefault()
    {
        var project = new Project { Id = Guid.NewGuid(), Name = "Projeto", Key = "PRJ", OwnerId = "admin" };
        var boards = new BoardRepositoryFake();
        var stages = new StageRepositoryFake();
        var projects = new ProjectRepositoryFake(project);
        var access = new ProjectAccessFake();
        var handler = new CreateBoardCommandHandler(boards, stages, projects, access);

        var boardId = await handler.Handle(new CreateBoardCommand("Operação", "admin", project.Id), CancellationToken.None);

        var board = Assert.Single(boards.Added);
        Assert.Equal(boardId, board.Id);
        Assert.Equal(project.Id, board.ProjectId);
        var backlog = Assert.Single(stages.Added);
        Assert.Equal(boardId, backlog.BoardId);
        Assert.Equal("Backlog", backlog.Name);
        Assert.Equal(StageCategory.Ready, backlog.Category);
        Assert.Equal(100, backlog.Position);
        Assert.Equal(boardId, project.DefaultBoardId);
        Assert.Equal(1, projects.SaveCount);
    }

    [Fact]
    public async Task CreateBoard_SemAcessoAoProjeto_RejeitaAntesDePersistir()
    {
        var projectId = Guid.NewGuid();
        var boards = new BoardRepositoryFake();
        var stages = new StageRepositoryFake();
        var projects = new ProjectRepositoryFake();
        var access = new ProjectAccessFake(new UnauthorizedAccessException("Acesso negado"));
        var handler = new CreateBoardCommandHandler(boards, stages, projects, access);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            handler.Handle(new CreateBoardCommand("Proibido", "user", projectId), CancellationToken.None));

        Assert.Empty(boards.Added);
        Assert.Empty(stages.Added);
        Assert.Equal(0, projects.SaveCount);
    }

    [Fact]
    public async Task ReorderStages_AlteraSomenteStagesInformadasDoBoard()
    {
        var board = NewBoard(Guid.NewGuid());
        var first = NewStage(board.Id, 100);
        var second = NewStage(board.Id, 200);
        var untouched = NewStage(board.Id, 300);
        var otherBoardStage = NewStage(Guid.NewGuid(), 400);
        var stages = new StageRepositoryFake(first, second, untouched, otherBoardStage);
        var workItem = new WorkItem { Id = Guid.NewGuid(), BoardId = board.Id, StageId = first.Id, WorkflowStatusId = Guid.NewGuid() };
        var handler = new ReorderStagesCommandHandler(stages, new BoardRepositoryFake(board));

        await handler.Handle(new ReorderStagesCommand(board.Id, [second.Id, first.Id], "admin"), CancellationToken.None);

        Assert.Equal(100, second.Position);
        Assert.Equal(200, first.Position);
        Assert.Equal(300, untouched.Position);
        Assert.Equal(400, otherBoardStage.Position);
        Assert.Equal([second.Id, first.Id], stages.Updated.Select(stage => stage.Id).ToArray());
        Assert.Equal(first.Id, workItem.StageId);
        Assert.NotNull(workItem.WorkflowStatusId);
    }

    [Fact]
    public async Task ReorderStages_ComListaVazia_RejeitaSemUpdate()
    {
        var board = NewBoard(Guid.NewGuid());
        var stages = new StageRepositoryFake(NewStage(board.Id, 100));
        var handler = new ReorderStagesCommandHandler(stages, new BoardRepositoryFake(board));

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new ReorderStagesCommand(board.Id, [], "admin"), CancellationToken.None));

        Assert.Empty(stages.Updated);
    }

    [Fact]
    public async Task ReorderStages_ComStageDeOutroBoard_RejeitaSemUpdate()
    {
        var board = NewBoard(Guid.NewGuid());
        var ownStage = NewStage(board.Id, 100);
        var stages = new StageRepositoryFake(ownStage);
        var handler = new ReorderStagesCommandHandler(stages, new BoardRepositoryFake(board));

        await Assert.ThrowsAsync<DomainException>(() =>
            handler.Handle(new ReorderStagesCommand(board.Id, [Guid.NewGuid()], "admin"), CancellationToken.None));

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
        var backlog = NewStage(destination.Id, 100);
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

    private static Stage NewStage(Guid boardId, double position) => new()
    {
        Id = Guid.NewGuid(), BoardId = boardId, Name = "Etapa", Position = position, CreatedAt = DateTimeOffset.UtcNow
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
        public Task<IReadOnlyList<Stage>> GetByBoardIdAsync(Guid boardId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<Stage>>(_stages.Where(stage => stage.BoardId == boardId).ToList());
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
    }

    private sealed class WorkItemRepositoryFake(params WorkItem[] exclusiveItems) : IWorkItemRepository
    {
        public int UpdateCount { get; private set; }
        public int DeleteCount { get; private set; }
        public Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<WorkItem?>(null);
        public Task<WorkItem?> GetForMoveAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<WorkItem?>(null);
        public Task<IReadOnlyList<WorkItem>> GetByBoardIdAsync(Guid boardId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WorkItem>>([]);
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
