using Prisma.Workspace.Application.Features.Boards.Commands;
using Prisma.Workspace.Application.Features.Boards;
using Prisma.Workspace.Application.Features.Boards.Dtos;
using Prisma.Workspace.Application.Features.Boards.Queries;
using Prisma.Workspace.Application.Features.Stages.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Prisma.Workspace.Api.Controllers;

/// <summary>
/// Controller de Boards (quadros kanban).
/// Nenhuma lógica de negócio aqui — só orquestra o MediatR.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BoardsController : ControllerBase
{
    private readonly IMediator _mediator;

    public BoardsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    /// <summary>
    /// Lista todos os quadros.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BoardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllBoardsQuery(UserId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Busca um quadro por Id.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BoardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetBoardByIdQuery(id, UserId), cancellationToken);

        if (result is null)
            return NotFound();

        return Ok(result);
    }

    /// <summary>
    /// Cria um novo quadro. O OwnerId é extraído do JWT do usuário autenticado.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBoardRequest request,
        CancellationToken cancellationToken)
    {
        var ownerId = UserId;

        var command = new CreateBoardCommand(request.Name, ownerId, request.ProjectId, request.TeamId, request.CopyStagesFromBoardId);
        var boardId = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = boardId }, boardId);
    }

    /// <summary>
    /// Exclui um quadro. Itens exclusivos são realocados para DestinationBoardId.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromQuery] Guid? destinationBoardId,
        [FromQuery] Guid? destinationStageId,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new RemoveBoardCommand(id, destinationBoardId, destinationStageId, UserId), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Retorna as estatísticas de Lead Time por coluna de um quadro.
    /// </summary>
    [HttpGet("{boardId:guid}/lead-time")]
    [ProducesResponseType(typeof(IReadOnlyList<StageLeadTimeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLeadTime(Guid boardId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetBoardLeadTimeQuery(boardId, UserId), cancellationToken);
        return Ok(result);
    }
}

/// <summary>
/// Request body para criação de Board (só o nome, o owner vem do JWT).
/// </summary>
public record CreateBoardRequest(string Name, Guid ProjectId, Guid? TeamId = null, Guid? CopyStagesFromBoardId = null);
