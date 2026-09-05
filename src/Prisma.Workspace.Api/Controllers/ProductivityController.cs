using System.Security.Claims;
using Prisma.Workspace.Application.Features.Productivity;
using Prisma.Workspace.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Prisma.Workspace.Api.Controllers;

[ApiController]
[Route("api/boards/{boardId:guid}")]
[Authorize]
[Tags("Produtividade")]
public class ProductivityController : ControllerBase
{
    private readonly IMediator _mediator;
    public ProductivityController(IMediator mediator) => _mediator = mediator;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("saved-filters")]
    public async Task<IActionResult> Filters(Guid boardId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetSavedFiltersQuery(boardId, UserId), ct));

    [HttpPost("saved-filters")]
    public async Task<IActionResult> CreateFilter(Guid boardId, [FromBody] SavedFilterRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new CreateSavedFilterCommand(boardId, UserId, request.Name, request.FilterJson), ct));

    [HttpDelete("saved-filters/{id:guid}")]
    public async Task<IActionResult> DeleteFilter(Guid boardId, Guid id, CancellationToken ct)
    { await _mediator.Send(new DeleteSavedFilterCommand(boardId, id, UserId), ct); return NoContent(); }

    [HttpGet("automations")]
    public async Task<IActionResult> Automations(Guid boardId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetAutomationRulesQuery(boardId, UserId), ct));

    [HttpPost("automations")]
    public async Task<IActionResult> CreateAutomation(Guid boardId, [FromBody] AutomationRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new CreateAutomationRuleCommand(
            boardId, request.TriggerStageId, request.ActionType,
            request.ActionValue, request.IsActive, UserId), ct));

    [HttpPut("automations/{id:guid}")]
    public async Task<IActionResult> UpdateAutomation(
        Guid boardId, Guid id, [FromBody] AutomationRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new UpdateAutomationRuleCommand(
            boardId, id, request.TriggerStageId, request.ActionType,
            request.ActionValue, request.IsActive, UserId), ct));

    [HttpDelete("automations/{id:guid}")]
    public async Task<IActionResult> DeleteAutomation(Guid boardId, Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteAutomationRuleCommand(boardId, id, UserId), ct);
        return NoContent();
    }

    [HttpPost("work-items/bulk")]
    public async Task<IActionResult> Bulk(Guid boardId, [FromBody] BulkRequest request, CancellationToken ct)
    {
        await _mediator.Send(new BulkWorkItemsCommand(
            boardId, request.WorkItemIds, request.Action,
            request.TargetValue, request.Priority, UserId), ct);
        return NoContent();
    }
}

public sealed record SavedFilterRequest(string Name, string FilterJson);
public sealed record AutomationRequest(
    Guid TriggerStageId,
    AutomationActionType ActionType,
    string ActionValue,
    bool IsActive = true);
public sealed record BulkRequest(
    IReadOnlyList<Guid> WorkItemIds,
    BulkActionType Action,
    string? TargetValue,
    Priority? Priority);
