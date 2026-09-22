using System.Security.Claims;
using Prisma.Workspace.Application.Features.Backlog;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Prisma.Workspace.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/backlog")]
[Authorize]
public class BacklogController : ControllerBase
{
    private readonly IMediator _mediator;
    public BacklogController(IMediator mediator) => _mediator = mediator;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>
    /// Backlog do projeto. <c>includeArchived=true</c> traz também as tarefas arquivadas,
    /// que de outro modo ficam inalcançáveis para restaurar.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get(
        Guid projectId, [FromQuery] bool includeArchived = false, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetProjectBacklogQuery(projectId, UserId, includeArchived), ct));

    [HttpPut("order")]
    public async Task<IActionResult> Reorder(Guid projectId, [FromBody] ReorderBacklogRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ReorderBacklogCommand(projectId, request.OrderedIds, UserId), ct);
        return NoContent();
    }

    [HttpPut("sprint")]
    public async Task<IActionResult> PlanSprint(Guid projectId, [FromBody] PlanSprintRequest request, CancellationToken ct)
    {
        await _mediator.Send(new PlanSprintCommand(projectId, request.SprintId, request.WorkItemIds, UserId), ct);
        return NoContent();
    }

    [HttpPut("items/{workItemId:guid}")]
    public async Task<IActionResult> UpdateItem(
        Guid projectId,
        Guid workItemId,
        [FromBody] UpdateBacklogItemRequest request,
        CancellationToken ct)
    {
        await _mediator.Send(new UpdateBacklogItemCommand(
            projectId, workItemId, request.Title, request.Priority,
            request.Points, request.EpicId, request.UpdateEpic, UserId), ct);
        return NoContent();
    }
}

public record ReorderBacklogRequest(IReadOnlyList<Guid> OrderedIds);
public record PlanSprintRequest(Guid? SprintId, IReadOnlyList<Guid> WorkItemIds);
public record UpdateBacklogItemRequest(
    string Title,
    Prisma.Workspace.Domain.Enums.Priority Priority,
    int? Points,
    Guid? EpicId,
    bool UpdateEpic = false);
