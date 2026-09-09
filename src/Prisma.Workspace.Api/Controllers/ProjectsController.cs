using System.Security.Claims;
using Prisma.Workspace.Application.Features.Boards.Commands;
using Prisma.Workspace.Application.Features.Projects;
using Prisma.Workspace.Application.Features.Stages.Commands;
using Prisma.Workspace.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Prisma.Workspace.Api.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IMediator _mediator;
    public ProjectsController(IMediator mediator) => _mediator = mediator;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] bool includeArchived, CancellationToken ct)
        => Ok(await _mediator.Send(new GetProjectsQuery(UserId, includeArchived), ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetProjectQuery(id, UserId), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request, CancellationToken ct)
    {
        var id = await _mediator.Send(new CreateProjectCommand(
            request.Key, request.Name, request.Description, UserId,
            request.Nature, request.WorkType, request.Methodology, request.StartDate, request.DueDate, request.WorkflowTemplateId), ct);

        // Projeto nasce pronto pra uso: quadro principal + colunas padrão.
        // CreateBoardCommand já cria a etapa "Backlog" (Ready/pos=100);
        // adicionamos apenas Em andamento e Concluído para completar o fluxo.
        var boardId = await _mediator.Send(new CreateBoardCommand("Quadro principal", UserId, id), ct);
        await _mediator.Send(new CreateStageCommand(boardId, "Em andamento", 200, null, StageCategory.InProgress, "#F59E0B", UserId), ct);
        await _mediator.Send(new CreateStageCommand(boardId, "Concluído", 300, null, StageCategory.Done, "#10B981", UserId), ct);

        return CreatedAtAction(nameof(Get), new { id }, id);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProjectDetailsRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateProjectCommand(
            id, request.Name, request.Description, request.OwnerId,
            request.StartDate, request.DueDate, request.Status, request.Methodology,
            request.Nature, request.WorkType, request.SettingsJson, request.TagIds ?? [], UserId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new SetProjectArchivedCommand(id, true, UserId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reactivate")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new SetProjectArchivedCommand(id, false, UserId), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/members/{userId}")]
    public async Task<IActionResult> PutMember(Guid id, string userId, [FromBody] ProjectMemberRequest request, CancellationToken ct)
    {
        await _mediator.Send(new AddProjectMemberCommand(id, userId, request.Role, UserId), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/members/{userId}")]
    public async Task<IActionResult> RemoveMember(Guid id, string userId, CancellationToken ct)
    {
        await _mediator.Send(new RemoveProjectMemberCommand(id, userId, UserId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/teams/{teamId:guid}")]
    public async Task<IActionResult> AddTeam(Guid id, Guid teamId, CancellationToken ct)
    {
        await _mediator.Send(new AddProjectTeamCommand(id, teamId, UserId), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/teams/{teamId:guid}")]
    public async Task<IActionResult> RemoveTeam(Guid id, Guid teamId, CancellationToken ct)
    {
        await _mediator.Send(new RemoveProjectTeamCommand(id, teamId, UserId), ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/history")]
    public async Task<IActionResult> GetHistory(Guid id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetProjectHistoryQuery(id, UserId), ct));

    [HttpPost("{id:guid}/custom-fields")]
    public async Task<IActionResult> CreateCustomField(
        Guid id, [FromBody] ProjectCustomFieldRequest request, CancellationToken ct)
    {
        var fieldId = await _mediator.Send(new UpsertProjectCustomFieldCommand(
            id, null, request.Name, request.Type, request.IsRequired,
            request.OptionsJson, request.Position, UserId), ct);
        return CreatedAtAction(nameof(Get), new { id }, fieldId);
    }

    [HttpPut("{id:guid}/custom-fields/{fieldId:guid}")]
    public async Task<IActionResult> UpdateCustomField(
        Guid id, Guid fieldId, [FromBody] ProjectCustomFieldRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpsertProjectCustomFieldCommand(
            id, fieldId, request.Name, request.Type, request.IsRequired,
            request.OptionsJson, request.Position, UserId), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/custom-fields/{fieldId:guid}")]
    public async Task<IActionResult> DisableCustomField(Guid id, Guid fieldId, CancellationToken ct)
    {
        await _mediator.Send(new DisableProjectCustomFieldCommand(id, fieldId, UserId), ct);
        return NoContent();
    }
}

public record CreateProjectRequest(
    string? Key,
    string Name,
    string? Description,
    WorkNature Nature,
    WorkType WorkType,
    ProjectMethodology Methodology = ProjectMethodology.Kanban,
    DateOnly? StartDate = null,
    DateOnly? DueDate = null,
    Guid? WorkflowTemplateId = null);
public record UpdateProjectDetailsRequest(
    string Name,
    string? Description,
    string OwnerId,
    DateOnly? StartDate,
    DateOnly? DueDate,
    ProjectStatus Status,
    ProjectMethodology Methodology,
    WorkNature Nature,
    WorkType WorkType,
    string? SettingsJson,
    IReadOnlyList<Guid>? TagIds);
public record ProjectMemberRequest(ProjectRole Role);
public record ProjectCustomFieldRequest(
    string Name,
    CustomFieldType Type,
    bool IsRequired,
    string? OptionsJson,
    double Position);
