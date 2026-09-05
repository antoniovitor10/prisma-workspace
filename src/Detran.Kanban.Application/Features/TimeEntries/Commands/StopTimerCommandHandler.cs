using Detran.Kanban.Application.Features.TimeEntries.Dtos;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Exceptions;
using MediatR;

namespace Detran.Kanban.Application.Features.TimeEntries.Commands;

public class StopTimerCommandHandler : IRequestHandler<StopTimerCommand, TimeEntryDto>
{
    private readonly ITimeEntryRepository _timeEntries;

    public StopTimerCommandHandler(ITimeEntryRepository timeEntries)
    {
        _timeEntries = timeEntries;
    }

    public async Task<TimeEntryDto> Handle(StopTimerCommand request, CancellationToken cancellationToken)
    {
        var running = await _timeEntries.GetRunningByUserAsync(request.UserId, cancellationToken);
        DomainException.Garantir(running is not null, "Não existe timer em andamento.");
        DomainException.Garantir(
            !request.WorkItemId.HasValue || running!.WorkItemId == request.WorkItemId.Value,
            "O timer em andamento pertence a outro card.");

        var tracked = await _timeEntries.GetByIdAsync(running!.Id, cancellationToken);
        DomainException.Garantir(tracked is not null, "Timer em andamento não encontrado.");

        tracked!.Encerrar(request.Note);
        await _timeEntries.UpdateAsync(tracked, cancellationToken);
        return tracked.ToDto();
    }
}
