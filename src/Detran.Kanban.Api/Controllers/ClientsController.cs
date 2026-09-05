using Detran.Kanban.Application.Features.Catalogo;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Detran.Kanban.Api.Controllers;

/// <summary>CRUD simples de Clientes (projetos/quadros pertencem a clientes).</summary>
[ApiController]
[Route("api/clients")]
[Authorize]
public class ClientsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ClientsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ClientDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetClientsQuery(UserId), cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(typeof(ClientDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] ClientRequest request, CancellationToken cancellationToken)
    {
        var cliente = await _mediator.Send(
            new CreateClientCommand(request.Name, UserId), cancellationToken);
        return Ok(new { cliente.Id, cliente.Name });
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteClientCommand(id, UserId), cancellationToken);
        return NoContent();
    }
}

public record ClientRequest(string Name);
