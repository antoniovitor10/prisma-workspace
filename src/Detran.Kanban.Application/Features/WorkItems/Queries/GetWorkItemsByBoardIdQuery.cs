using Detran.Kanban.Application.Features.WorkItems.Dtos;
using MediatR;

namespace Detran.Kanban.Application.Features.WorkItems.Queries;

/// <summary>
/// Query para obter todos os WorkItems pertencentes a um Quadro (Board) específico.
/// </summary>
public record GetWorkItemsByBoardIdQuery(Guid BoardId, string? CurrentUserId = null) : IRequest<IReadOnlyList<WorkItemDto>>;
