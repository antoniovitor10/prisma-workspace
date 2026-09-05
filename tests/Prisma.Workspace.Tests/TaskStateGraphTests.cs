using Prisma.Workspace.Application.Features.TaskFeed.Queries;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Tests;

public class TaskStateGraphTests
{
    [Fact]
    public async Task Query_BuildsNodesDurationsAndAuditedEdgesFromRealHistory()
    {
        var workItemId = Guid.NewGuid();
        var todo = new Stage { Id = Guid.NewGuid(), Name = "A fazer" };
        var doing = new Stage { Id = Guid.NewGuid(), Name = "Em andamento" };
        var entered = DateTimeOffset.UtcNow.AddHours(-3);
        var feed = new FeedFake([
            new StageHistory
            {
                Id = Guid.NewGuid(), WorkItemId = workItemId, StageId = todo.Id, Stage = todo,
                EnteredAt = entered, LeftAt = entered.AddHours(1),
            },
            new StageHistory
            {
                Id = Guid.NewGuid(), WorkItemId = workItemId, StageId = doing.Id, Stage = doing,
                EnteredAt = entered.AddHours(1), ActorId = "user-1", ActorName = "PO",
                Reason = "Início autorizado",
            },
        ]);
        var handler = new GetTaskStateGraphQueryHandler(feed, new AccessFake());

        var result = await handler.Handle(new GetTaskStateGraphQuery(workItemId, "user-1"), default);

        Assert.Equal(2, result.Nodes.Count);
        Assert.Contains(result.Nodes, node => node.Id == todo.Id && node.TotalSeconds == 3600 && !node.IsCurrent);
        Assert.Contains(result.Nodes, node => node.Id == doing.Id && node.IsCurrent);
        var edge = Assert.Single(result.Edges);
        Assert.Equal(todo.Id, edge.Source);
        Assert.Equal(doing.Id, edge.Target);
        Assert.Equal("PO", edge.ActorName);
        Assert.Equal("Início autorizado", edge.Reason);
    }

    private sealed class AccessFake : IWorkItemAccessService
    {
        public Task EnsureAsync(Guid workItemId, string userId, PlatformPermission permission,
            ProjectRole minimumRole = ProjectRole.Viewer, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FeedFake(IReadOnlyList<StageHistory> histories) : ITaskFeedRepository
    {
        public Task<IReadOnlyList<StageHistory>> GetStageHistoriesAsync(Guid workItemId, CancellationToken cancellationToken = default)
            => Task.FromResult(histories);
        public Task<IReadOnlyList<Comment>> GetCommentsAsync(Guid workItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Comment>>([]);
        public Task<Comment> AddCommentAsync(Comment comment, CancellationToken cancellationToken = default)
            => Task.FromResult(comment);
        public Task<IReadOnlyList<TaskEvent>> GetEventsAsync(Guid workItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TaskEvent>>([]);
        public Task AddEventAsync(TaskEvent taskEvent, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
