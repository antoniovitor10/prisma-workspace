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
    [HttpGet("tasks/{workItemId:guid}/git")]
    public async Task<IActionResult> TaskGit(Guid projectId, Guid workItemId, CancellationToken ct)
        => Ok(await mediator.Send(new GetMethodTaskGitQuery(projectId, workItemId, ActorId), ct));
    /// <summary>"Consultar agora" na aba Git: consulta os repositórios no modo consulta da tarefa.</summary>
    [HttpPost("tasks/{workItemId:guid}/git/poll")]
    public async Task<IActionResult> PollTaskGit(Guid projectId, Guid workItemId, CancellationToken ct)
        => Ok(await mediator.Send(new PollMethodTaskGitCommand(projectId, workItemId, ActorId), ct));
    [HttpPost("tasks/{id}/branches")]
    public async Task<IActionResult> LinkBranch(Guid projectId, string id, LinkMethodBranchRequest request, CancellationToken ct)
        => Ok(await mediator.Send(new LinkMethodGitBranchCommand(projectId, id, request, ActorId), ct));
    [HttpGet("tasks/{id}/pull-request-draft")]
    public async Task<IActionResult> GetDraft(Guid projectId, string id, CancellationToken ct)
        => Ok(await mediator.Send(new GetMethodPullRequestDraftQuery(projectId, id, ActorId), ct));
    [HttpPut("tasks/{id}/pull-request-draft")]
    public async Task<IActionResult> SaveDraft(Guid projectId, string id, SaveMethodPullRequestDraftRequest request, CancellationToken ct)
        => Ok(await mediator.Send(new SaveMethodPullRequestDraftCommand(projectId, id, request, ActorId), ct));
    [HttpPost("tasks/{id}/pull-requests/open")]
    public async Task<IActionResult> Open(Guid projectId, string id, OpenMethodPullRequestRequest request, CancellationToken ct)
        => Ok(await mediator.Send(new OpenMethodPullRequestCommand(projectId, id, request, ActorId), ct));
}
