using Prisma.Workspace.Application.Features.Boards.Dtos;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using MediatR;

namespace Prisma.Workspace.Application.Features.Boards.Queries;

/// <summary>
/// Handler da query GetAllBoards.
/// </summary>
public class GetAllBoardsQueryHandler : IRequestHandler<GetAllBoardsQuery, IReadOnlyList<BoardDto>>
{
    private readonly IBoardRepository _boardRepository;
    private readonly IProjectAccessService _projectAccess;
    private readonly IPermissionService _permissions;

    public GetAllBoardsQueryHandler(
        IBoardRepository boardRepository,
        IProjectAccessService projectAccess,
        IPermissionService permissions)
    {
        _boardRepository = boardRepository;
        _projectAccess = projectAccess;
        _permissions = permissions;
    }

    public async Task<IReadOnlyList<BoardDto>> Handle(
        GetAllBoardsQuery request,
        CancellationToken cancellationToken)
    {
        await _permissions.EnsureAsync(
            request.ActorId, PlatformPermission.View,
            PermissionScope.Organization, cancellationToken: cancellationToken);
        var boards = await _boardRepository.GetAllAsync(cancellationToken);
        var accessible = await _projectAccess.GetAccessibleProjectIdsAsync(
            boards.Select(x => x.ProjectId),
            request.ActorId, cancellationToken);

        return boards.Where(x => accessible.Contains(x.ProjectId))
            .Select(b => new BoardDto
        {
            Id = b.Id,
            Name = b.Name,
            OwnerId = b.OwnerId ?? string.Empty,
            ProjectId = b.ProjectId,
            TeamId = b.TeamId,
            CardSettingsJson = b.CardSettingsJson,
            CreatedAt = b.CreatedAt
        }).ToList().AsReadOnly();
    }
}
