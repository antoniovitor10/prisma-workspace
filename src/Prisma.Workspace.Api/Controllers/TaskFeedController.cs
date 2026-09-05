using Prisma.Workspace.Application.Features.TaskFeed.Commands;
using Prisma.Workspace.Application.Features.TaskFeed.Dtos;
using Prisma.Workspace.Application.Features.TaskFeed.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Prisma.Workspace.Api.Controllers;

/// <summary>
/// Feed da tarefa: comentários de usuários e eventos de sistema.
/// </summary>
[ApiController]
[Route("api/WorkItems/{workItemId:guid}")]
[Authorize]
public class TaskFeedController : ControllerBase
{
    private readonly IMediator _mediator;

    public TaskFeedController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet("comments")]
    [ProducesResponseType(typeof(IReadOnlyList<CommentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetComments(Guid workItemId, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetCommentsQuery(workItemId, ActorId), cancellationToken));
    }

    [HttpPost("comments")]
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddComment(
        Guid workItemId,
        [FromBody] AddCommentRequest request,
        CancellationToken cancellationToken)
    {
        var userId = ActorId;
        var userName = User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name ?? userId;

        var comment = await _mediator.Send(
            new AddCommentCommand(workItemId, userId, userName, request.Text, request.MentionedUserIds), cancellationToken);
        return CreatedAtAction(nameof(GetComments), new { workItemId }, comment);
    }

    [HttpGet("events")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskEventDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEvents(Guid workItemId, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetTaskEventsQuery(workItemId, ActorId), cancellationToken));
    }

    [HttpGet("state-graph")]
    [ProducesResponseType(typeof(TaskStateGraphDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStateGraph(Guid workItemId, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetTaskStateGraphQuery(workItemId, ActorId), cancellationToken));
    }
}

public record AddCommentRequest(string Text, IReadOnlyList<string>? MentionedUserIds = null);
