using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Repositories;

namespace Prisma.Workspace.Api.Controllers;

/// <summary>
/// Tokens de leitura por organização GitHub do modo consulta (HU-0002, T-0007). Só sessão humana administrativa;
/// as respostas trazem o prefixo, a autoria e as datas, nunca o valor.
/// </summary>
[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/method/git-credentials")]
[TypeFilter(typeof(MethodGitEndpointFilter))]
public sealed class MethodGitCredentialsController(IMediator mediator) : ControllerBase
{
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, CancellationToken ct)
        => Ok(await mediator.Send(new ListGitReadCredentialsQuery(projectId, ActorId), ct));
    [HttpPost]
    public async Task<IActionResult> Save(Guid projectId, SaveGitReadCredentialRequest request, CancellationToken ct)
        => Ok(await mediator.Send(new SaveGitReadCredentialCommand(projectId, request, ActorId), ct));
    [HttpPost("{credentialId:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid projectId, Guid credentialId, CancellationToken ct)
        => Ok(await mediator.Send(new RevokeGitReadCredentialCommand(projectId, credentialId, ActorId), ct));
}
