using System.Security.Claims;
using System.Text.Json;
using Detran.Kanban.Application.Features.ExternalPortal;
using Detran.Kanban.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Detran.Kanban.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/external-portal")]
public class ExternalPortalController : ControllerBase
{
    private readonly IMediator _mediator;
    public ExternalPortalController(IMediator mediator) => _mediator = mediator;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> Get(Guid projectId, CancellationToken ct)
    {
        var portal = await _mediator.Send(new GetProjectExternalPortalQuery(projectId, UserId), ct);
        return portal is null ? NoContent() : Ok(portal);
    }

    [HttpPut]
    public async Task<IActionResult> Upsert(
        Guid projectId, [FromBody] UpsertExternalPortalRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new UpsertExternalPortalCommand(
            projectId, request.BoardId, request.PublicSlug, request.IsEnabled,
            request.RequiresAuthentication, request.AccessModes, UserId), ct));

    [HttpPost("invitations")]
    public async Task<IActionResult> CreateInvitation(
        Guid projectId, [FromBody] CreatePortalInvitationRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new CreatePortalInvitationCommand(
            projectId, request.Email, request.ExpiresInDays, UserId), ct));

    [HttpGet("forms")]
    public async Task<IActionResult> GetForms(Guid projectId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetProjectExternalFormsQuery(projectId, UserId), ct));

    [HttpPost("forms")]
    public async Task<IActionResult> CreateForm(
        Guid projectId, [FromBody] SaveExternalFormRequest request, CancellationToken ct)
    {
        var created = await _mediator.Send(request.ToCommand(projectId, null, UserId), ct);
        return Created(created.PublicPath, created);
    }

    [HttpPut("forms/{formId:guid}")]
    public async Task<IActionResult> UpdateForm(
        Guid projectId, Guid formId, [FromBody] SaveExternalFormRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(request.ToCommand(projectId, formId, UserId), ct));
}

public record UpsertExternalPortalRequest(
    Guid BoardId,
    string PublicSlug,
    bool IsEnabled,
    bool RequiresAuthentication,
    ExternalPortalAccessMode AccessModes);
public record CreatePortalInvitationRequest(string Email, int ExpiresInDays = 7);
public record SaveExternalFormRequest(
    string PublicSlug,
    string Title,
    string? Description,
    string? Category,
    string? ConfirmationMessage,
    bool IsEnabled,
    bool IsDefault,
    Priority DefaultPriority,
    Guid? InitialStageId,
    Guid? DefaultTeamId,
    string? DefaultResponsibleId,
    int MaxFiles,
    long MaxFileSizeBytes,
    string AllowedExtensions,
    string AllowedMimeTypes,
    int MinimumCompletionSeconds,
    IReadOnlyList<ExternalFormFieldDto> Fields,
    IReadOnlyList<ExternalFormAssignmentRuleDto> AssignmentRules)
{
    public SaveExternalFormCommand ToCommand(Guid projectId, Guid? formId, string actorId)
        => new(projectId, formId, PublicSlug, Title, Description, Category, ConfirmationMessage,
            IsEnabled, IsDefault, DefaultPriority, InitialStageId, DefaultTeamId,
            DefaultResponsibleId, MaxFiles, MaxFileSizeBytes, AllowedExtensions,
            AllowedMimeTypes, MinimumCompletionSeconds, Fields, AssignmentRules, actorId);
}

[ApiController]
[Authorize]
[Route("api/external-requests")]
public class InternalExternalRequestsController : ControllerBase
{
    private readonly IMediator _mediator;
    public InternalExternalRequestsController(IMediator mediator) => _mediator = mediator;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private string UserName => User.FindFirstValue(ClaimTypes.Name)
        ?? User.FindFirstValue(ClaimTypes.Email)
        ?? "Atendente";

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
        => Ok(await _mediator.Send(new GetInternalExternalRequestsQuery(UserId), ct));

    [HttpPost("{protocol}/replies")]
    public async Task<IActionResult> Reply(
        string protocol, [FromBody] InternalExternalReplyRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new AddInternalExternalRequestReplyCommand(
            protocol, request.Content, UserId, UserName), ct));

    [HttpPost("{protocol}/triage")]
    public async Task<IActionResult> Triage(
        string protocol, [FromBody] ExternalRequestTriageRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new ApplyExternalRequestTriageCommand(
            protocol, request.Action, request.Reason, request.Message, request.Category,
            request.Priority, request.ResponsibleId, request.TeamId, request.ProjectId,
            request.BoardId, request.StageId, request.RelatedWorkItemId,
            request.RelatedWorkItemNumber, UserId, UserName), ct));
}

public record InternalExternalReplyRequest(string Content);
public record ExternalRequestTriageRequest(
    ExternalRequestTriageAction Action,
    string? Reason,
    string? Message,
    string? Category,
    Priority? Priority,
    string? ResponsibleId,
    Guid? TeamId,
    Guid? ProjectId,
    Guid? BoardId,
    Guid? StageId,
    Guid? RelatedWorkItemId,
    long? RelatedWorkItemNumber);

[ApiController]
[AllowAnonymous]
[Route("api/public")]
public class PublicExternalPortalController : ControllerBase
{
    private readonly IMediator _mediator;
    public PublicExternalPortalController(IMediator mediator) => _mediator = mediator;
    private string? AuthenticatedUserId => User.Identity?.IsAuthenticated == true
        ? User.FindFirstValue(ClaimTypes.NameIdentifier) : null;
    private string? AuthenticatedEmail => User.Identity?.IsAuthenticated == true
        ? User.FindFirstValue(ClaimTypes.Email) : null;

