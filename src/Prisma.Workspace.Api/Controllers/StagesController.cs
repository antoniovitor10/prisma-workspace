using Prisma.Workspace.Application.Features.Stages.Commands;
using Prisma.Workspace.Application.Features.Boards;
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

    [HttpGet("board/{boardId:guid}")]
    public async Task<IActionResult> GetByBoardId(Guid boardId, CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetStagesByBoardIdQuery(boardId, UserId), cancellationToken));

    /// <summary>
    /// Reordena as etapas de um projeto.
    /// O corpo é um array JSON de GUIDs na nova ordem.
    /// </summary>
    [HttpPut("board/{projectId:guid}/order")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reorder(
        Guid projectId,
        [FromBody] Guid[] orderedStageIds,
        CancellationToken cancellationToken)
    {
        var command = new ReorderBoardStagesCommand(projectId, orderedStageIds, UserId);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
    }

    [HttpGet("{stageId:guid}/impact")]
    public async Task<IActionResult> Impact(Guid stageId, [FromQuery] StageCategory category, CancellationToken ct)
        => Ok(await _mediator.Send(new GetBoardStageImpactQuery(stageId, category, UserId), ct));

    [HttpDelete("{stageId:guid}")]
    public async Task<IActionResult> Delete(Guid stageId, [FromQuery] Guid? destinationStageId, CancellationToken ct)
    {
        await _mediator.Send(new RemoveBoardStageCommand(stageId, destinationStageId, UserId), ct);
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

    [HttpPost("board/{boardId:guid}")]
    public async Task<IActionResult> CreateForBoard(Guid boardId, [FromBody] CreateStageRequest request, CancellationToken cancellationToken)
    {
        var id = await _mediator.Send(new CreateStageCommand(request.ProjectId, request.Name, request.Position,
            request.WorkflowStatusId, request.Category, request.Color, UserId, boardId), cancellationToken);
        return CreatedAtAction(nameof(GetByBoardId), new { boardId }, id);
    }

    /// <summary>
    /// Atualiza uma coluna/etapa existente do fluxo do projeto.
    /// </summary>
    [HttpPut("{stageId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid stageId,
        [FromBody] UpdateStageRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateBoardStageCommand(
            stageId, request.Name, request.Category, request.Color, request.ConfirmCategoryChange, UserId, request.ConfirmDescendants, request.ImpactToken);
        await _mediator.Send(command, cancellationToken);
        return NoContent();
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

/// <summary>
/// Modelo de request para atualizar uma coluna.
/// </summary>
public record UpdateStageRequest(
    string Name,
    StageCategory? Category = null,
    string? Color = null,
    bool ConfirmCategoryChange = false,
    bool ConfirmDescendants = false,
    string? ImpactToken = null);
