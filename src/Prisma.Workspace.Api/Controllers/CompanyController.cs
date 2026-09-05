using Prisma.Workspace.Application.Features.Company;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Prisma.Workspace.Api.Controllers;

/// <summary>
/// Área "Empresa": galeria de projetos (quadros) com cliente e progresso.
/// </summary>
[ApiController]
[Route("api/company")]
[Authorize]
public class CompanyController : ControllerBase
{
    private readonly IMediator _mediator;

    public CompanyController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    /// <summary>Projetos = quadros, com cliente, progresso e horas.</summary>
    [HttpGet("projects")]
    [ProducesResponseType(typeof(IReadOnlyList<ProjectSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProjects(CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetCompanyProjectsQuery(UserId), cancellationToken));
    }

    /// <summary>Vincula cliente e descrição a um quadro/projeto.</summary>
    [HttpPut("projects/{boardId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateProject(
        Guid boardId,
        [FromBody] UpdateProjectRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new UpdateProjectCommand(
                boardId, request.ClientId, request.Description, UserId), cancellationToken);
        return NoContent();
    }
}

public record UpdateProjectRequest(Guid? ClientId, string? Description);
