using System.Security.Claims;
using Detran.Kanban.Application.Features.Workflow;
using Detran.Kanban.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detran.Kanban.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/organizations/{organizationId:guid}/workflow-templates")]
public sealed class OrganizationWorkflowTemplatesController : ControllerBase
{
    private readonly IMediator _mediator;
    public OrganizationWorkflowTemplatesController(IMediator mediator) => _mediator = mediator;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Get(Guid organizationId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetOrganizationWorkflowTemplatesQuery(organizationId, UserId), ct));

    [HttpPost]
    public async Task<IActionResult> Create(
        Guid organizationId, [FromBody] OrganizationWorkflowTemplateInput request, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new CreateOrganizationWorkflowTemplateCommand(organizationId, request, UserId), ct);
        return CreatedAtAction(nameof(Get), new { organizationId }, result);
    }

    [HttpPut("{templateId:guid}")]
    public async Task<IActionResult> Update(
        Guid organizationId, Guid templateId,
        [FromBody] OrganizationWorkflowTemplateInput request, CancellationToken ct)
        => Ok(await _mediator.Send(
            new UpdateOrganizationWorkflowTemplateCommand(organizationId, templateId, request, UserId), ct));

    [HttpDelete("{templateId:guid}")]
    public async Task<IActionResult> Disable(Guid organizationId, Guid templateId, CancellationToken ct)
    {
        await _mediator.Send(new DisableOrganizationWorkflowTemplateCommand(
            organizationId, templateId, UserId), ct);
        return NoContent();
    }
}

public sealed record SetWorkflowInheritanceRequest(
    WorkflowInheritanceMode Mode, Guid? WorkflowTemplateId);
