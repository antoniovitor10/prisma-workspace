using Detran.Kanban.Application.Features.WorkItemDetails;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Detran.Kanban.Api.Controllers;

/// <summary>
/// Detalhes da tarefa: taxonomia (tipo, tags, pontos), descrição e checklist.
/// </summary>
[ApiController]
[Route("api/WorkItems/{workItemId:guid}")]
[Authorize]
public class WorkItemDetailsController : ControllerBase
{
    private readonly IMediator _mediator;

    public WorkItemDetailsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    // ─── Taxonomia ──────────────────────────────────────────────────────
    [HttpPut("taxonomy")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetTaxonomy(
        Guid workItemId,
        [FromBody] TaxonomyRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new SetTaxonomyCommand(
            workItemId, request.TaskTypeId, request.Points,
            request.TagIds ?? new List<Guid>(), ActorId), cancellationToken);
        return NoContent();
    }

    // ─── Descrição ──────────────────────────────────────────────────────
    [HttpPut("description")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetDescription(
        Guid workItemId,
        [FromBody] DescriptionRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new SetDescriptionCommand(
            workItemId, request.Description, ActorId), cancellationToken);
        return NoContent();
    }

    // ─── Checklist ──────────────────────────────────────────────────────
    [HttpGet("checklist")]
    [ProducesResponseType(typeof(IReadOnlyList<ChecklistItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChecklist(Guid workItemId, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetChecklistQuery(workItemId, ActorId), cancellationToken));
    }

    [HttpPost("checklist")]
    [ProducesResponseType(typeof(ChecklistItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddChecklistItem(
        Guid workItemId,
        [FromBody] ChecklistTextRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new AddChecklistItemCommand(
            workItemId, request.Text, ActorId), cancellationToken));
    }

    [HttpPut("checklist/{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ToggleChecklistItem(
        Guid workItemId,
        Guid itemId,
        [FromBody] ChecklistToggleRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new ToggleChecklistItemCommand(
            workItemId, itemId, request.Done, ActorId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("checklist/{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteChecklistItem(
        Guid workItemId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteChecklistItemCommand(
            workItemId, itemId, ActorId), cancellationToken);
        return NoContent();
    }
}

public record TaxonomyRequest(Guid? TaskTypeId, int? Points, List<Guid>? TagIds);
public record DescriptionRequest(string? Description);
public record ChecklistTextRequest(string Text);
public record ChecklistToggleRequest(bool Done);
