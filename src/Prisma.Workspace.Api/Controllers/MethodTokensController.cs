using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodSecurity;

namespace Prisma.Workspace.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/method/tokens")]
public sealed class MethodTokensController(IMethodTokenService tokens) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MethodTokenDto>>> List(Guid projectId, CancellationToken cancellationToken)
        => Ok(await tokens.ListAsync(projectId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<IssuedMethodTokenDto>> Issue(Guid projectId, IssueMethodTokenRequest request, CancellationToken cancellationToken)
        => Ok(await tokens.IssueAsync(projectId, request, cancellationToken));

    [HttpPost("{tokenId:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid projectId, Guid tokenId, CancellationToken cancellationToken)
    {
        await tokens.RevokeAsync(projectId, tokenId, cancellationToken);
        return NoContent();
    }
}
