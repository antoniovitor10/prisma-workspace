using Prisma.Workspace.Application.Features.Catalogo;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Prisma.Workspace.Api.Controllers;

/// <summary>CRUD simples de Tipos de tarefa (chips coloridos).</summary>
[ApiController]
[Route("api/task-types")]
[Authorize]
public class TaskTypesController : ControllerBase
{
    private readonly IMediator _mediator;

    public TaskTypesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CatalogItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetTaskTypesQuery(UserId), cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(typeof(CatalogItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] CatalogRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(
            new CreateTaskTypeCommand(request.Name, request.Color, UserId), cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteTaskTypeCommand(id, UserId), cancellationToken);
        return NoContent();
    }
}

/// <summary>Request genérico de catálogo (tipo/tag).</summary>
public record CatalogRequest(string Name, string? Color);
