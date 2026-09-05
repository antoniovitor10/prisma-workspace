using Prisma.Workspace.Application.Features.WorkItems.Dtos;
using MediatR;

namespace Prisma.Workspace.Application.Features.WorkItems.Queries;

/// <summary>
/// Query para obter subtarefas de um WorkItem pai.
/// </summary>
public record GetSubItemsQuery(Guid ParentId, string? CurrentUserId = null) : IRequest<IReadOnlyList<WorkItemDto>>;
