using MediatR;
using Prisma.Workspace.Application.Features.WorkItems.Dtos;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Features.WorkItems.Queries;

/// <summary>
/// Cartões do projeto inteiro, qualquer que seja o quadro de origem.
///
/// Existe porque o fluxo passou a pertencer ao projeto (<c>SPEC-BOARD-AS-VIEW</c>): o
/// Kanban do projeto abre direto, sem exigir que alguém escolha um quadro antes.
/// </summary>
public record GetWorkItemsByProjectIdQuery(Guid ProjectId, string? CurrentUserId = null)
    : IRequest<IReadOnlyList<WorkItemDto>>;

public class GetWorkItemsByProjectIdQueryHandler
    : IRequestHandler<GetWorkItemsByProjectIdQuery, IReadOnlyList<WorkItemDto>>
{
    private readonly IWorkItemRepository _workItems;
    private readonly IProjectAccessService _access;

    public GetWorkItemsByProjectIdQueryHandler(
        IWorkItemRepository workItems,
        IProjectAccessService access)
    {
        _workItems = workItems;
        _access = access;
    }

    public async Task<IReadOnlyList<WorkItemDto>> Handle(
        GetWorkItemsByProjectIdQuery request,
        CancellationToken cancellationToken)
    {
        // Mesmo portão da listagem de etapas do projeto: quem enxerga o projeto enxerga
        // todos os seus cartões. O quadro deixou de restringir acesso (D83).
        await _access.EnsureAtLeastAsync(
            request.ProjectId, request.CurrentUserId ?? string.Empty,
            ProjectRole.Viewer, cancellationToken);

        var items = await _workItems.GetByProjectIdAsync(request.ProjectId, cancellationToken);
        return WorkItemCardProjection.ToCards(items, request.CurrentUserId);
    }
}
