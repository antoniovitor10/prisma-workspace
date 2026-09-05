using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.TimeEntries.Dtos;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Application.Features.WorkItems;
using Prisma.Workspace.Domain.Authorization;
using Prisma.Workspace.Domain.Enums;
using MediatR;

namespace Prisma.Workspace.Application.Features.TimeEntries.Commands;

public class CreateManualTimeEntryCommandHandler
    : IRequestHandler<CreateManualTimeEntryCommand, TimeEntryDto>
{
    private readonly ITimeEntryRepository _timeEntries;
    private readonly IWorkItemRepository _workItems;
    private readonly IProjectAccessService _projectAccess;
    private readonly IPermissionService _permissions;

    public CreateManualTimeEntryCommandHandler(
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

    public async Task<TimeEntryDto> Handle(CreateManualTimeEntryCommand request, CancellationToken cancellationToken)
    {
        var item = await _workItems.GetByIdAsync(request.WorkItemId, cancellationToken)
            ?? throw new NaoEncontradoException("Tarefa");
        await WorkItemAccessGuard.EnsureAsync(
            item, request.UserId, ProjectRole.Member, PlatformPermission.Edit,
            _projectAccess, _permissions, cancellationToken);

        var entry = TimeEntry.Manual(
            request.WorkItemId, request.UserId, request.StartedAt, request.EndedAt, request.Note);
        await _timeEntries.AddAsync(entry, cancellationToken);
        return entry.ToDto();
    }
}
