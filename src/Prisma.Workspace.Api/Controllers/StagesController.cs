using Prisma.Workspace.Application.Features.Stages.Commands;
using Prisma.Workspace.Application.Features.Stages.Dtos;
using Prisma.Workspace.Application.Features.Stages.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Api.Controllers;

/// <summary>
/// Controller para gerenciar Etapas (colunas do fluxo do projeto).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class StagesController : ControllerBase
{
    private readonly IMediator _mediator;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public StagesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Retorna todas as colunas/etapas do fluxo de um projeto.
    /// </summary>
    [HttpGet("project/{projectId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<StageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetByProjectId(Guid projectId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetStagesByProjectIdQuery(projectId, UserId), cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Reordena as etapas de um projeto.
    /// O corpo é um array JSON de GUIDs na nova ordem.
    /// </summary>
    [HttpPut("project/{projectId:guid}/order")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reorder(
        Guid projectId,
        [FromBody] Guid[] orderedStageIds,
        CancellationToken cancellationToken)
    {
        var command = new ReorderStagesCommand(projectId, orderedStageIds, UserId);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Cria uma nova coluna/etapa no fluxo do projeto.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateStageRequest request,
        CancellationToken cancellationToken)
    {
        var command = new CreateStageCommand(
            request.ProjectId, request.Name, request.Position,
            request.WorkflowStatusId, request.Category, request.Color, UserId);
        var stageId = await _mediator.Send(command, cancellationToken);
        return CreatedAtAction(nameof(GetByProjectId), new { projectId = request.ProjectId }, stageId);
    }
}

/// <summary>
/// Modelo de request para criar uma coluna.
/// </summary>
public record CreateStageRequest(
    Guid ProjectId, string Name, double Position,
    Guid? WorkflowStatusId = null,
    StageCategory Category = StageCategory.InProgress,
    string Color = "#64748B");
