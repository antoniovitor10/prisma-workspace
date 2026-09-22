using Prisma.Workspace.Application.Features.WorkItems.Commands;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Tests;

public class MoveWorkItemCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenStageChanges_ClosesCurrentHistoryAndAddsNewHistoryForInsert()
    {
        var projectId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var sourceStageId = Guid.NewGuid();
        var destinationStageId = Guid.NewGuid();

        var currentHistory = new StageHistory
        {
            Id = Guid.NewGuid(),
            WorkItemId = Guid.NewGuid(),
            StageId = sourceStageId,
            EnteredAt = DateTimeOffset.UtcNow.AddDays(-1)
        };

        var workItem = new WorkItem
        {
            Id = currentHistory.WorkItemId,
            BoardId = boardId,
            Board = new Board { Id = boardId, ProjectId = projectId, Name = "Quadro" },
            StageId = sourceStageId,
            Title = "Card",
            Position = 100,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2),
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        workItem.StageHistories.Add(currentHistory);

        var workItemRepository = new FakeWorkItemRepository(workItem);
        var stageRepository = new FakeStageRepository(new Stage
        {
            Id = destinationStageId,
            ProjectId = projectId,
            BoardId = boardId,
            Name = "Destino",
            Position = 200,
            CreatedAt = DateTimeOffset.UtcNow
        });

        var handler = new MoveWorkItemCommandHandler(workItemRepository, stageRepository, new FakeTaskFeedRepository());

        await handler.Handle(new MoveWorkItemCommand(workItem.Id, destinationStageId, 200, "user-1", "PO", "Pronto para revisão"), CancellationToken.None);

        Assert.Equal(destinationStageId, workItem.StageId);
        Assert.Equal(200, workItem.Position);
        Assert.NotNull(currentHistory.LeftAt);
        Assert.Same(workItem, workItemRepository.UpdatedWorkItem);

        var newHistory = Assert.Single(workItem.StageHistories, h => h.StageId == destinationStageId);
        Assert.Equal(Guid.Empty, newHistory.Id);
        Assert.Equal(workItem.Id, newHistory.WorkItemId);
        Assert.Null(newHistory.LeftAt);
        Assert.Equal("user-1", newHistory.ActorId);
        Assert.Equal("PO", newHistory.ActorName);
        Assert.Equal("Pronto para revisão", newHistory.Reason);
        Assert.NotNull(workItemRepository.SavedEvent);
        Assert.Equal("stage_changed", workItemRepository.SavedEvent!.Kind);
        using var payload = System.Text.Json.JsonDocument.Parse(workItemRepository.SavedEvent.Payload!);
        Assert.Equal("Pronto para revisão", payload.RootElement.GetProperty("reason").GetString());
    }

    private sealed class FakeWorkItemRepository : IWorkItemRepository
    {
        private readonly WorkItem _workItem;

        public FakeWorkItemRepository(WorkItem workItem)
        {
            _workItem = workItem;
        }

        public WorkItem? UpdatedWorkItem { get; private set; }
        public TaskEvent? SavedEvent { get; private set; }

        public Task<WorkItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(id == _workItem.Id ? _workItem : null);
        }

        public Task<WorkItem?> GetForMoveAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(id == _workItem.Id ? _workItem : null);
        }

        public Task<IReadOnlyList<WorkItem>> GetByBoardIdAsync(Guid boardId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<WorkItem>> GetByProjectIdAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<WorkItem>> GetSubItemsAsync(Guid parentId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<WorkItemAssignee>> GetAssigneesAsync(Guid workItemId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<WorkItem> AddAsync(WorkItem workItem, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(WorkItem workItem, CancellationToken cancellationToken = default)
        {
            UpdatedWorkItem = workItem;
            return Task.CompletedTask;
        }

        public Task UpdateWithEventAsync(
            WorkItem workItem,
            TaskEvent taskEvent,
            CancellationToken cancellationToken = default)
        {
            UpdatedWorkItem = workItem;
            SavedEvent = taskEvent;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(WorkItem workItem, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task AddAssigneeAsync(Guid workItemId, string userId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task RemoveAssigneeAsync(Guid workItemId, string userId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<WorkItem>> GetAssignedToUserAsync(string userId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task UpdatePersonalPrioritiesAsync(string userId, IReadOnlyList<Guid> orderedWorkItemIds, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<WorkItem>> GetExclusiveToBoardAsync(Guid boardId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<WorkItem>>(new List<WorkItem>());
    }

    private sealed class FakeTaskFeedRepository : ITaskFeedRepository
    {
        public Task<IReadOnlyList<Comment>> GetCommentsAsync(Guid workItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Comment>>(new List<Comment>());
        public Task<Comment> AddCommentAsync(Comment comment, CancellationToken cancellationToken = default)
            => Task.FromResult(comment);
        public Task<IReadOnlyList<TaskEvent>> GetEventsAsync(Guid workItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TaskEvent>>(new List<TaskEvent>());
        public Task<IReadOnlyList<StageHistory>> GetStageHistoriesAsync(Guid workItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<StageHistory>>(new List<StageHistory>());
        public Task AddEventAsync(TaskEvent taskEvent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeStageRepository : IStageRepository
    {
        private readonly Stage _stage;

        public FakeStageRepository(Stage stage)
        {
            _stage = stage;
        }

        public Task<Stage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(id == _stage.Id ? _stage : null);
        }

        public Task<IReadOnlyList<Stage>> GetByProjectIdAsync(Guid boardId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<Stage>> GetByBoardIdAsync(Guid boardId, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<Stage> list = _stage.BoardId == boardId ? [_stage] : [];
            return Task.FromResult(list);
        }

        public Task<Stage> AddAsync(Stage stage, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task UpdateAsync(Stage stage, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAsync(Stage stage, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }
    }
}
