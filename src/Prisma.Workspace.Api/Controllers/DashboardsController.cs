using System.Security.Claims;
using Prisma.Workspace.Application.Features.Reports;
using Prisma.Workspace.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Prisma.Workspace.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboards")]
public class DashboardsController : ControllerBase
{
    private readonly IMediator _mediator;
    public DashboardsController(IMediator mediator) => _mediator = mediator;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("collaborator")]
    public async Task<IActionResult> Collaborator(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new GetCollaboratorDashboardQuery(from, to, UserId), cancellationToken));

    [HttpGet("manager")]
    public async Task<IActionResult> Manager(
        [FromQuery] Guid? projectId,
        [FromQuery] Guid? teamId,
        [FromQuery] string? userId,
        [FromQuery] string? status,
        [FromQuery] Priority? priority,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetManagerDashboardQuery(
            Filters(projectId, teamId, userId, status, priority, from, to), UserId), cancellationToken));

    [HttpGet("projects/{projectId:guid}")]
    public async Task<IActionResult> Project(
        Guid projectId,
        [FromQuery] Guid? teamId,
        [FromQuery] string? userId,
        [FromQuery] string? status,
        [FromQuery] Priority? priority,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetProjectDashboardQuery(
            projectId, Filters(projectId, teamId, userId, status, priority, from, to), UserId), cancellationToken));

    private static AnalyticsFiltersDto Filters(
        Guid? projectId, Guid? teamId, string? userId, string? status,
        Priority? priority, DateOnly? from, DateOnly? to)
        => new(projectId, teamId, userId, status, priority, from, to);
}
