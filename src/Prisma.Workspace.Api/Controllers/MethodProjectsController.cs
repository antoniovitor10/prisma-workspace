using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Features.MethodSecurity;

namespace Prisma.Workspace.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/method")]
public sealed class MethodProjectsController(IMethodProjectOperations projects) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<MethodIdentityDto>> Identity(Guid projectId, CancellationToken ct)
        => Ok(await projects.IdentityAsync(projectId, ct));
    [HttpGet("settings")]
    public async Task<ActionResult<MethodProjectSnapshot?>> Settings(Guid projectId, CancellationToken ct)
        => Ok(await projects.SettingsAsync(projectId, ct));
    [HttpPost("provision")]
    public async Task<ActionResult<MethodProjectSnapshot>> Provision(Guid projectId, ProvisionMethodProjectRequest request, CancellationToken ct)
        => Ok(await projects.ProvisionAsync(projectId, request, ct));
    [HttpPut("settings")]
    public async Task<ActionResult<MethodProjectSnapshot>> Update(Guid projectId, UpdateMethodProjectRequest request, CancellationToken ct)
        => Ok(await projects.UpdateAsync(projectId, request, ct));
    [HttpGet("rules")]
    public async Task<ActionResult<MethodRulesDto>> Rules(Guid projectId, CancellationToken ct)
        => Ok(await projects.RulesAsync(projectId, ct));
}

[ApiController]
[Authorize]
[Route("api/method")]
public sealed class MethodIdentityController(IMethodProjectOperations projects) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<MethodIdentityDto>> Identity(CancellationToken ct)
        => Ok(await projects.IdentityAsync(null, ct));
}
