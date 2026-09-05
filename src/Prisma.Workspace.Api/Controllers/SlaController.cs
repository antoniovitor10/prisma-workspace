using System.Security.Claims;
using Prisma.Workspace.Application.Features.Sla;
using Prisma.Workspace.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Prisma.Workspace.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/sla")]
public class SlaController : ControllerBase
{
    private readonly IMediator _mediator;
    public SlaController(IMediator mediator) => _mediator = mediator;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    [ProducesResponseType(typeof(ProjectSlaPolicyDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetProjectSlaPolicyQuery(projectId, UserId), ct));

    [HttpPut]
    [ProducesResponseType(typeof(ProjectSlaPolicyDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Upsert(
        Guid projectId,
        [FromBody] UpsertProjectSlaPolicyRequest request,
        CancellationToken ct)
        => Ok(await _mediator.Send(new UpsertProjectSlaPolicyCommand(
            projectId,
            request.IsEnabled,
            request.FirstResponseMinutes,
            request.ResolutionMinutes,
            request.ServiceStart,
            request.ServiceEnd,
            request.BusinessDaysMask,
            request.TimeZoneId,
            request.PauseWhileWaitingRequester,
            request.AlertsEnabled,
            request.NearDueMinutes,
            request.Holidays,
            request.Rules.Select((rule, index) => new SlaRuleDto(
                rule.Category,
                rule.Priority,
                rule.FirstResponseMinutes,
                rule.ResolutionMinutes,
                index * 100)).ToList(),
            UserId), ct));
}

public record UpsertProjectSlaPolicyRequest(
    bool IsEnabled,
    int FirstResponseMinutes,
    int ResolutionMinutes,
    TimeOnly ServiceStart,
    TimeOnly ServiceEnd,
    int BusinessDaysMask,
    string TimeZoneId,
    bool PauseWhileWaitingRequester,
    bool AlertsEnabled,
    int NearDueMinutes,
    IReadOnlyList<string> Holidays,
    IReadOnlyList<UpsertSlaRuleRequest> Rules);

public record UpsertSlaRuleRequest(
    string? Category,
    Priority? Priority,
    int? FirstResponseMinutes,
    int? ResolutionMinutes);
