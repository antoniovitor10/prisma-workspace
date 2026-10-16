using Prisma.Workspace.Application.Features.TimeEntries.Dtos;
using Prisma.Workspace.Application.Interfaces;
using MediatR;

namespace Prisma.Workspace.Application.Features.TimeEntries.Commands;

public class StopTimerCommandHandler : IRequestHandler<StopTimerCommand, TimeEntryDto>
{
    private readonly ITimeEntryRepository _timeEntries;

    public StopTimerCommandHandler(ITimeEntryRepository timeEntries)
    {
        _timeEntries = timeEntries;
    }

    public async Task<TimeEntryDto> Handle(StopTimerCommand request, CancellationToken cancellationToken)
    {
        var entry = await _timeEntries.StopTimerAsync(
            request.UserId, request.WorkItemId, request.Note, cancellationToken);
        return entry.ToDto();
    }
}
