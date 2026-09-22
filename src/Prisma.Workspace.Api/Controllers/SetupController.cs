using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Prisma.Workspace.Api.OpenApi;
using Prisma.Workspace.Application.Features.Installation;
using Prisma.Workspace.Application.Interfaces;

namespace Prisma.Workspace.Api.Controllers;

[ApiController]
[Route("api/setup")]
[AllowAnonymous]
[Tags("Instalação")]
public sealed class SetupController(IMediator mediator) : ControllerBase
{
    [HttpGet("status")]
    [ProducesResponseType(typeof(InstallationSetupStatus), StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(CancellationToken ct)
        => Ok(await mediator.Send(new GetInstallationStatusQuery(), ct));

    [HttpPost]
    [EnableRateLimiting("setup")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete([FromBody] CompleteSetupRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new CompleteInstallationSetupCommand(
            request.AdministratorName,
            request.AdministratorEmail,
            request.AdministratorPassword,
            request.OrganizationName,
            request.OrganizationSlug,
            Request.Headers["X-Prisma-Setup-Token"].FirstOrDefault()), ct);

        return result.Outcome switch
        {
            InstallationSetupOutcome.Created => StatusCode(StatusCodes.Status201Created, new { initialized = true }),
            InstallationSetupOutcome.Unavailable => ProblemResult(403, "setup_unavailable",
                "Configuração inicial indisponível", "Não foi possível autorizar a configuração inicial."),
            InstallationSetupOutcome.AlreadyCompleted => ProblemResult(409, "setup_already_completed",
                "Configuração inicial concluída", "Esta instalação já foi configurada."),
            InstallationSetupOutcome.Conflict => ProblemResult(409, "setup_conflict",
                "Instalação incompatível", "A instalação contém dados e não pode executar a configuração inicial."),
            InstallationSetupOutcome.ValidationFailed => BadRequest(ApiErrors.Validation(
                result.Errors ?? new Dictionary<string, string[]> { ["request"] = ["Dados inválidos."] },
                HttpContext.TraceIdentifier)),
            _ => throw new InvalidOperationException("Resultado de setup desconhecido.")
        };
    }

    private ObjectResult ProblemResult(int status, string type, string title, string detail)
        => StatusCode(status, ApiErrors.Problem(status, type, title, detail, HttpContext.TraceIdentifier));
}

public sealed record CompleteSetupRequest(
    string AdministratorName,
    string AdministratorEmail,
    string AdministratorPassword,
    string OrganizationName,
    string OrganizationSlug);
