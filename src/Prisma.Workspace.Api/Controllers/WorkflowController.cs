using System.Security.Claims;
using Prisma.Workspace.Application.Features.Workflow;
using Prisma.Workspace.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Prisma.Workspace.Api.Controllers;

[ApiController]
[Route("api/projects/{projectId:guid}/workflow")]
[Authorize]
public sealed class WorkflowController : ControllerBase
{
    private readonly IMediator _mediator;
    public WorkflowController(IMediator mediator) => _mediator = mediator;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<ActionResult<ProjectWorkflowDto>> Get(Guid projectId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetProjectWorkflowQuery(projectId, UserId), ct));

    [HttpPut("inheritance")]
    public async Task<IActionResult> SetInheritance(
        Guid projectId, [FromBody] SetWorkflowInheritanceRequest request, CancellationToken ct)
    {
        await _mediator.Send(new SetProjectWorkflowInheritanceCommand(
            projectId, request.Mode, request.WorkflowTemplateId, UserId), ct);
        return NoContent();
    }

    [HttpPost("statuses")]
    public async Task<ActionResult<WorkflowStatusDto>> CreateStatus(
        Guid projectId, [FromBody] WorkflowStatusRequest request, CancellationToken ct)
    {
        var status = await _mediator.Send(new CreateWorkflowStatusCommand(projectId,
            request.Name, request.Color, request.Position, request.Category,
            request.IsInitial, request.IsFinal, UserId), ct);
        return CreatedAtAction(nameof(Get), new { projectId }, status);
    }

    [HttpPut("statuses/{statusId:guid}")]
    public async Task<ActionResult<WorkflowStatusDto>> UpdateStatus(
        Guid projectId, Guid statusId, [FromBody] WorkflowStatusRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new UpdateWorkflowStatusCommand(projectId, statusId,
            request.Name, request.Color, request.Position, request.Category,
            request.IsInitial, request.IsFinal, UserId), ct));

    [HttpDelete("statuses/{statusId:guid}")]
    public async Task<IActionResult> DeleteStatus(Guid projectId, Guid statusId, CancellationToken ct)
    {
        await _mediator.Send(new DeleteWorkflowStatusCommand(projectId, statusId, UserId), ct);
        return NoContent();
    }

    [HttpPut("status-order")]
    public async Task<IActionResult> Reorder(
        Guid projectId, [FromBody] ReorderWorkflowRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ReorderWorkflowStatusesCommand(projectId, request.StatusIds, UserId), ct);
        return NoContent();
    }

    [HttpPut("transitions")]
    public async Task<IActionResult> ReplaceTransitions(
        Guid projectId, [FromBody] ReplaceTransitionsRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ReplaceWorkflowTransitionsCommand(projectId, request.Transitions, UserId), ct);
        return NoContent();
    }

    [HttpPut("stages/{stageId:guid}")]
    public async Task<IActionResult> UpdateStage(
        Guid projectId, Guid stageId, [FromBody] WorkflowStageRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateWorkflowStageCommand(projectId, stageId, request.Name,
            request.Position, request.WipLimit, request.WorkflowStatusId, UserId), ct);
        return NoContent();
    }

    [HttpDelete("stages/{stageId:guid}")]
    public async Task<IActionResult> DeleteStage(Guid projectId, Guid stageId, CancellationToken ct)
    {
        await _mediator.Send(new DeleteWorkflowStageCommand(projectId, stageId, UserId), ct);
        return NoContent();
    }

    [HttpPut("boards/{boardId:guid}/card-settings")]
    public async Task<IActionResult> UpdateCardSettings(
        Guid projectId, Guid boardId, [FromBody] BoardCardSettingsRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateBoardCardSettingsCommand(
            projectId, boardId, request.SettingsJson, UserId), ct);
        return NoContent();
    }
}

public sealed record WorkflowStatusRequest(
    string Name, string Color, double Position, StageCategory Category,
    bool IsInitial, bool IsFinal);
public sealed record ReorderWorkflowRequest(IReadOnlyList<Guid> StatusIds);
public sealed record ReplaceTransitionsRequest(IReadOnlyList<WorkflowTransitionDto> Transitions);
public sealed record WorkflowStageRequest(
    string Name, double Position, int? WipLimit, Guid WorkflowStatusId);
public sealed record BoardCardSettingsRequest(string SettingsJson);
