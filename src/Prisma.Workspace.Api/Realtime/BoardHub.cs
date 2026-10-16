using System.Security.Claims;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Prisma.Workspace.Application.Features.MethodSecurity;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Api.Realtime;

[Authorize]
public sealed class BoardHub : Hub
{
    private readonly AppDbContext _db;
    private readonly IMethodAccessContext? _method;
    private readonly IMethodPermissionEvaluator? _methodPermissions;
    public BoardHub(AppDbContext db, IMethodAccessContext? method = null, IMethodPermissionEvaluator? methodPermissions = null)
        => (_db, _method, _methodPermissions) = (db, method, methodPermissions);

    public async Task JoinBoard(Guid organizationId, Guid boardId)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new HubException("Usuário não autenticado.");
        if (_method?.Current is { AuthenticationKind: MethodAuthenticationKind.MethodToken } actor)
        {
            if (actor.OrganizationId != organizationId || actor.ProjectId is not { } projectId || actor.ActorId != userId
                || _methodPermissions is null || !await _db.Boards.IgnoreQueryFilters().AsNoTracking()
                    .AnyAsync(x => x.Id == boardId && x.OrganizationId == organizationId && x.ProjectId == projectId)
                || !await _methodPermissions.HasAsync(actor, PlatformPermission.View, PermissionScope.Project, projectId))
                throw new HubException("Acesso ao quadro negado.");
            await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(boardId));
            return;
        }
        var member = await _db.OrganizationMembers.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(x => x.OrganizationId == organizationId && x.UserId == userId
                && x.IsActive && x.Organization.IsActive);
        var board = member && await _db.Boards.IgnoreQueryFilters().AsNoTracking()
            .AnyAsync(x => x.Id == boardId && x.OrganizationId == organizationId);
        if (!board) throw new HubException("Acesso ao quadro negado.");
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(boardId));
    }

    public Task LeaveBoard(Guid boardId)
        => Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(boardId));

    internal static string GroupName(Guid boardId) => $"board:{boardId:N}";
}

public sealed class SignalRBoardRealtimeNotifier : IBoardRealtimeNotifier
{
    private readonly IHubContext<BoardHub> _hub;
    public SignalRBoardRealtimeNotifier(IHubContext<BoardHub> hub) => _hub = hub;
    public Task BoardChangedAsync(
        Guid boardId, string changeType, Guid? workItemId = null, CancellationToken ct = default)
        => _hub.Clients.Group(BoardHub.GroupName(boardId)).SendAsync("boardChanged", new
        {
            boardId,
            changeType,
            workItemId,
            occurredAt = DateTimeOffset.UtcNow
        }, ct);
}
