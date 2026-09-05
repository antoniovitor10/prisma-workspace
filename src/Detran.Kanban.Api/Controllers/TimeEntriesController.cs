using Detran.Kanban.Application.Features.TimeEntries.Commands;
using Detran.Kanban.Application.Features.TimeEntries.Dtos;
using Detran.Kanban.Application.Features.TimeEntries.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Detran.Kanban.Api.Controllers;

/// <summary>
/// Timer e lançamentos de horas. Regras (timer único etc.) vivem na Application.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TimeEntriesController : ControllerBase
{
    private readonly IMediator _mediator;

    public TimeEntriesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Usuário autenticado sem identificador.");

    [HttpGet("running")]
    [ProducesResponseType(typeof(TimeEntryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetRunning(CancellationToken cancellationToken)
    {
        var running = await _mediator.Send(new GetRunningTimerQuery(UserId), cancellationToken);
        return running is null ? NoContent() : Ok(running);
    }

    [HttpGet("work-item/{workItemId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<TimeEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByWorkItem(Guid workItemId, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(
            new GetTimeEntriesByWorkItemQuery(workItemId, UserId), cancellationToken));
    }

    [HttpPost("start")]
    [ProducesResponseType(typeof(TimeEntryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Start(
        [FromBody] StartTimerRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(
            new StartTimerCommand(request.WorkItemId, UserId, request.Note), cancellationToken));
    }

    [HttpPost("stop")]
    [ProducesResponseType(typeof(TimeEntryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Stop(
        [FromBody] StopTimerRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(
            new StopTimerCommand(request.WorkItemId, UserId, request.Note), cancellationToken));
    }

    [HttpPost("manual")]
    [ProducesResponseType(typeof(TimeEntryDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateManual(
        [FromBody] CreateManualTimeEntryRequest request,
        CancellationToken cancellationToken)
    {
        var dto = await _mediator.Send(new CreateManualTimeEntryCommand(
            request.WorkItemId, UserId, request.StartedAt, request.EndedAt, request.Note), cancellationToken);
        return CreatedAtAction(nameof(GetByWorkItem), new { workItemId = request.WorkItemId }, dto);
    }

    [HttpGet("work-item/{workItemId:guid}/total")]
    [ProducesResponseType(typeof(TimeTotalDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorkItemTotal(Guid workItemId, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(
            new GetTimeTotalQuery(UserId, WorkItemId: workItemId), cancellationToken));
    }

    [HttpGet("user/{userId}/total")]
    [ProducesResponseType(typeof(TimeTotalDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserTotal(string userId, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(
            new GetTimeTotalQuery(UserId, UserId: userId), cancellationToken));
    }

    [HttpGet("board/{boardId:guid}/total")]
    [ProducesResponseType(typeof(TimeTotalDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBoardTotal(Guid boardId, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(
            new GetTimeTotalQuery(UserId, BoardId: boardId), cancellationToken));
    }

    /// <summary>Visão semanal do tempo do usuário logado: total por dia (seg→dom).</summary>
    [HttpGet("my/weekly")]
    [ProducesResponseType(typeof(WeeklyTimeDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyWeekly(
        [FromQuery] string? weekStart,
        CancellationToken cancellationToken)
    {
        DateOnly? inicio = null;
        if (!string.IsNullOrWhiteSpace(weekStart))
        {
            if (!DateOnly.TryParse(weekStart, out var parsed))
                return BadRequest(new { detail = "Data inválida (use yyyy-MM-dd)." });
            inicio = parsed;
        }

        return Ok(await _mediator.Send(new GetMyWeeklyTimeQuery(UserId, inicio), cancellationToken));
    }
}

public record StartTimerRequest(Guid WorkItemId, string? Note);

public record StopTimerRequest(Guid? WorkItemId, string? Note);

public record CreateManualTimeEntryRequest(
    Guid WorkItemId,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    string? Note);
