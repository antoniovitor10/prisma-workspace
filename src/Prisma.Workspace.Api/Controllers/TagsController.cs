using Prisma.Workspace.Application.Features.Catalogo;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Prisma.Workspace.Api.Controllers;

/// <summary>CRUD simples de Tags (etiquetas coloridas).</summary>
[ApiController]
[Route("api/tags")]
[Authorize]
public class TagsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TagsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CatalogItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetTagsQuery(UserId), cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(typeof(CatalogItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CatalogRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(
            new CreateTagCommand(request.Name, request.Color, UserId), cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteTagCommand(id, UserId), cancellationToken);
        return NoContent();
    }
}
