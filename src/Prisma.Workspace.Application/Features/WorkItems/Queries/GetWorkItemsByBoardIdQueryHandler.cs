using Prisma.Workspace.Application.Features.WorkItems.Dtos;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using MediatR;

namespace Prisma.Workspace.Application.Features.WorkItems.Queries;

/// <summary>
/// Handler da query para listar os cards de um Board.
/// </summary>
public class GetWorkItemsByBoardIdQueryHandler : IRequestHandler<GetWorkItemsByBoardIdQuery, IReadOnlyList<WorkItemDto>>
{
    private readonly IWorkItemRepository _workItemRepository;
    private readonly IBoardAccessService _access;

    public GetWorkItemsByBoardIdQueryHandler(
        IWorkItemRepository workItemRepository,
        IBoardAccessService access)
    {
        _workItemRepository = workItemRepository;
        _access = access;
    }

    public async Task<IReadOnlyList<WorkItemDto>> Handle(
        GetWorkItemsByBoardIdQuery request,
        CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(request.BoardId, request.CurrentUserId ?? string.Empty,
            PlatformPermission.View, ProjectRole.Viewer, cancellationToken);
        var items = await _workItemRepository.GetByBoardIdAsync(request.BoardId, cancellationToken);

        return WorkItemCardProjection.ToCards(items, request.CurrentUserId);
    }
}
