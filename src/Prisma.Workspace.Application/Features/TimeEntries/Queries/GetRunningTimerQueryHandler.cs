using Prisma.Workspace.Application.Features.TimeEntries.Dtos;
using Prisma.Workspace.Application.Interfaces;
using MediatR;

namespace Prisma.Workspace.Application.Features.TimeEntries.Queries;

public class GetRunningTimerQueryHandler : IRequestHandler<GetRunningTimerQuery, TimeEntryDto?>
{
    private readonly ITimeEntryRepository _timeEntries;

    public GetRunningTimerQueryHandler(ITimeEntryRepository timeEntries)
    {
        _timeEntries = timeEntries;
    }

    public async Task<TimeEntryDto?> Handle(GetRunningTimerQuery request, CancellationToken cancellationToken)
    {
        var running = await _timeEntries.GetRunningByUserAsync(request.UserId, cancellationToken);
        return running?.ToDto();
    }
}
