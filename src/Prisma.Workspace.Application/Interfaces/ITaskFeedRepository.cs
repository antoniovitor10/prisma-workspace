using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Application.Interfaces;

/// <summary>
/// Repositorio do feed da tarefa: comentarios de usuarios + eventos de sistema.
/// </summary>
public interface ITaskFeedRepository
{
    Task<IReadOnlyList<Comment>> GetCommentsAsync(Guid workItemId, CancellationToken cancellationToken = default);
    Task<Comment> AddCommentAsync(Comment comment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskEvent>> GetEventsAsync(Guid workItemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StageHistory>> GetStageHistoriesAsync(Guid workItemId, CancellationToken cancellationToken = default);
    Task AddEventAsync(TaskEvent taskEvent, CancellationToken cancellationToken = default);
}
