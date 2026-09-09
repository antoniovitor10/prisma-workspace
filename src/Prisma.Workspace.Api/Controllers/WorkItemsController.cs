using Prisma.Workspace.Application.Features.WorkItems.Commands;
using Prisma.Workspace.Application.Features.WorkItems.Dtos;
using Prisma.Workspace.Application.Features.WorkItems.Queries;
using Prisma.Workspace.Application.Features.WorkItems;
using Prisma.Workspace.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Prisma.Workspace.Api.Controllers;

/// <summary>
/// Controller para gerenciar cartões de tarefas (WorkItems).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class WorkItemsController : ControllerBase
{
    private readonly IMediator _mediator;

    public WorkItemsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lista os itens de trabalho de um quadro.
    /// </summary>
    [HttpGet("board/{boardId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByBoardId(Guid boardId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _mediator.Send(new GetWorkItemsByBoardIdQuery(boardId, userId), cancellationToken);
        return Ok(result);
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkItemSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery] Guid projectId,
        [FromQuery] string query,
        [FromQuery] int limit = 12,
        CancellationToken cancellationToken = default)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        return Ok(await _mediator.Send(
            new SearchWorkItemsQuery(projectId, query, userId, limit), cancellationToken));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(WorkItemDetailsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        return Ok(await _mediator.Send(new GetWorkItemDetailsQuery(id, userId), cancellationToken));
    }

    /// <summary>
    /// Lista as subtarefas de um item de trabalho pai.
    /// </summary>
    [HttpGet("{parentId:guid}/subitems")]
    [ProducesResponseType(typeof(IReadOnlyList<WorkItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSubItems(Guid parentId, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var result = await _mediator.Send(new GetSubItemsQuery(parentId, userId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Cria uma nova tarefa ou subtarefa.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateWorkItemRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var command = new CreateWorkItemCommand(
            request.BoardId,
            request.StageId,
            request.ParentId,
            request.Title,
            request.Subtitle,
            request.Description,
            request.Priority,
            request.EstimatedHours,
            request.DueDate,
            request.Position,
            userId,
            request.Kind,
            request.SprintId,
            request.RemainingHours,
            request.TeamId,
            request.ResponsibleId,
            request.ParticipantIds,
            request.Origin,
            request.RequesterId,
            request.RequesterName,
            request.RequesterEmail,
            request.StartDate,
            request.AcceptanceCriteria,
            ProjectId: request.ProjectId
        );

        var workItemId = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetByBoardId), new { boardId = request.BoardId }, workItemId);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateWorkItemRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var actorName = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? actorId;
        await _mediator.Send(new UpdateWorkItemCommand(
            id, request.Title, request.Description, request.Kind, request.StageId,
            request.Priority, request.ResponsibleId, request.TeamId, request.Origin,
            request.RequesterId, request.RequesterName, request.RequesterEmail,
            request.StartDate, request.DueDate, request.EstimatedHours, request.RemainingHours,
            request.Points, request.AcceptanceCriteria, actorId, actorName), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        await _mediator.Send(new SetWorkItemArchivedCommand(id, true, actorId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/reactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        await _mediator.Send(new SetWorkItemArchivedCommand(id, false, actorId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/duplicate")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> Duplicate(Guid id, CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var newId = await _mediator.Send(new DuplicateWorkItemCommand(id, actorId), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = newId }, newId);
    }

    [HttpPost("{id:guid}/links")]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddLink(
        Guid id,
        [FromBody] WorkItemLinkRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var linkId = await _mediator.Send(new CreateWorkItemLinkCommand(
            id, request.TargetWorkItemId, request.Type, actorId), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, linkId);
    }

    [HttpDelete("{id:guid}/links/{linkId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveLink(Guid id, Guid linkId, CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        await _mediator.Send(new RemoveWorkItemLinkCommand(id, linkId, actorId), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/following")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetFollowing(
        Guid id,
        [FromBody] WorkItemFollowingRequest request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        await _mediator.Send(new SetWorkItemFollowingCommand(id, request.Following, userId), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/custom-fields")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetCustomFields(
        Guid id,
        [FromBody] WorkItemCustomFieldsRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        await _mediator.Send(new SetWorkItemCustomFieldsCommand(
            id, request.Values ?? new Dictionary<Guid, string?>(), actorId), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Move uma tarefa de coluna ou altera a sua ordem.
    /// </summary>
    [HttpPost("move")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Move(
        [FromBody] MoveWorkItemRequest request,
        CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var actorName = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? actorId;
        var command = new MoveWorkItemCommand(request.WorkItemId, request.DestinationStageId, request.Position, actorId, actorName, request.Reason);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }
}

/// <summary>
/// Modelo de request para criar uma tarefa.
/// </summary>
public record CreateWorkItemRequest(
    Guid BoardId,
    Guid? StageId,
    Guid? ParentId,
    string Title,
    string? Subtitle,
    string? Description,
    Priority Priority,
    decimal? EstimatedHours,
    DateOnly? DueDate,
    double Position,
    WorkItemKind Kind = WorkItemKind.Task,
    Guid? SprintId = null,
    decimal? RemainingHours = null,
    Guid? TeamId = null,
    string? ResponsibleId = null,
    IReadOnlyList<string>? ParticipantIds = null,
    WorkItemOrigin Origin = WorkItemOrigin.Internal,
    string? RequesterId = null,
    string? RequesterName = null,
    string? RequesterEmail = null,
    DateOnly? StartDate = null,
    string? AcceptanceCriteria = null,
    /// <summary>Quando BoardId é omitido, resolve o quadro padrão deste projeto.</summary>
    Guid? ProjectId = null
);

public record UpdateWorkItemRequest(
    string Title,
    string? Description,
    WorkItemKind Kind,
    Guid? StageId,
    Priority Priority,
    string? ResponsibleId,
    Guid? TeamId,
    WorkItemOrigin Origin,
    string? RequesterId,
    string? RequesterName,
    string? RequesterEmail,
    DateOnly? StartDate,
    DateOnly? DueDate,
    decimal? EstimatedHours,
    decimal? RemainingHours,
    int? Points,
    string? AcceptanceCriteria);

public record WorkItemLinkRequest(Guid TargetWorkItemId, WorkItemLinkType Type);
public record WorkItemFollowingRequest(bool Following);
public record WorkItemCustomFieldsRequest(IReadOnlyDictionary<Guid, string?>? Values);

/// <summary>
/// Modelo de request para mover/reordenar uma tarefa.
/// </summary>
public record MoveWorkItemRequest(Guid WorkItemId, Guid? DestinationStageId, double Position, string? Reason = null);
