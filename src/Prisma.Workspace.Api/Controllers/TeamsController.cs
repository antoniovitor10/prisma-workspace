using System.Security.Claims;
using Prisma.Workspace.Application.Features.Teams;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Prisma.Workspace.Api.Controllers;

/// <summary>
/// Equipes: CRUD simples, membros e capacidade semanal.
/// </summary>
[ApiController]
[Route("api/teams")]
[Authorize]
public class TeamsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TeamsController(IMediator mediator)
    {
        _mediator = mediator;
    }
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>Equipes com membros e horas trabalhadas na semana corrente.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TeamDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetTeamsQuery(UserId), cancellationToken));
    }

    [HttpPost]
    [ProducesResponseType(typeof(TeamDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create([FromBody] TeamRequest request, CancellationToken cancellationToken)
    {
        var team = await _mediator.Send(new CreateTeamCommand(
            request.Name, request.DefaultWeeklyCapacityHours, UserId), cancellationToken);
        return Ok(team);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Update(
        Guid id, [FromBody] UpdateTeamRequest request, CancellationToken cancellationToken)
    {
        await _mediator.Send(new UpdateTeamCommand(
            id, request.Name, request.LeaderId,
            request.DefaultWeeklyCapacityHours, UserId), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetActive(
        Guid id, [FromBody] SetTeamActiveRequest request, CancellationToken cancellationToken)
    {
        await _mediator.Send(new SetTeamActiveCommand(id, request.IsActive, UserId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _mediator.Send(new SetTeamActiveCommand(id, false, UserId), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/members")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> AddMember(
        Guid id,
        [FromBody] TeamMemberRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(
            new AddTeamMemberCommand(id, request.UserId, request.WeeklyCapacityHours, UserId), cancellationToken);
        return NoContent();
    }

    [HttpDelete("{id:guid}/members/{userId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveMember(Guid id, string userId, CancellationToken cancellationToken)
    {
        await _mediator.Send(new RemoveTeamMemberCommand(id, userId, UserId), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/members/{userId}/capacity")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UpdateCapacity(
        Guid id, string userId, [FromBody] UpdateTeamCapacityRequest request,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new UpdateTeamMemberCapacityCommand(
            id, userId, request.WeeklyCapacityHours, UserId), cancellationToken);
        return NoContent();
    }
}

public record TeamRequest(string Name, decimal? DefaultWeeklyCapacityHours);
public record UpdateTeamRequest(string Name, string? LeaderId, decimal DefaultWeeklyCapacityHours);
public record SetTeamActiveRequest(bool IsActive);
public record TeamMemberRequest(string UserId, decimal? WeeklyCapacityHours);
public record UpdateTeamCapacityRequest(decimal WeeklyCapacityHours);
