using Prisma.Workspace.Application.Features.Me.Dtos;
using Prisma.Workspace.Application.Interfaces;
using MediatR;

namespace Prisma.Workspace.Application.Features.Me.Queries;

public class GetMyActiveTimerQueryHandler : IRequestHandler<GetMyActiveTimerQuery, ActiveTimerDto?>
{
    private readonly ITimeEntryRepository _timeEntries;
    private readonly IWorkItemRepository _workItems;

    public GetMyActiveTimerQueryHandler(ITimeEntryRepository timeEntries, IWorkItemRepository workItems)
    {
        _timeEntries = timeEntries;
        _workItems = workItems;
    }

    public async Task<ActiveTimerDto?> Handle(GetMyActiveTimerQuery request, CancellationToken cancellationToken)
    {
        var running = await _timeEntries.GetRunningByUserAsync(request.UserId, cancellationToken);
        if (running is null) return null;

        var workItem = await _workItems.GetByIdAsync(running.WorkItemId, cancellationToken);
        return new ActiveTimerDto
        {
            Id = running.Id,
            WorkItemId = running.WorkItemId,
            WorkItemTitle = workItem?.Title,
            StartedAt = running.StartedAt
        };
    }
}
