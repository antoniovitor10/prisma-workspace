using System.Security.Claims;
using Prisma.Workspace.Application.Features.Organizations;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Prisma.Workspace.Api.Controllers;

[ApiController]
[Route("api/organizations")]
[Authorize]
public class OrganizationsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IApplicationEmailSender _email;
    private readonly IConfiguration _configuration;
    public OrganizationsController(IMediator mediator, IApplicationEmailSender email, IConfiguration configuration)
        => (_mediator, _email, _configuration) = (mediator, email, configuration);
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> GetMine(CancellationToken ct)
        => Ok(await _mediator.Send(new GetOrganizationsQuery(UserId), ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrganizationRequest request, CancellationToken ct)
    {
        var organization = await _mediator.Send(
            new CreateOrganizationCommand(request.Name, request.Slug, UserId), ct);
        return Created("/api/organizations", organization);
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken ct)
        => Ok(await _mediator.Send(new GetCurrentOrganizationQuery(UserId), ct));

    [HttpPut("current")]
    public async Task<IActionResult> UpdateCurrent(
        [FromBody] UpdateOrganizationRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new UpdateOrganizationCommand(
            request.Name, request.Locale, request.TimeZone, request.WeekStartDay, UserId), ct));

    [HttpGet("current/access")]
    public async Task<IActionResult> GetAccess(CancellationToken ct)
        => Ok(await _mediator.Send(new GetOrganizationAccessQuery(UserId), ct));

    [HttpGet("current/members")]
    public async Task<IActionResult> GetMembers(CancellationToken ct)
        => Ok(await _mediator.Send(new GetOrganizationMembersQuery(UserId), ct));

    [HttpPut("current/members/{userId}")]
    public async Task<IActionResult> UpdateMember(
        string userId, [FromBody] UpdateOrganizationMemberRequest request, CancellationToken ct)
    {
        await _mediator.Send(new UpdateOrganizationMemberCommand(
            userId, request.Role, request.IsActive, request.DisplayName, UserId), ct);
        return NoContent();
    }

    [HttpPost("current/invitations")]
    public async Task<IActionResult> Invite(
        [FromBody] InviteOrganizationMemberRequest request, CancellationToken ct)
    {
        var invitation = await _mediator.Send(new InviteOrganizationMemberCommand(
            request.Email, request.Role, request.ExpiresInDays ?? 7, UserId), ct);

        var baseUrl = (_configuration["FrontendBaseUrl"] ?? "http://localhost:5173").TrimEnd('/');
        var link = $"{baseUrl}/?invite={invitation.Token}";
        await _email.SendAsync(invitation.Email, "Convite para o Detran Kanban",
            $"Você foi convidado para participar do Detran Kanban.\n\nAceite acessando: {link}\n\n"
            + $"O convite expira em {invitation.ExpiresAt:dd/MM/yyyy}.", ct);

        return Ok(invitation);
    }

    [HttpPost("invitations/accept")]
    public async Task<IActionResult> AcceptInvitation(
        [FromBody] AcceptOrganizationInvitationRequest request, CancellationToken ct)
        => Ok(new { organizationId = await _mediator.Send(
            new AcceptOrganizationInvitationCommand(request.Token, UserId), ct) });

    [HttpGet("current/permissions")]
    public async Task<IActionResult> GetPermissions(CancellationToken ct)
        => Ok(await _mediator.Send(new GetPermissionGrantsQuery(UserId), ct));

    [HttpPut("current/permissions")]
    public async Task<IActionResult> SetPermission(
        [FromBody] SetPermissionGrantRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new SetPermissionGrantCommand(
            request.UserId, request.Scope, request.ScopeId,
            request.Permission, request.IsAllowed, UserId), ct));

    [HttpDelete("current/permissions/{grantId:guid}")]
    public async Task<IActionResult> DeletePermission(Guid grantId, CancellationToken ct)
    {
        await _mediator.Send(new DeletePermissionGrantCommand(grantId, UserId), ct);
        return NoContent();
    }
}

public record CreateOrganizationRequest(string Name, string? Slug);
public record UpdateOrganizationRequest(string Name, string Locale, string TimeZone, DayOfWeek WeekStartDay);
public record UpdateOrganizationMemberRequest(OrganizationRole Role, bool IsActive, string? DisplayName);
public record InviteOrganizationMemberRequest(string Email, OrganizationRole Role, int? ExpiresInDays);
public record AcceptOrganizationInvitationRequest(string Token);
public record SetPermissionGrantRequest(
    string UserId, PermissionScope Scope, Guid? ScopeId,
    PlatformPermission Permission, bool IsAllowed);
