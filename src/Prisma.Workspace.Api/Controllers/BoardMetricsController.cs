using Prisma.Workspace.Application.Features.BoardMetrics;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Prisma.Workspace.Api.Controllers;

/// <summary>
/// Dashboard do quadro: 6 métricas fixas.
/// </summary>
[ApiController]
[Route("api/Boards/{boardId:guid}/metrics")]
[Authorize]
public class BoardMetricsController : ControllerBase
{
    private readonly IMediator _mediator;

    public BoardMetricsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [ProducesResponseType(typeof(BoardMetricsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid boardId, CancellationToken cancellationToken)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        return Ok(await _mediator.Send(
            new GetBoardMetricsQuery(boardId, actorId), cancellationToken));
    }
}
