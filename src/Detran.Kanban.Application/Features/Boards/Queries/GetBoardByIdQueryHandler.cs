using Detran.Kanban.Application.Features.Boards.Dtos;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Enums;
using MediatR;

namespace Detran.Kanban.Application.Features.Boards.Queries;

/// <summary>
/// Handler da query GetBoardById.
/// </summary>
public class GetBoardByIdQueryHandler : IRequestHandler<GetBoardByIdQuery, BoardDto?>
{
    private readonly IBoardAccessService _access;

    public GetBoardByIdQueryHandler(IBoardAccessService access)
    {
        _access = access;
    }

    public async Task<BoardDto?> Handle(
        GetBoardByIdQuery request,
        CancellationToken cancellationToken)
    {
        var board = await _access.EnsureAsync(
            request.Id, request.ActorId, PlatformPermission.View,
            ProjectRole.Viewer, cancellationToken);

        return new BoardDto
        {
            Id = board.Id,
            Name = board.Name,
            OwnerId = board.OwnerId ?? string.Empty,
            ProjectId = board.ProjectId,
            TeamId = board.TeamId,
            CardSettingsJson = board.CardSettingsJson,
            CreatedAt = board.CreatedAt
        };
    }
}