    [HttpGet("portals/{slug}")]
    public async Task<IActionResult> GetPortal(string slug, CancellationToken ct)
        => Ok(await _mediator.Send(new GetPublicPortalQuery(slug), ct));

    [HttpPost("portals/{slug}/verification-codes")]
    [EnableRateLimiting("external-public")]
    public async Task<IActionResult> RequestVerification(
        string slug, [FromBody] PortalVerificationRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new RequestPortalVerificationCodeCommand(slug, request.Email), ct));

    [HttpPost("portals/{slug}/requests")]
    [EnableRateLimiting("external-submissions")]
    public async Task<IActionResult> CreateRequest(
        string slug, [FromBody] PublicExternalRequest request, CancellationToken ct)
    {
        var created = await _mediator.Send(new CreateExternalRequestCommand(
            slug, request.Title, request.Description, request.RequesterName, request.RequesterEmail,
            request.InvitationToken, request.VerificationCode, AuthenticatedUserId, AuthenticatedEmail), ct);
        return Created(created.TrackingPath, created);
    }

    [HttpGet("portals/{portalSlug}/forms/{formSlug}")]
    public async Task<IActionResult> GetForm(
        string portalSlug, string formSlug, CancellationToken ct)
        => Ok(await _mediator.Send(new GetPublicExternalFormQuery(portalSlug, formSlug), ct));

    [HttpPost("portals/{portalSlug}/forms/{formSlug}/submissions")]
    [EnableRateLimiting("external-submissions")]
    [RequestSizeLimit(1_000_000_000)]
    public async Task<IActionResult> SubmitForm(
        string portalSlug,
        string formSlug,
        [FromForm] string payload,
        [FromForm] List<IFormFile>? files,
        CancellationToken ct)
    {
        PublicExternalFormSubmission? submission;
        try
        {
            submission = JsonSerializer.Deserialize<PublicExternalFormSubmission>(
                payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        }
        catch (JsonException)
        {
            return BadRequest(new { detail = "Dados do formulário inválidos." });
        }
        if (submission is null) return BadRequest(new { detail = "Dados do formulário obrigatórios." });

        var opened = new List<Stream>();
        try
        {
            var uploads = (files ?? []).Select(file =>
            {
                var stream = file.OpenReadStream();
                opened.Add(stream);
                return new ExternalFormSubmissionFile(
                    file.FileName, file.ContentType, file.Length, stream);
            }).ToList();
            var created = await _mediator.Send(new SubmitExternalFormCommand(
                portalSlug, formSlug, submission.Values, uploads, submission.StartedAt,
                submission.Website, submission.InvitationToken, submission.VerificationCode,
                AuthenticatedUserId, AuthenticatedEmail), ct);
            return Created(created.TrackingPath, created);
        }
        finally
        {
            foreach (var stream in opened) stream.Dispose();
        }
    }

    [HttpGet("requests/{protocol}")]
    public async Task<IActionResult> GetRequest(string protocol, [FromQuery] string key, CancellationToken ct)
        => Ok(await _mediator.Send(new GetExternalRequestQuery(protocol, key, AuthenticatedEmail), ct));

    [HttpPost("requests/{protocol}/replies")]
    public async Task<IActionResult> Reply(
        string protocol, [FromBody] PublicExternalReplyRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new AddExternalRequestReplyCommand(
            protocol, request.AccessKey, request.Content, AuthenticatedEmail), ct));

    [HttpPost("requests/{protocol}/attachments")]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> Upload(
        string protocol, [FromForm] string accessKey, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest(new { detail = "Arquivo vazio." });
        await using var content = file.OpenReadStream();
        var attachment = await _mediator.Send(new UploadExternalRequestAttachmentCommand(
            protocol, accessKey, file.FileName, file.ContentType, file.Length, content, AuthenticatedEmail), ct);
        return Created($"/api/public/requests/{protocol}/attachments/{attachment.Id}", attachment);
    }

    [HttpGet("requests/{protocol}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> Download(
        string protocol, Guid attachmentId, [FromQuery] string key, CancellationToken ct)
    {
        var file = await _mediator.Send(new GetExternalRequestAttachmentQuery(
            protocol, key, attachmentId, AuthenticatedEmail), ct);
        return File(file.Content, file.MimeType, file.FileName);
    }

    [HttpPost("requests/{protocol}/rating")]
    public async Task<IActionResult> Rate(
        string protocol, [FromBody] PublicExternalRatingRequest request, CancellationToken ct)
    {
        await _mediator.Send(new RateExternalRequestCommand(
            protocol, request.AccessKey, request.Rating, request.Comment, AuthenticatedEmail), ct);
        return NoContent();
    }

    [HttpPost("requests/{protocol}/confirm-completion")]
    public async Task<IActionResult> ConfirmCompletion(
        string protocol, [FromBody] PublicExternalAccessRequest request, CancellationToken ct)
    {
        await _mediator.Send(new ConfirmExternalRequestCompletionCommand(
            protocol, request.AccessKey, AuthenticatedEmail), ct);
        return NoContent();
    }
}

public record PortalVerificationRequest(string Email);
public record PublicExternalRequest(
    string Title,
    string? Description,
    string RequesterName,
    string RequesterEmail,
    string? InvitationToken,
    string? VerificationCode);
public record PublicExternalReplyRequest(string AccessKey, string Content);
public record PublicExternalRatingRequest(string AccessKey, int Rating, string? Comment);
public record PublicExternalAccessRequest(string AccessKey);
public record PublicExternalFormSubmission(
    IReadOnlyDictionary<string, string?> Values,
    DateTimeOffset StartedAt,
    string? Website,
    string? InvitationToken,
    string? VerificationCode);
