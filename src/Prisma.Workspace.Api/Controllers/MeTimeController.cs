using Prisma.Workspace.Application.Features.MeTime.Commands;
using Prisma.Workspace.Application.Features.MeTime.Dtos;
using Prisma.Workspace.Application.Features.MeTime.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Security.Claims;

namespace Prisma.Workspace.Api.Controllers;

/// <summary>
/// "Meu Tempo": ajuste por tarefa por dia e justificativas do dia.
/// </summary>
[ApiController]
[Route("api/me/time")]
[Authorize]
public class MeTimeController : ControllerBase
{
    private readonly IMediator _mediator;

    public MeTimeController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Usuário sem identificador.");

    /// <summary>Horas apontadas por tarefa em um dia (aba "Ajustar tarefas").</summary>
    [HttpGet("daily-by-task")]
    [ProducesResponseType(typeof(IReadOnlyList<TaskDayTimeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DailyByTask([FromQuery] string date, CancellationToken cancellationToken)
    {
        if (!TryParseDate(date, out var day))
            return BadRequest(new { detail = "Data inválida (use yyyy-MM-dd)." });

        return Ok(await _mediator.Send(new GetMyDailyByTaskQuery(UserId, day), cancellationToken));
    }

    /// <summary>Justificativas de um dia.</summary>
    [HttpGet("justifications")]
    [ProducesResponseType(typeof(IReadOnlyList<DayJustificationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJustifications([FromQuery] string date, CancellationToken cancellationToken)
    {
        if (!TryParseDate(date, out var day))
            return BadRequest(new { detail = "Data inválida (use yyyy-MM-dd)." });

        return Ok(await _mediator.Send(new GetDayJustificationsQuery(UserId, day), cancellationToken));
    }

    /// <summary>Cria uma justificativa do dia (férias, atestado, feriado...).</summary>
    [HttpPost("justifications")]
    [ProducesResponseType(typeof(DayJustificationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddJustification(
        [FromBody] JustificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseDate(request.Date, out var day))
            return BadRequest(new { detail = "Data inválida (use yyyy-MM-dd)." });

        return Ok(await _mediator.Send(
            new AddDayJustificationCommand(UserId, day, request.Reason, request.Hours), cancellationToken));
    }

    [HttpDelete("justifications/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteJustification(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteDayJustificationCommand(id, UserId), cancellationToken);
        return NoContent();
    }

    private static bool TryParseDate(string? value, out DateOnly date)
        => DateOnly.TryParse(value, CultureInfo.InvariantCulture, out date);
}

public record JustificationRequest(string Date, string Reason, decimal Hours);
