using System.Security.Claims;
using Detran.Kanban.Application.Features.Notifications;
using Detran.Kanban.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detran.Kanban.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/notifications")]
[Tags("Notificações")]
public sealed class NotificationsController : ControllerBase
{
    private readonly IMediator _mediator;
    public NotificationsController(IMediator mediator) => _mediator = mediator;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    [ProducesResponseType(typeof(NotificationPageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30,
        CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetNotificationsQuery(UserId, unreadOnly, page, pageSize), ct));

    [HttpPut("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        await _mediator.Send(new MarkNotificationReadCommand(id, UserId), ct);
        return NoContent();
    }

    [HttpPut("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await _mediator.Send(new MarkAllNotificationsReadCommand(UserId), ct);
        return NoContent();
    }

    [HttpGet("preferences")]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationPreferenceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPreferences(CancellationToken ct)
        => Ok(await _mediator.Send(new GetNotificationPreferencesQuery(UserId), ct));

    [HttpPut("preferences/{type}")]
    [ProducesResponseType(typeof(NotificationPreferenceDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetPreference(
        NotificationType type,
        [FromBody] NotificationPreferenceRequest request,
        CancellationToken ct)
        => Ok(await _mediator.Send(new SetNotificationPreferenceCommand(
            UserId, type, request.InAppEnabled, request.EmailEnabled), ct));
}

public sealed record NotificationPreferenceRequest(bool InAppEnabled, bool EmailEnabled);
