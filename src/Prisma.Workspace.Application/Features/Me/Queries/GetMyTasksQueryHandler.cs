using Prisma.Workspace.Application.Features.Me.Dtos;
using Prisma.Workspace.Application.Interfaces;
using MediatR;

namespace Prisma.Workspace.Application.Features.Me.Queries;

public class GetMyTasksQueryHandler : IRequestHandler<GetMyTasksQuery, IReadOnlyList<MeTaskDto>>
{
    private readonly IWorkItemRepository _workItems;
    private readonly IProjectAccessService _projectAccess;

    public GetMyTasksQueryHandler(IWorkItemRepository workItems, IProjectAccessService projectAccess)
    {
        _workItems = workItems;
        _projectAccess = projectAccess;
    }

    public async Task<IReadOnlyList<MeTaskDto>> Handle(GetMyTasksQuery request, CancellationToken cancellationToken)
    {
        var items = await _workItems.GetAssignedToUserAsync(request.UserId, cancellationToken);
        var accessibleProjectIds = await _projectAccess.GetAccessibleProjectIdsAsync(
            items.Where(x => x.Board is not null).Select(x => x.Board.ProjectId),
            request.UserId, cancellationToken);
        items = items.Where(x => x.Board is not null
            && accessibleProjectIds.Contains(x.Board.ProjectId)).ToList();
        var now = DateTimeOffset.UtcNow;

        return items
            .Select(w =>
            {
                var mine = w.Assignees.First(a => a.UserId == request.UserId);
                return new MeTaskDto
                {
                    Id = w.Id,
                    BoardId = w.BoardId,
                    BoardName = w.Board?.Name ?? string.Empty,
                    StageId = w.StageId,
                    StageName = w.Stage?.Name,
                    Title = w.Title,
                    Subtitle = w.Subtitle,
                    Priority = (int)w.Priority,
                    EstimatedHours = w.EstimatedHours,
                    DueDate = w.DueDate?.ToString("yyyy-MM-dd"),
                    PersonalPriority = mine.PersonalPriority,
                    AssignedAt = mine.AssignedAt,
                    TotalTimeSeconds = w.TimeEntries.Sum(e =>
                        Math.Max(0, (int)((e.EndedAt ?? now) - e.StartedAt).TotalSeconds)),
                    UserTimeSeconds = w.TimeEntries
                        .Where(e => e.UserId == request.UserId)
                        .Sum(e => Math.Max(0, (int)((e.EndedAt ?? now) - e.StartedAt).TotalSeconds))
                };
            })
            .OrderBy(t => t.PersonalPriority == 0 ? int.MaxValue : t.PersonalPriority)
            .ThenBy(t => t.AssignedAt)
            .ToList();
    }
}
