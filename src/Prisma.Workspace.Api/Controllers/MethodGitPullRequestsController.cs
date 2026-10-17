using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodGit.Repositories;

namespace Prisma.Workspace.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/method")]
[TypeFilter(typeof(MethodGitEndpointFilter))]
public sealed class MethodGitPullRequestsController(IMediator mediator) : ControllerBase
{
    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    [HttpGet("pull-requests")]
    public async Task<IActionResult> List(Guid projectId, [FromQuery] bool? linked = null, [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await mediator.Send(new ListMethodGitPullRequestsQuery(projectId, linked, page, pageSize, ActorId), ct));
    [HttpPost("pull-requests/{observationId:guid}/link")]
    public async Task<IActionResult> LinkObserved(Guid projectId, Guid observationId, LinkObservedPullRequestRequest request, CancellationToken ct)
        => Ok(await mediator.Send(new LinkObservedMethodGitPullRequestCommand(projectId, observationId, request, ActorId), ct));
    [HttpPost("tasks/{id}/pull-requests")]
    public async Task<IActionResult> LinkUrl(Guid projectId, string id, LinkMethodPullRequestRequest request, CancellationToken ct)
        => Ok(await mediator.Send(new LinkMethodGitPullRequestCommand(projectId, id, request, ActorId), ct));
}
