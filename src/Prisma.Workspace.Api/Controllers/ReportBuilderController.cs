using System.Security.Claims;
using Prisma.Workspace.Application.Features.Reports;
using Prisma.Workspace.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Prisma.Workspace.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportBuilderController : ControllerBase
{
    private readonly IMediator _mediator;
    public ReportBuilderController(IMediator mediator) => _mediator = mediator;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("catalog")]
    public async Task<IActionResult> Catalog([FromQuery] Guid? projectId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetReportCatalogQuery(UserId, projectId), ct));

    [HttpGet("definitions")]
    public async Task<IActionResult> List([FromQuery] Guid? projectId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetSavedReportsQuery(UserId, projectId), ct));

    [HttpPost("definitions")]
    public async Task<IActionResult> Create(
        [FromBody] SaveReportRequest request, CancellationToken ct)
    {
        var created = await _mediator.Send(request.ToCommand(null, UserId), ct);
        return Created($"/api/reports/definitions/{created.Id}", created);
    }

    [HttpPut("definitions/{id:guid}")]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] SaveReportRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(request.ToCommand(id, UserId), ct));

    [HttpDelete("definitions/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new DeleteReportCommand(id, UserId), ct);
        return NoContent();
    }

    [HttpPost("definitions/{id:guid}/duplicate")]
    public async Task<IActionResult> Duplicate(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new DuplicateReportCommand(id, UserId), ct));

    [HttpPost("definitions/{id:guid}/run")]
    public async Task<IActionResult> Run(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new RunSavedReportQuery(id, UserId), ct));

    [HttpPost("preview")]
    public async Task<IActionResult> Preview(
        [FromBody] RunReportRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new RunAdHocReportQuery(
            request.ProjectId, request.Source, request.Visualization,
            request.Definition, UserId), ct));

    [HttpGet("definitions/{id:guid}/export")]
    public async Task<IActionResult> Export(Guid id, CancellationToken ct)
    {
        var export = await _mediator.Send(new ExportSavedReportQuery(id, UserId), ct);
        return File(export.Content, export.ContentType, export.FileName);
    }
}

public record SaveReportRequest(
    Guid? ProjectId,
    string Name,
    ReportDataSource Source,
    ReportVisualization Visualization,
    ReportDefinitionDto Definition,
    bool IsShared)
{
    public SaveReportCommand ToCommand(Guid? id, string actorId)
        => new(id, ProjectId, Name, Source, Visualization, Definition, IsShared, actorId);
}

public record RunReportRequest(
    Guid? ProjectId,
    ReportDataSource Source,
    ReportVisualization Visualization,
    ReportDefinitionDto Definition);
