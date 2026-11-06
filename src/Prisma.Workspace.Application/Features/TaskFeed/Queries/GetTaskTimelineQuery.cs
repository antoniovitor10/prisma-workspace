using MediatR;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Features.TaskFeed.Queries;

public sealed record TaskTimelineEntryDto(Guid Id, string Kind, DateTimeOffset OccurredAt,
    string? ActorId, string? ActorName, Guid? FromColumnId, string? FromColumnName, Guid ToColumnId, string ToColumnName);
public sealed record TaskTimelineDto(IReadOnlyList<TaskTimelineEntryDto> Items, bool UsesCurrentColumnNames);
public sealed record GetTaskTimelineQuery(Guid WorkItemId, string ActorId) : IRequest<TaskTimelineDto>;

public sealed class GetTaskTimelineQueryHandler(ITaskFeedRepository feed, IWorkItemAccessService access)
    : IRequestHandler<GetTaskTimelineQuery, TaskTimelineDto>
{
    public async Task<TaskTimelineDto> Handle(GetTaskTimelineQuery request, CancellationToken ct)
    {
        await access.EnsureAsync(request.WorkItemId, request.ActorId, PlatformPermission.View, ProjectRole.Viewer, ct);
        var histories = (await feed.GetStageHistoriesAsync(request.WorkItemId, ct))
            .OrderBy(history => history.EnteredAt).ThenBy(history => history.Id).ToList();
        var items = new List<TaskTimelineEntryDto>();
        for (var index = 0; index < histories.Count; index++)
        {
            var current = histories[index];
            var previous = index == 0 ? null : histories[index - 1];
            // Reclassificação/conclusão sem troca de coluna não é movimentação visual.
            if (previous is not null && previous.StageId == current.StageId) continue;
            // História antiga pode começar na primeira entrada após criação no backlog.
            var initialKind = current.WorkItem?.CreatedAt == current.EnteredAt ? "created_in_column" : "column_entered";
            items.Add(new(current.Id, previous is null ? initialKind : "column_moved", current.EnteredAt,
                current.ActorId, current.ActorName, previous?.StageId, previous?.Stage?.Name,
                current.StageId, current.Stage?.Name ?? "Coluna não disponível"));
        }
        // O modelo existente não guarda snapshot de nomes; informar o fallback, sem reescrever história.
        return new(items.OrderByDescending(item => item.OccurredAt).ThenByDescending(item => item.Id).ToList(), true);
    }
}
