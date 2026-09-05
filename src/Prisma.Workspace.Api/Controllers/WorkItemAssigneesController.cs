using Prisma.Workspace.Application.Features.Assignees;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Prisma.Workspace.Api.Controllers;

/// <summary>
/// Responsáveis de uma tarefa (atribuir/desatribuir).
/// </summary>
[ApiController]
[Route("api/WorkItems/{workItemId:guid}/assignees")]
[Authorize]
public class WorkItemAssigneesController : ControllerBase
{
    private readonly IMediator _mediator;

    public WorkItemAssigneesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private string ActorName => User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? ActorId;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AssignedUserDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid workItemId, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetAssigneesQuery(workItemId, ActorId), cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Assign(
        Guid workItemId,
        [FromBody] AssignUserRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new AssignUserCommand(workItemId, request.UserId, ActorId, ActorName), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{userId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unassign(Guid workItemId, string userId, CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new UnassignUserCommand(workItemId, userId, ActorId, ActorName), cancellationToken);
        return NoContent();
    }
}

public record AssignUserRequest(string UserId);
