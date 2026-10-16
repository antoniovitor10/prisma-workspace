using Prisma.Workspace.Application.Common.Exceptions;
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
    private readonly IHoursReadAccessService _hours;

    public GetTimeTotalQueryHandler(
        ITimeEntryRepository timeEntries,
        IWorkItemAccessService workItemAccess,
        IBoardAccessService boardAccess,
        IPermissionService permissions, IHoursReadAccessService hours)
    {
        _timeEntries = timeEntries;
        _workItemAccess = workItemAccess;
        _boardAccess = boardAccess;
        _permissions = permissions;
        _hours = hours;
    }

    public async Task<TimeTotalDto> Handle(GetTimeTotalQuery request, CancellationToken cancellationToken)
    {
        var scope = await _hours.GetScopeAsync(request.ActorId,false,cancellationToken);
        if (request.WorkItemId.HasValue)
        {
            await _workItemAccess.EnsureAsync(request.WorkItemId.Value, request.ActorId,
                PlatformPermission.View, ProjectRole.Viewer, cancellationToken);

        }
        else if (!string.IsNullOrEmpty(request.UserId))
        {
            if (!string.Equals(request.UserId, request.ActorId, StringComparison.Ordinal))
                await _permissions.EnsureAsync(request.ActorId, PlatformPermission.ViewReport,
                    PermissionScope.Organization, cancellationToken: cancellationToken);
            if (request.UserId != request.ActorId && (scope.OwnOnly || !scope.ReadEnabled))
                throw new AcessoNegadoException();
        }
        else if (request.BoardId.HasValue)
        {
            await _boardAccess.EnsureAsync(request.BoardId.Value, request.ActorId,
                PlatformPermission.View, ProjectRole.Viewer, cancellationToken);

        }
        else
            throw new DomainException("Informe tarefa, usuário ou quadro para totalizar.");

        var entries = await _timeEntries.GetVisibleEntriesAsync(scope,request.WorkItemId,request.BoardId,request.UserId,cancellationToken);
        // Tarefa/quadro já passaram pelo portão do recurso; a consulta pessoal mantém
        // o histórico próprio. A totalização de terceiro também restringe projetos.
        var visible = !string.IsNullOrWhiteSpace(request.UserId) && request.UserId != request.ActorId
            ? await _hours.FilterEntriesAsync(scope,entries,cancellationToken) : entries;
        var now = DateTimeOffset.UtcNow;
        var totalSeconds = visible.Sum(e=>Math.Max(0,(int)((e.EndedAt ?? now)-e.StartedAt).TotalSeconds));
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
