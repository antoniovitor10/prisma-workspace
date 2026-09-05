namespace Detran.Kanban.Application.Interfaces;

public interface IBoardRealtimeNotifier
{
    Task BoardChangedAsync(
        Guid boardId, string changeType, Guid? workItemId = null,
        CancellationToken ct = default);
}
