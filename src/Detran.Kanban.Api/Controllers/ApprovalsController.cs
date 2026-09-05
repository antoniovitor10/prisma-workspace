using Detran.Kanban.Application.Features.Approvals.Commands;
using Detran.Kanban.Application.Features.Approvals.Dtos;
using Detran.Kanban.Application.Features.Approvals.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Detran.Kanban.Api.Controllers;

/// <summary>
/// Aprovações de entrega de tarefa (Solicitar → Aprovar/Rejeitar).
/// Nenhuma lógica de negócio aqui — só traduz HTTP para o MediatR.
/// </summary>
[ApiController]
[Route("api")]
[Authorize]
public class ApprovalsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ApprovalsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Usuário sem identificador.");

    /// <summary>Aprovações de uma tarefa (mais recente primeiro).</summary>
    [HttpGet("WorkItems/{workItemId:guid}/approvals")]
    [ProducesResponseType(typeof(IReadOnlyList<ApprovalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByWorkItem(Guid workItemId, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(
            new GetApprovalsByWorkItemQuery(workItemId, UserId), cancellationToken));
    }

    /// <summary>Solicita aprovação da tarefa a um aprovador.</summary>
    [HttpPost("WorkItems/{workItemId:guid}/approvals")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RequestApproval(
        Guid workItemId,
        [FromBody] RequestApprovalBody body,
        CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(
            new RequestApprovalCommand(workItemId, UserId, body.ApproverId), cancellationToken);
        return Ok(new { Id = id, Status = 0 });
    }

    /// <summary>Decide (1 = aprovar, 2 = rejeitar) uma aprovação pendente do usuário logado.</summary>
    [HttpPut("approvals/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Decide(
        Guid id,
        [FromBody] DecideApprovalBody body,
        CancellationToken cancellationToken)
    {
        if (body.Status != 1 && body.Status != 2)
            return BadRequest(new { detail = "Status deve ser 1 (aprovar) ou 2 (rejeitar)." });

        await _mediator.Send(
            new DecideApprovalCommand(id, UserId, Aprovar: body.Status == 1, body.Note), cancellationToken);
        return NoContent();
    }

    /// <summary>Aprovações onde o usuário logado é o aprovador.</summary>
    [HttpGet("me/approvals")]
    [ProducesResponseType(typeof(IReadOnlyList<ApprovalDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MyApprovals(CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetMyApprovalsQuery(UserId), cancellationToken));
    }
}

public record RequestApprovalBody(string ApproverId);
public record DecideApprovalBody(int Status, string? Note);
