using System.Security.Claims;
using Detran.Kanban.Application.Features.Search;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detran.Kanban.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/search")]
[Tags("Pesquisa global")]
public sealed class SearchController : ControllerBase
{
    private readonly IMediator _mediator;
    public SearchController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(GlobalSearchDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery(Name = "q")] string query,
        [FromQuery] int limit = 6,
        CancellationToken ct = default)
        => Ok(await _mediator.Send(new GlobalSearchQuery(
            query, User.FindFirstValue(ClaimTypes.NameIdentifier)!, limit), ct));
}
