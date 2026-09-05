using Detran.Kanban.Application.Features.Me.Commands;
using Detran.Kanban.Application.Features.Me.Dtos;
using Detran.Kanban.Application.Features.Me.Queries;
using Detran.Kanban.Application.Features.Me;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Detran.Kanban.Api.Controllers;

/// <summary>
/// Área pessoal ("Eu"): fila de tarefas priorizada e timer ativo.
/// </summary>
[ApiController]
[Route("api/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly IMediator _mediator;

    public MeController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Usuário autenticado sem identificador.");

    /// <summary>Fila "Tarefas para mim" ordenada pela prioridade pessoal.</summary>
    /// <summary>Painel unificado de tarefas, prazos, menções, aprovações e alertas.</summary>
    [HttpGet("work")]
    [ProducesResponseType(typeof(MyWorkDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyWork(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetMyWorkDashboardQuery(UserId), cancellationToken));

    [HttpGet("tasks")]
    [ProducesResponseType(typeof(IReadOnlyList<MeTaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTasks(CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetMyTasksQuery(UserId), cancellationToken));
    }

    /// <summary>Reordena a fila pessoal: recebe a lista de IDs na nova ordem.</summary>
    [HttpPut("tasks/priorities")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdatePriorities(
        [FromBody] UpdatePrioritiesRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new UpdateMyTaskPrioritiesCommand(UserId, request.WorkItemIds), cancellationToken);
        return NoContent();
    }

    /// <summary>Timer ativo do usuário logado (ou 204 se não houver).</summary>
    [HttpGet("active-timer")]
    [ProducesResponseType(typeof(ActiveTimerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetActiveTimer(CancellationToken cancellationToken)
    {
        var timer = await _mediator.Send(new GetMyActiveTimerQuery(UserId), cancellationToken);
        return timer is null ? NoContent() : Ok(timer);
    }
}

public record UpdatePrioritiesRequest(IReadOnlyList<Guid> WorkItemIds);
