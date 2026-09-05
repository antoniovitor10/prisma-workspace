using System.Security.Claims;
using Detran.Kanban.Application.Features.Sprints;
using Detran.Kanban.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detran.Kanban.Api.Controllers;

[ApiController]
[Authorize]
public class SprintsController : ControllerBase
{
    private readonly IMediator _mediator;
    public SprintsController(IMediator mediator) => _mediator = mediator;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("api/projects/{projectId:guid}/sprints")]
    public async Task<IActionResult> GetByProject(Guid projectId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetProjectSprintsQuery(projectId, UserId), ct));

    [HttpPost("api/projects/{projectId:guid}/sprints")]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateSprintRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(new CreateSprintCommand(projectId, request.TeamId, request.Name,
            request.Goal, request.StartDate, request.EndDate, UserId), ct);
        return Created($"/api/sprints/{id}", id);
    }

    [HttpPut("api/sprints/{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSprintRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateSprintCommand(
            id, request.Name, request.Goal, request.StartDate, request.EndDate, UserId), ct);
        return NoContent();
    }

    [HttpPut("api/sprints/{id:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeSprintStatusRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ChangeSprintStatusCommand(
            id, request.Status, UserId, request.IncompleteItemsAction, request.TargetSprintId), ct);
        return NoContent();
    }

    [HttpPut("api/sprints/{id:guid}/capacity/{memberId}")]
    public async Task<IActionResult> SetCapacity(Guid id, string memberId, [FromBody] SprintCapacityRequest request, CancellationToken ct)
    {
        await _mediator.Send(new SetSprintCapacityCommand(id, memberId, request.AvailableHours,
            request.DaysOffHours, UserId), ct);
        return NoContent();
    }
}

public record CreateSprintRequest(Guid? TeamId, string Name, string? Goal, DateOnly StartDate, DateOnly EndDate);
public record UpdateSprintRequest(string Name, string? Goal, DateOnly StartDate, DateOnly EndDate);
public record ChangeSprintStatusRequest(
    SprintStatus Status,
    SprintIncompleteItemsAction IncompleteItemsAction = SprintIncompleteItemsAction.ReturnToBacklog,
    Guid? TargetSprintId = null);
public record SprintCapacityRequest(decimal AvailableHours, decimal DaysOffHours);
