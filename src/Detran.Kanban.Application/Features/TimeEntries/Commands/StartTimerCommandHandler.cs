using Detran.Kanban.Application.Common.Exceptions;
using Detran.Kanban.Application.Features.TimeEntries.Dtos;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Application.Features.WorkItems;
using Detran.Kanban.Domain.Authorization;
using Detran.Kanban.Domain.Enums;
using MediatR;

namespace Detran.Kanban.Application.Features.TimeEntries.Commands;

/// <summary>
/// Regra do timer único: cada usuário tem no máximo um cronômetro aberto.
/// Iniciar em outra tarefa encerra o anterior; iniciar na mesma tarefa
/// devolve o cronômetro já aberto (idempotente).
/// </summary>
public class StartTimerCommandHandler : IRequestHandler<StartTimerCommand, TimeEntryDto>
{
    private readonly ITimeEntryRepository _timeEntries;
    private readonly IWorkItemRepository _workItems;
    private readonly IProjectAccessService _projectAccess;
    private readonly IPermissionService _permissions;

    public StartTimerCommandHandler(
        ITimeEntryRepository timeEntries,
        IWorkItemRepository workItems,
        IProjectAccessService projectAccess,
        IPermissionService permissions)
    {
        _timeEntries = timeEntries;
        _workItems = workItems;
        _projectAccess = projectAccess;
        _permissions = permissions;
    }

    public async Task<TimeEntryDto> Handle(StartTimerCommand request, CancellationToken cancellationToken)
    {
        var item = await _workItems.GetByIdAsync(request.WorkItemId, cancellationToken)
            ?? throw new NaoEncontradoException("Tarefa");
        await WorkItemAccessGuard.EnsureAsync(
            item, request.UserId, ProjectRole.Member, PlatformPermission.Edit,
            _projectAccess, _permissions, cancellationToken);

        var running = await _timeEntries.GetRunningByUserAsync(request.UserId, cancellationToken);
        if (running is not null)
        {
            if (running.WorkItemId == request.WorkItemId) return running.ToDto();

            var tracked = await _timeEntries.GetByIdAsync(running.Id, cancellationToken);
            if (tracked is not null)
            {
                tracked.Encerrar();
                await _timeEntries.UpdateAsync(tracked, cancellationToken);
            }
        }

        var entry = TimeEntry.IniciarAgora(request.WorkItemId, request.UserId, request.Note);
        await _timeEntries.AddAsync(entry, cancellationToken);
        return entry.ToDto();
    }
}
