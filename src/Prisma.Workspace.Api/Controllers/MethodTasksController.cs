using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodSecurity;

namespace Prisma.Workspace.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/method/tasks")]
public sealed class MethodTasksController(IMethodTaskOperations tasks) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<MethodTaskDto>>> List(Guid projectId, [FromQuery] string? status, CancellationToken ct)
        => Ok(await tasks.ListAsync(projectId, status, false, ct));
    [HttpGet("next")]
    public async Task<ActionResult<IReadOnlyList<MethodTaskDto>>> Next(Guid projectId, CancellationToken ct)
        => Ok(await tasks.ListAsync(projectId, null, true, ct));
    [HttpGet("{id}")]
    public async Task<ActionResult<MethodTaskDto>> Get(Guid projectId, string id, CancellationToken ct)
        => Ok(await tasks.GetAsync(projectId, id, ct));
    [HttpPost]
    public async Task<ActionResult<MethodTaskDto>> Create(Guid projectId, CreateMethodTaskRequest request, CancellationToken ct)
        => Ok(await tasks.CreateAsync(projectId, request, ct));
    [HttpPut("{id}")]
    public async Task<ActionResult<MethodTaskDto>> Update(Guid projectId, string id, UpdateMethodTaskRequest request, CancellationToken ct)
        => Ok(await tasks.UpdateAsync(projectId, id, request, ct));
    [HttpPost("{id}/move")]
    public async Task<ActionResult<MethodTaskDto>> Move(Guid projectId, string id, MoveMethodTaskRequest request, CancellationToken ct)
        => Ok(await tasks.MoveAsync(projectId, id, request, ct));
    [HttpPost("{id}/comments")]
    public async Task<ActionResult<MethodCommentDto>> Comment(Guid projectId, string id, MethodCommentRequest request, CancellationToken ct)
        => Ok(await tasks.CommentAsync(projectId, id, request, ct));
    [HttpPost("{itemId:guid}/resume-approval")]
    public async Task<ActionResult<ResumeMethodTaskApprovalDto>> ApproveResume(Guid projectId, Guid itemId, ResumeMethodTaskRequest request, CancellationToken ct)
        => Ok(await tasks.ApproveResumeAsync(projectId, itemId, request, ct));
}
