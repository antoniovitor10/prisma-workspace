using System.Security.Claims;
using Prisma.Workspace.Application.Features.Reports;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Prisma.Workspace.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports/projects/{projectId:guid}")]
public class ReportsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ReportsController(IMediator mediator) => _mediator = mediator;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("/api/reports/prepared")]
    [ProducesResponseType(typeof(PreparedReportsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPrepared(
        [FromQuery] Guid? projectId,
        [FromQuery] Guid? teamId,
        [FromQuery] string? userId,
        [FromQuery] string? status,
        [FromQuery] Prisma.Workspace.Domain.Enums.Priority? priority,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
        => Ok(await _mediator.Send(new GetPreparedReportsQuery(
            new AnalyticsFiltersDto(projectId, teamId, userId, status, priority, from, to), UserId), ct));

    [HttpGet("/api/reports/hours")]
    [ProducesResponseType(typeof(OrganizationHoursReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOrganizationHours(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? userId,
        [FromQuery] Guid? teamId,
        CancellationToken ct)
        => Ok(await _mediator.Send(new GetOrganizationHoursReportQuery(from, to, userId, teamId, UserId), ct));

    [HttpGet("time")]
    [ProducesResponseType(typeof(TimeReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTime(
        Guid projectId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] Guid? teamId,
        [FromQuery] string? userId,
        CancellationToken ct)
        => Ok(await _mediator.Send(new GetTimeReportQuery(
            projectId, from, to, teamId, userId, UserId), ct));

    [HttpGet("custom-fields")]
    [ProducesResponseType(typeof(CustomFieldReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomFields(
        Guid projectId,
        [FromQuery] Guid? fieldId,
        [FromQuery] string? value,
        CancellationToken ct)
        => Ok(await _mediator.Send(new GetCustomFieldReportQuery(
            projectId, fieldId, value, UserId), ct));
}
