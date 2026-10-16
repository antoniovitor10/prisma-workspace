using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.TimeEntries.Dtos;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Application.Features.WorkItems;
using Prisma.Workspace.Domain.Authorization;
using Prisma.Workspace.Domain.Enums;
using MediatR;

namespace Prisma.Workspace.Application.Features.TimeEntries.Commands;

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

        var entry = await _timeEntries.StartTimerAsync(
            request.WorkItemId, request.UserId, request.Note, cancellationToken);
        return entry.ToDto();
    }
}
