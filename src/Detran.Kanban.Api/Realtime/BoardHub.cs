using System.Security.Claims;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Detran.Kanban.Api.Realtime;

[Authorize]
public sealed class BoardHub : Hub
{
    private readonly AppDbContext _db;
    public BoardHub(AppDbContext db) => _db = db;

    public async Task JoinBoard(Guid organizationId, Guid boardId)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new HubException("Usuário não autenticado.");
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
