using Prisma.Workspace.Application.Features.TimeEntries.Dtos;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Domain.Enums;
using MediatR;

namespace Prisma.Workspace.Application.Features.TimeEntries.Queries;

public class GetTimeTotalQueryHandler : IRequestHandler<GetTimeTotalQuery, TimeTotalDto>
{
    private readonly ITimeEntryRepository _timeEntries;
    private readonly IWorkItemAccessService _workItemAccess;
    private readonly IBoardAccessService _boardAccess;
    private readonly IPermissionService _permissions;

    public GetTimeTotalQueryHandler(
        ITimeEntryRepository timeEntries,
        IWorkItemAccessService workItemAccess,
        IBoardAccessService boardAccess,
        IPermissionService permissions)
    {
        _timeEntries = timeEntries;
        _workItemAccess = workItemAccess;
        _boardAccess = boardAccess;
        _permissions = permissions;
    }

    public async Task<TimeTotalDto> Handle(GetTimeTotalQuery request, CancellationToken cancellationToken)
    {
        int totalSeconds;
        if (request.WorkItemId.HasValue)
        {
            await _workItemAccess.EnsureAsync(request.WorkItemId.Value, request.ActorId,
                PlatformPermission.View, ProjectRole.Viewer, cancellationToken);
            totalSeconds = await _timeEntries.GetTotalSecondsByWorkItemIdAsync(request.WorkItemId.Value, cancellationToken);
        }
        else if (!string.IsNullOrEmpty(request.UserId))
        {
            if (!string.Equals(request.UserId, request.ActorId, StringComparison.Ordinal))
                await _permissions.EnsureAsync(request.ActorId, PlatformPermission.ViewReport,
                    PermissionScope.Organization, cancellationToken: cancellationToken);
            totalSeconds = await _timeEntries.GetTotalSecondsByUserIdAsync(request.UserId, cancellationToken);
        }
        else if (request.BoardId.HasValue)
        {
            await _boardAccess.EnsureAsync(request.BoardId.Value, request.ActorId,
                PlatformPermission.View, ProjectRole.Viewer, cancellationToken);
            totalSeconds = await _timeEntries.GetTotalSecondsByBoardIdAsync(request.BoardId.Value, cancellationToken);
        }
        else
            throw new DomainException("Informe tarefa, usuário ou quadro para totalizar.");

        return new TimeTotalDto
        {
            WorkItemId = request.WorkItemId,
            BoardId = request.BoardId,
            UserId = request.UserId,
            TotalSeconds = totalSeconds,
            TotalHours = Math.Round(totalSeconds / 3600m, 2)
        };
    }
}
