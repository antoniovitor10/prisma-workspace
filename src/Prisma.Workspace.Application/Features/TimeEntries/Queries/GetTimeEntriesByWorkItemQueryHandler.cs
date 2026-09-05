using Prisma.Workspace.Application.Features.TimeEntries.Dtos;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.WorkItems;
using Prisma.Workspace.Domain.Authorization;
using Prisma.Workspace.Domain.Enums;
using MediatR;

namespace Prisma.Workspace.Application.Features.TimeEntries.Queries;

public class GetTimeEntriesByWorkItemQueryHandler
    : IRequestHandler<GetTimeEntriesByWorkItemQuery, IReadOnlyList<TimeEntryDto>>
{
    private readonly ITimeEntryRepository _timeEntries;
    private readonly IWorkItemRepository _workItems;
    private readonly IProjectAccessService _projectAccess;
    private readonly IPermissionService _permissions;
    private readonly IUserDirectory _users;

    public GetTimeEntriesByWorkItemQueryHandler(
        ITimeEntryRepository timeEntries,
        IWorkItemRepository workItems,
        IProjectAccessService projectAccess,
        IPermissionService permissions,
        IUserDirectory users)
    {
        _timeEntries = timeEntries;
        _workItems = workItems;
        _projectAccess = projectAccess;
        _permissions = permissions;
        _users = users;
    }

    public async Task<IReadOnlyList<TimeEntryDto>> Handle(
        GetTimeEntriesByWorkItemQuery request,
        CancellationToken cancellationToken)
    {
        var item = await _workItems.GetByIdAsync(request.WorkItemId, cancellationToken)
            ?? throw new NaoEncontradoException("Tarefa");
        await WorkItemAccessGuard.EnsureAsync(
            item, request.ActorId, ProjectRole.Viewer, PlatformPermission.View,
            _projectAccess, _permissions, cancellationToken);
        var entries = await _timeEntries.GetByWorkItemIdAsync(request.WorkItemId, cancellationToken);
        var names = (await _users.GetByIdsAsync(
            entries.Select(entry => entry.UserId), includeInactive: true, cancellationToken))
            .ToDictionary(user => user.Id, user => user.DisplayName);
        return entries.Select(entry => entry.ToDto(
            names.GetValueOrDefault(entry.UserId, UserDisplayName.Resolve(entry.UserId, null, null, null))))
            .ToList();
    }
}
