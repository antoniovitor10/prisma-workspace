using System.Security.Claims;
using Detran.Kanban.Application.Features.Audit;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detran.Kanban.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/audit")]
[Tags("Auditoria")]
public sealed class AuditController : ControllerBase
{
    private readonly IMediator _mediator;
    public AuditController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(AuditPageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Search(
        [FromQuery] string? entityType,
        [FromQuery] string? entityId,
        [FromQuery] string? action,
        [FromQuery] string? userId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
        => Ok(await _mediator.Send(new SearchAuditLogsQuery(
            User.FindFirstValue(ClaimTypes.NameIdentifier)!, entityType, entityId, action,
            userId, from, to, page, pageSize), ct));
}
