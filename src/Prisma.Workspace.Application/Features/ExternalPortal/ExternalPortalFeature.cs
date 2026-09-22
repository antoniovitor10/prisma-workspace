using System.Security.Cryptography;
using System.Text;
using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Prisma.Workspace.Application.Features.ExternalPortal;

public record ExternalPortalDto(
    Guid Id,
    Guid ProjectId,
    Guid BoardId,
    string ProjectName,
    string PublicSlug,
    bool IsEnabled,
    bool RequiresAuthentication,
    ExternalPortalAccessMode AccessModes,
    string PublicPath,
    IReadOnlyList<ExternalFormSummaryDto> Forms);

public record ExternalRequestMessageDto(
    Guid Id,
    ExternalRequestMessageAuthor AuthorType,
    string AuthorName,
    string Content,
    DateTimeOffset CreatedAt);

public record ExternalRequestAttachmentDto(Guid Id, string FileName, long? FileSize, string? MimeType, DateTimeOffset CreatedAt);

public record ExternalRequestDto(
    Guid Id,
    string Protocol,
    Guid ProjectId,
    string ProjectKey,
    string ProjectName,
    Guid BoardId,
    Guid WorkItemId,
    long WorkItemNumber,
    string Title,
    string? Description,
    string Status,
    Priority Priority,
    string RequesterName,
    string RequesterEmail,
    string? RequesterPhone,
    string? Category,
    string? RelatedService,
    Guid? ExternalFormId,
    string? ExternalFormTitle,
    ExternalRequestTriageStatus TriageStatus,
    string? ResponsibleId,
    Guid? TeamId,
    Guid? StageId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    int? Rating,
    string? RatingComment,
    DateTimeOffset? CompletionConfirmedAt,
    IReadOnlyDictionary<string, string?> SubmittedValues,
    IReadOnlyList<ExternalRequestTriageEventDto> TriageEvents,
    IReadOnlyList<ExternalRequestMessageDto> Messages,
    IReadOnlyList<ExternalRequestAttachmentDto> Attachments);

/// <summary>
/// Contrato deliberadamente mínimo da consulta por protocolo. Nunca acrescentar aqui
/// dados de triagem, roteamento, responsáveis, eventos internos, campos privados ou horas.
/// </summary>
public record PublicExternalRequestDto(
    string Protocol,
    string Title,
    string? Description,
    string Status,
    string? Category,
    string? RelatedService,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CompletedAt,
    int? Rating,
    string? RatingComment,
    DateTimeOffset? CompletionConfirmedAt,
    IReadOnlyList<ExternalRequestMessageDto> Messages,
    IReadOnlyList<ExternalRequestAttachmentDto> Attachments);

public record CreatedExternalRequestDto(
    string Protocol,
    string AccessKey,
    string TrackingPath,
    Guid WorkItemId,
    string? ConfirmationMessage = null,
    bool ConfirmationDelivered = false);
public record VerificationCodeDto(bool Delivered, DateTimeOffset ExpiresAt, string? DevelopmentCode);
public record PortalInvitationDto(string Token, string Email, DateTimeOffset ExpiresAt, string InvitationPath);

internal static class ExternalPortalMapper
{
    public static ExternalPortalDto Map(
        Prisma.Workspace.Domain.Entities.ExternalPortal portal, bool includeDisabledForms = true)
        => new(portal.Id, portal.ProjectId, portal.BoardId, portal.Project.Name, portal.PublicSlug,
            portal.IsEnabled, portal.RequiresAuthentication, portal.AccessModes, $"/portal/{portal.PublicSlug}",
            portal.Forms.Where(x => includeDisabledForms || x.IsEnabled)
                .OrderByDescending(x => x.IsDefault).ThenBy(x => x.Title)
                .Select(ExternalFormMapper.Summary).ToList());

    public static ExternalRequestDto MapInternal(ExternalRequest request)
    {
        var workItem = request.WorkItem;
        var project = workItem.Board.Project ?? request.ExternalPortal.Project!;
        var status = workItem.CompletedAt.HasValue
            ? "Concluída"
            : workItem.WorkflowStatus?.Name ?? workItem.Stage?.Name ?? "Nova";
        return new ExternalRequestDto(
            request.Id, request.Protocol, project.Id, project.Key, project.Name, workItem.BoardId,
            workItem.Id, workItem.Number, workItem.Title, workItem.Description, status,
            workItem.Priority, workItem.RequesterName ?? "Solicitante", request.RequesterEmail,
            request.RequesterPhone, request.Category, request.RelatedService,
            request.ExternalFormId, request.ExternalForm?.Title, request.TriageStatus,
            workItem.ResponsibleId, workItem.TeamId, workItem.StageId,
            request.CreatedAt, request.UpdatedAt, workItem.CompletedAt,
            request.Rating, request.RatingComment, request.CompletionConfirmedAt,
            ExternalFormSerialization.Values(request),
            request.TriageEvents.OrderBy(x => x.CreatedAt)
                .Select(x => new ExternalRequestTriageEventDto(
                    x.Id, x.Action, x.ActorName, x.Description, x.DataJson, x.CreatedAt)).ToList(),
            request.Messages.OrderBy(x => x.CreatedAt)
                .Select(x => new ExternalRequestMessageDto(x.Id, x.AuthorType, x.AuthorName, x.Content, x.CreatedAt))
                .ToList(),
            workItem.Attachments.Where(x => x.IsExternalVisible).OrderBy(x => x.CreatedAt)
                .Select(x => new ExternalRequestAttachmentDto(x.Id, x.FileName, x.FileSize, x.MimeType, x.CreatedAt))
                .ToList());
    }

    public static PublicExternalRequestDto MapPublic(ExternalRequest request)
    {
        var workItem = request.WorkItem;
        var status = workItem.CompletedAt.HasValue
            ? "Concluída"
            : workItem.WorkflowStatus?.Name ?? workItem.Stage?.Name ?? "Nova";
        return new PublicExternalRequestDto(
            request.Protocol, workItem.Title, workItem.Description, status,
            request.Category, request.RelatedService, request.CreatedAt, request.UpdatedAt,
            workItem.CompletedAt, request.Rating, request.RatingComment,
            request.CompletionConfirmedAt,
            request.Messages.OrderBy(x => x.CreatedAt)
                .Select(x => new ExternalRequestMessageDto(
                    x.Id, x.AuthorType, x.AuthorName, x.Content, x.CreatedAt)).ToList(),
            workItem.Attachments.Where(x => x.IsExternalVisible).OrderBy(x => x.CreatedAt)
                .Select(x => new ExternalRequestAttachmentDto(
                    x.Id, x.FileName, x.FileSize, x.MimeType, x.CreatedAt)).ToList());
    }
}

internal static class PortalSecurity
{
    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static string Hash(string secret)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret.Trim())));

    public static bool Matches(string secret, string hash)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(hash)) return false;
        var candidate = Encoding.ASCII.GetBytes(Hash(secret));
        var stored = Encoding.ASCII.GetBytes(hash);
        return candidate.Length == stored.Length && CryptographicOperations.FixedTimeEquals(candidate, stored);
    }

    public static string GenerateAccessKey()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(6));

    public static string GenerateInvitationToken()
        => Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

    public static string GenerateVerificationCode()
        => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public static void EnsureRequestAccess(ExternalRequest request, string accessKey, string? authenticatedEmail)
    {
        var authenticatedOwner = !string.IsNullOrWhiteSpace(authenticatedEmail)
            && string.Equals(NormalizeEmail(authenticatedEmail), request.RequesterEmail, StringComparison.Ordinal);
        if (!authenticatedOwner && !Matches(accessKey, request.AccessKeyHash))
            throw new NaoEncontradoException("Solicitação ou chave de acompanhamento");
    }
}

public record GetProjectExternalPortalQuery(Guid ProjectId, string ActorId) : IRequest<ExternalPortalDto?>;
public class GetProjectExternalPortalQueryHandler : IRequestHandler<GetProjectExternalPortalQuery, ExternalPortalDto?>
{
    private readonly IExternalPortalRepository _portals;
    private readonly IProjectAccessService _access;
    public GetProjectExternalPortalQueryHandler(IExternalPortalRepository portals, IProjectAccessService access)
        => (_portals, _access) = (portals, access);

    public async Task<ExternalPortalDto?> Handle(GetProjectExternalPortalQuery request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.Viewer, ct);
        var portal = await _portals.GetByProjectAsync(request.ProjectId, ct);
        return portal is null ? null : ExternalPortalMapper.Map(portal);
    }
}

public record UpsertExternalPortalCommand(
    Guid ProjectId,
    Guid BoardId,
    string PublicSlug,
    bool IsEnabled,
    bool RequiresAuthentication,
    ExternalPortalAccessMode AccessModes,
    string ActorId) : IRequest<ExternalPortalDto>;

public class UpsertExternalPortalCommandValidator : AbstractValidator<UpsertExternalPortalCommand>
{
    public UpsertExternalPortalCommandValidator()
    {
        RuleFor(x => x.BoardId).NotEmpty();
        RuleFor(x => x.PublicSlug).NotEmpty().MaximumLength(80);
    }
}

public class UpsertExternalPortalCommandHandler : IRequestHandler<UpsertExternalPortalCommand, ExternalPortalDto>
{
    private readonly IExternalPortalRepository _portals;
    private readonly IProjectRepository _projects;
    private readonly IProjectAccessService _access;
    public UpsertExternalPortalCommandHandler(
        IExternalPortalRepository portals, IProjectRepository projects, IProjectAccessService access)
        => (_portals, _projects, _access) = (portals, projects, access);

    public async Task<ExternalPortalDto> Handle(UpsertExternalPortalCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, ct);
        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NaoEncontradoException("Projeto");
        DomainException.Garantir(project.Boards.Any(x => x.Id == request.BoardId),
            "O quadro de entrada não pertence ao projeto.");
        var slug = Prisma.Workspace.Domain.Entities.ExternalPortal.NormalizeSlug(request.PublicSlug);
        var portal = await _portals.GetByProjectAsync(request.ProjectId, ct);
        DomainException.Garantir(!await _portals.SlugExistsAsync(slug, portal?.Id, ct),
            "Este endereço público já está em uso.");

        if (portal is null)
        {
            portal = Prisma.Workspace.Domain.Entities.ExternalPortal.Create(
                project.OrganizationId, project.Id, request.BoardId, slug,
                request.IsEnabled, request.RequiresAuthentication, request.AccessModes);
            portal.Project = project;
            portal.Board = project.Boards.Single(x => x.Id == request.BoardId);
            _portals.AddPortal(portal);
        }
        else
        {
            portal.Update(request.BoardId, slug, request.IsEnabled,
                request.RequiresAuthentication, request.AccessModes);
        }

        if (portal.Forms.Count == 0)
        {
            var defaultForm = ExternalForm.CreateDefault(portal.Id, project.Name);
            defaultForm.ExternalPortal = portal;
            portal.Forms.Add(defaultForm);
        }

        await _portals.SaveAsync(ct);
        return ExternalPortalMapper.Map(portal);
    }
}

public record CreatePortalInvitationCommand(Guid ProjectId, string Email, int ExpiresInDays, string ActorId)
    : IRequest<PortalInvitationDto>;
public class CreatePortalInvitationCommandValidator : AbstractValidator<CreatePortalInvitationCommand>
{
    public CreatePortalInvitationCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.ExpiresInDays).InclusiveBetween(1, 30);
    }
}
public class CreatePortalInvitationCommandHandler : IRequestHandler<CreatePortalInvitationCommand, PortalInvitationDto>
{
    private readonly IExternalPortalRepository _portals;
    private readonly IProjectAccessService _access;
    public CreatePortalInvitationCommandHandler(IExternalPortalRepository portals, IProjectAccessService access)
        => (_portals, _access) = (portals, access);

    public async Task<PortalInvitationDto> Handle(CreatePortalInvitationCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, ct);
        var portal = await _portals.GetByProjectAsync(request.ProjectId, ct)
            ?? throw new NaoEncontradoException("Portal externo");
        DomainException.Garantir(portal.AccessModes.HasFlag(ExternalPortalAccessMode.Invitation),
            "O acesso por convite não está habilitado.");
        var token = PortalSecurity.GenerateInvitationToken();
        var expiresAt = DateTimeOffset.UtcNow.AddDays(request.ExpiresInDays);
        _portals.AddInvitation(new ExternalPortalInvitation
        {
            Id = Guid.NewGuid(), ExternalPortalId = portal.Id,
            Email = PortalSecurity.NormalizeEmail(request.Email), TokenHash = PortalSecurity.Hash(token),
            ExpiresAt = expiresAt, CreatedAt = DateTimeOffset.UtcNow
        });
        await _portals.SaveAsync(ct);
        return new PortalInvitationDto(token, request.Email.Trim(), expiresAt,
            $"/portal/{portal.PublicSlug}?invite={token}&email={Uri.EscapeDataString(request.Email.Trim())}");
    }
}

public record GetPublicPortalQuery(string Slug) : IRequest<ExternalPortalDto>;
public class GetPublicPortalQueryHandler : IRequestHandler<GetPublicPortalQuery, ExternalPortalDto>
{
    private readonly IExternalPortalRepository _portals;
    public GetPublicPortalQueryHandler(IExternalPortalRepository portals) => _portals = portals;
    public async Task<ExternalPortalDto> Handle(GetPublicPortalQuery request, CancellationToken ct)
    {
        var portal = await _portals.GetBySlugAsync(
            Prisma.Workspace.Domain.Entities.ExternalPortal.NormalizeSlug(request.Slug), ct)
            ?? throw new NaoEncontradoException("Portal externo");
        return ExternalPortalMapper.Map(portal, includeDisabledForms: false);
    }
}

public record RequestPortalVerificationCodeCommand(string Slug, string Email) : IRequest<VerificationCodeDto>;
public class RequestPortalVerificationCodeCommandValidator : AbstractValidator<RequestPortalVerificationCodeCommand>
{
    public RequestPortalVerificationCodeCommandValidator()
        => RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
}
public class RequestPortalVerificationCodeCommandHandler
    : IRequestHandler<RequestPortalVerificationCodeCommand, VerificationCodeDto>
{
    private readonly IExternalPortalRepository _portals;
    private readonly IPortalEmailSender _emailSender;
    public RequestPortalVerificationCodeCommandHandler(IExternalPortalRepository portals, IPortalEmailSender emailSender)
        => (_portals, _emailSender) = (portals, emailSender);

    public async Task<VerificationCodeDto> Handle(RequestPortalVerificationCodeCommand request, CancellationToken ct)
    {
        var portal = await _portals.GetBySlugAsync(
            Prisma.Workspace.Domain.Entities.ExternalPortal.NormalizeSlug(request.Slug), ct)
            ?? throw new NaoEncontradoException("Portal externo");
        DomainException.Garantir(portal.AccessModes.HasFlag(ExternalPortalAccessMode.EmailCode),
            "O acesso por código de e-mail não está habilitado.");
        var email = PortalSecurity.NormalizeEmail(request.Email);
        var code = PortalSecurity.GenerateVerificationCode();
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(10);
        _portals.AddVerification(new ExternalPortalVerification
        {
            Id = Guid.NewGuid(), ExternalPortalId = portal.Id, Email = email,
            CodeHash = PortalSecurity.Hash(code), ExpiresAt = expiresAt, CreatedAt = DateTimeOffset.UtcNow
        });
        await _portals.SaveAsync(ct);
        var delivered = await _emailSender.SendVerificationCodeAsync(email, portal.Project.Name, code, ct);
        return new VerificationCodeDto(
            delivered, expiresAt, !delivered && _emailSender.CanExposeLocalCode ? code : null);
    }
}

public record CreateExternalRequestCommand(
    string Slug,
    string Title,
    string? Description,
    string RequesterName,
    string RequesterEmail,
    string? InvitationToken,
    string? VerificationCode,
    string? AuthenticatedUserId,
    string? AuthenticatedEmail) : IRequest<CreatedExternalRequestDto>;

public class CreateExternalRequestCommandValidator : AbstractValidator<CreateExternalRequestCommand>
{
    public CreateExternalRequestCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Description).MaximumLength(10000);
        RuleFor(x => x.RequesterName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.RequesterEmail).NotEmpty().EmailAddress().MaximumLength(320);
    }
}

public class CreateExternalRequestCommandHandler
    : IRequestHandler<CreateExternalRequestCommand, CreatedExternalRequestDto>
{
    private readonly IExternalPortalRepository _portals;
    private readonly IPlatformNotificationPublisher? _notifications;
    public CreateExternalRequestCommandHandler(
        IExternalPortalRepository portals,
        IPlatformNotificationPublisher? notifications = null)
        => (_portals, _notifications) = (portals, notifications);

    public async Task<CreatedExternalRequestDto> Handle(CreateExternalRequestCommand request, CancellationToken ct)
    {
        var portal = await _portals.GetBySlugAsync(
            Prisma.Workspace.Domain.Entities.ExternalPortal.NormalizeSlug(request.Slug), ct)
            ?? throw new NaoEncontradoException("Portal externo");
        var email = PortalSecurity.NormalizeEmail(request.RequesterEmail);
        var hasAccess = !portal.RequiresAuthentication;
        ExternalPortalInvitation? invitation = null;
        ExternalPortalVerification? verification = null;

        if (!hasAccess && portal.AccessModes.HasFlag(ExternalPortalAccessMode.Login)
            && !string.IsNullOrWhiteSpace(request.AuthenticatedUserId))
        {
            hasAccess = string.IsNullOrWhiteSpace(request.AuthenticatedEmail)
                || string.Equals(email, PortalSecurity.NormalizeEmail(request.AuthenticatedEmail), StringComparison.Ordinal);
        }
        if (!hasAccess && portal.AccessModes.HasFlag(ExternalPortalAccessMode.Invitation)
            && !string.IsNullOrWhiteSpace(request.InvitationToken))
        {
            invitation = await _portals.GetInvitationAsync(
                portal.Id, email, PortalSecurity.Hash(request.InvitationToken), ct);
            hasAccess = invitation is not null;
        }
        if (!hasAccess && portal.AccessModes.HasFlag(ExternalPortalAccessMode.EmailCode)
            && !string.IsNullOrWhiteSpace(request.VerificationCode))
        {
            verification = await _portals.GetVerificationAsync(
                portal.Id, email, PortalSecurity.Hash(request.VerificationCode), ct);
            hasAccess = verification is not null;
        }
        DomainException.Garantir(hasAccess,
            "Autentique-se, use um convite válido ou confirme o código enviado por e-mail.");

        var initialStatus = portal.Project.WorkflowStatuses
            .OrderBy(x => x.Position).FirstOrDefault(x => x.IsInitial);
        var initialStage = portal.Project.Stages.Where(x => x.BoardId == portal.BoardId).OrderBy(x => x.Position)
            .FirstOrDefault(x => initialStatus == null || x.WorkflowStatusId == initialStatus.Id)
            ?? portal.Project.Stages.Where(x => x.BoardId == portal.BoardId).OrderBy(x => x.Position).FirstOrDefault();
        DomainException.Garantir(initialStage is not null, "Configure uma coluna no quadro de entrada do portal.");
        var now = DateTimeOffset.UtcNow;
        var backlogRank = await _portals.GetNextBacklogRankAsync(portal.BoardId, ct);
        var workItem = new WorkItem
        {
            Id = Guid.NewGuid(), BoardId = portal.BoardId, TeamId = portal.Board.TeamId,
            StageId = initialStage!.Id, WorkflowStatusId = initialStage.WorkflowStatusId,
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Priority = Priority.Medium, Kind = WorkItemKind.Request, Origin = WorkItemOrigin.ExternalPortal,
            RequesterId = request.AuthenticatedUserId,
            RequesterName = request.RequesterName.Trim(), RequesterEmail = email,
            CreatedBy = request.AuthenticatedUserId,
            Position = (double)backlogRank,
            BacklogRank = backlogRank,
            CreatedAt = now, UpdatedAt = now
        };
        var accessKey = PortalSecurity.GenerateAccessKey();
        var externalRequest = new ExternalRequest
        {
            Id = Guid.NewGuid(), ExternalPortalId = portal.Id, WorkItemId = workItem.Id,
            AccessKeyHash = PortalSecurity.Hash(accessKey), RequesterEmail = email,
            CreatedAt = now, UpdatedAt = now
        };
        if (invitation is not null) invitation.UsedAt = now;
        if (verification is not null) verification.UsedAt = now;
        await _portals.CreateRequestAsync(workItem, externalRequest, ct);
        if (_notifications is not null)
            await _notifications.PublishAsync(new NotificationEnvelope(
                portal.OrganizationId, portal.Project.OwnerId, NotificationType.ExternalRequestReceived,
                "Nova solicitação recebida",
                $"{externalRequest.Protocol} · {workItem.Title}", $"/requests?request={externalRequest.Id}",
                workItem.Id, externalRequest.Id, portal.ProjectId), ct);
        return new CreatedExternalRequestDto(externalRequest.Protocol, accessKey,
            $"/portal/{portal.PublicSlug}/acompanhar?protocol={Uri.EscapeDataString(externalRequest.Protocol)}&key={accessKey}",
            workItem.Id);
    }
}

public record GetExternalRequestQuery(string Protocol, string AccessKey, string? AuthenticatedEmail)
    : IRequest<PublicExternalRequestDto>;
public class GetExternalRequestQueryHandler : IRequestHandler<GetExternalRequestQuery, PublicExternalRequestDto>
{
    private readonly IExternalPortalRepository _portals;
    public GetExternalRequestQueryHandler(IExternalPortalRepository portals) => _portals = portals;
    public async Task<PublicExternalRequestDto> Handle(GetExternalRequestQuery request, CancellationToken ct)
    {
        var externalRequest = await _portals.GetRequestByProtocolAsync(request.Protocol.Trim(), ct)
            ?? throw new NaoEncontradoException("Solicitação");
        PortalSecurity.EnsureRequestAccess(externalRequest, request.AccessKey, request.AuthenticatedEmail);
        return ExternalPortalMapper.MapPublic(externalRequest);
    }
}

public record GetInternalExternalRequestsQuery(string ActorId) : IRequest<IReadOnlyList<ExternalRequestDto>>;
public class GetInternalExternalRequestsQueryHandler
    : IRequestHandler<GetInternalExternalRequestsQuery, IReadOnlyList<ExternalRequestDto>>
{
    private readonly IExternalPortalRepository _portals;
    private readonly IProjectAccessService _access;
    public GetInternalExternalRequestsQueryHandler(IExternalPortalRepository portals, IProjectAccessService access)
        => (_portals, _access) = (portals, access);
    public async Task<IReadOnlyList<ExternalRequestDto>> Handle(GetInternalExternalRequestsQuery request, CancellationToken ct)
    {
        var requests = await _portals.GetRequestsAsync(ct);
        var allowedProjects = new HashSet<Guid>();
        foreach (var projectId in requests.SelectMany(x => new[]
                 {
                     x.ExternalPortal.ProjectId,
                     x.WorkItem.Board.ProjectId
                 }).Distinct())
            if (await _access.GetRoleAsync(projectId, request.ActorId, ct) is not null)
                allowedProjects.Add(projectId);
        return requests.Where(x => allowedProjects.Contains(x.ExternalPortal.ProjectId)
                || allowedProjects.Contains(x.WorkItem.Board.ProjectId))
            .Select(ExternalPortalMapper.MapInternal).ToList();
    }
}

public record AddExternalRequestReplyCommand(
    string Protocol,
    string AccessKey,
    string Content,
    string? AuthenticatedEmail) : IRequest<ExternalRequestMessageDto>;
public class AddExternalRequestReplyCommandValidator : AbstractValidator<AddExternalRequestReplyCommand>
{
    public AddExternalRequestReplyCommandValidator()
        => RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
}
public class AddExternalRequestReplyCommandHandler
    : IRequestHandler<AddExternalRequestReplyCommand, ExternalRequestMessageDto>
{
    private readonly IExternalPortalRepository _portals;
    private readonly IPlatformNotificationPublisher? _notifications;
    public AddExternalRequestReplyCommandHandler(
        IExternalPortalRepository portals,
        IPlatformNotificationPublisher? notifications = null)
        => (_portals, _notifications) = (portals, notifications);
    public async Task<ExternalRequestMessageDto> Handle(AddExternalRequestReplyCommand request, CancellationToken ct)
    {
        var externalRequest = await _portals.GetRequestByProtocolAsync(request.Protocol.Trim(), ct)
            ?? throw new NaoEncontradoException("Solicitação");
        PortalSecurity.EnsureRequestAccess(externalRequest, request.AccessKey, request.AuthenticatedEmail);
        var message = NewMessage(externalRequest, ExternalRequestMessageAuthor.Requester,
            externalRequest.WorkItem.RequesterName ?? "Solicitante", null, request.Content);
        _portals.AddMessage(message);
        if (externalRequest.TriageStatus == ExternalRequestTriageStatus.WaitingForInformation)
            externalRequest.TriageStatus = ExternalRequestTriageStatus.New;
        _portals.AddTaskEvent(TaskEvent.Registrar(
            externalRequest.WorkItemId, $"external:{externalRequest.RequesterEmail}",
            "external_requester_replied"));
        await _portals.SaveAsync(ct);
        if (_notifications is not null)
        {
            var item = externalRequest.WorkItem;
            var recipients = item.Assignees.Select(x => x.UserId)
                .Concat(item.Followers.Select(x => x.UserId))
                .Append(item.ResponsibleId ?? string.Empty)
                .Append(item.Board.Project?.OwnerId ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct();
            await _notifications.PublishManyAsync(recipients.Select(userId => new NotificationEnvelope(
                item.Board.OrganizationId, userId, NotificationType.ExternalReply,
                "Nova resposta do solicitante",
                $"{externalRequest.Protocol} recebeu uma nova resposta.",
                $"/requests?request={externalRequest.Id}", item.Id, externalRequest.Id,
                item.Board.ProjectId)), ct);
        }
        return new ExternalRequestMessageDto(message.Id, message.AuthorType, message.AuthorName, message.Content, message.CreatedAt);
    }

    internal static ExternalRequestMessage NewMessage(
        ExternalRequest request, ExternalRequestMessageAuthor authorType,
        string authorName, string? authorId, string content)
    {
        var now = DateTimeOffset.UtcNow;
        request.UpdatedAt = now;
        return new ExternalRequestMessage
        {
            Id = Guid.NewGuid(), ExternalRequestId = request.Id, AuthorType = authorType,
            AuthorName = authorName.Trim(), AuthorId = authorId,
            Content = content.Trim(), CreatedAt = now
        };
    }
}

public record AddInternalExternalRequestReplyCommand(
    string Protocol, string Content, string ActorId, string ActorName) : IRequest<ExternalRequestMessageDto>;
public class AddInternalExternalRequestReplyCommandValidator : AbstractValidator<AddInternalExternalRequestReplyCommand>
{
    public AddInternalExternalRequestReplyCommandValidator()
        => RuleFor(x => x.Content).NotEmpty().MaximumLength(4000);
}
public class AddInternalExternalRequestReplyCommandHandler
    : IRequestHandler<AddInternalExternalRequestReplyCommand, ExternalRequestMessageDto>
{
    private readonly IExternalPortalRepository _portals;
    private readonly IProjectAccessService _access;
    private readonly IPortalEmailSender _emailSender;
    private readonly IPlatformNotificationPublisher? _notifications;
    public AddInternalExternalRequestReplyCommandHandler(
        IExternalPortalRepository portals,
        IProjectAccessService access,
        IPortalEmailSender emailSender,
        IPlatformNotificationPublisher? notifications = null)
        => (_portals, _access, _emailSender, _notifications) = (portals, access, emailSender, notifications);
    public async Task<ExternalRequestMessageDto> Handle(AddInternalExternalRequestReplyCommand request, CancellationToken ct)
    {
        var externalRequest = await _portals.GetRequestByProtocolAsync(request.Protocol.Trim(), ct)
            ?? throw new NaoEncontradoException("Solicitação");
        await _access.EnsureAtLeastAsync(
            externalRequest.WorkItem.Board.ProjectId,
            request.ActorId, ProjectRole.Member, ct);
        var message = AddExternalRequestReplyCommandHandler.NewMessage(
            externalRequest, ExternalRequestMessageAuthor.Agent,
            request.ActorName, request.ActorId, request.Content);
        _portals.AddMessage(message);
        _portals.AddTaskEvent(TaskEvent.Registrar(
            externalRequest.WorkItemId, request.ActorId, "external_public_reply"));
        await _portals.SaveAsync(ct);
        if (_notifications is not null)
        {
            var item = externalRequest.WorkItem;
            var recipients = item.Assignees.Select(x => x.UserId)
                .Concat(item.Followers.Select(x => x.UserId))
                .Append(item.ResponsibleId ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x) && x != request.ActorId).Distinct();
            await _notifications.PublishManyAsync(recipients.Select(userId => new NotificationEnvelope(
                item.Board.OrganizationId, userId, NotificationType.PublicReply,
                "Resposta pública enviada",
                $"{request.ActorName} respondeu à solicitação {externalRequest.Protocol}.",
                $"/requests?request={externalRequest.Id}", item.Id, externalRequest.Id,
                item.Board.ProjectId)), ct);
        }
        var trackingPath = $"/portal/{externalRequest.ExternalPortal.PublicSlug}/acompanhar?protocol={Uri.EscapeDataString(externalRequest.Protocol)}";
        try
        {
            await _emailSender.SendPublicReplyAsync(
                externalRequest.RequesterEmail,
                externalRequest.ExternalPortal.Project.Name,
                externalRequest.Protocol,
                message.Content,
                trackingPath,
                ct);
        }
        catch
        {
            // A resposta pública persiste mesmo quando o provedor de e-mail estiver indisponível.
        }
        return new ExternalRequestMessageDto(message.Id, message.AuthorType, message.AuthorName, message.Content, message.CreatedAt);
    }
}

public record RateExternalRequestCommand(
    string Protocol, string AccessKey, int Rating, string? Comment, string? AuthenticatedEmail) : IRequest;
public class RateExternalRequestCommandValidator : AbstractValidator<RateExternalRequestCommand>
{
    public RateExternalRequestCommandValidator()
    {
        RuleFor(x => x.Rating).InclusiveBetween(1, 5);
        RuleFor(x => x.Comment).MaximumLength(1000);
    }
}
public class RateExternalRequestCommandHandler : IRequestHandler<RateExternalRequestCommand>
{
    private readonly IExternalPortalRepository _portals;
    public RateExternalRequestCommandHandler(IExternalPortalRepository portals) => _portals = portals;
    public async Task Handle(RateExternalRequestCommand request, CancellationToken ct)
    {
        var externalRequest = await _portals.GetRequestByProtocolAsync(request.Protocol.Trim(), ct)
            ?? throw new NaoEncontradoException("Solicitação");
        PortalSecurity.EnsureRequestAccess(externalRequest, request.AccessKey, request.AuthenticatedEmail);
        DomainException.Garantir(externalRequest.WorkItem.CompletedAt.HasValue,
            "A solicitação só pode ser avaliada depois de concluída.");
        externalRequest.Rate(request.Rating, request.Comment);
        await _portals.SaveAsync(ct);
    }
}

public record ConfirmExternalRequestCompletionCommand(
    string Protocol, string AccessKey, string? AuthenticatedEmail) : IRequest;
public class ConfirmExternalRequestCompletionCommandHandler : IRequestHandler<ConfirmExternalRequestCompletionCommand>
{
    private readonly IExternalPortalRepository _portals;
    public ConfirmExternalRequestCompletionCommandHandler(IExternalPortalRepository portals) => _portals = portals;
    public async Task Handle(ConfirmExternalRequestCompletionCommand request, CancellationToken ct)
    {
        var externalRequest = await _portals.GetRequestByProtocolAsync(request.Protocol.Trim(), ct)
            ?? throw new NaoEncontradoException("Solicitação");
        PortalSecurity.EnsureRequestAccess(externalRequest, request.AccessKey, request.AuthenticatedEmail);
        externalRequest.ConfirmCompletion();
        await _portals.SaveAsync(ct);
    }
}

public record UploadExternalRequestAttachmentCommand(
    string Protocol, string AccessKey, string FileName, string? MimeType,
    long Length, Stream Content, string? AuthenticatedEmail) : IRequest<ExternalRequestAttachmentDto>;
public class UploadExternalRequestAttachmentCommandHandler
    : IRequestHandler<UploadExternalRequestAttachmentCommand, ExternalRequestAttachmentDto>
{
    private readonly IExternalPortalRepository _portals;
    private readonly IFileStorage _storage;
    public UploadExternalRequestAttachmentCommandHandler(IExternalPortalRepository portals, IFileStorage storage)
        => (_portals, _storage) = (portals, storage);
    public async Task<ExternalRequestAttachmentDto> Handle(UploadExternalRequestAttachmentCommand request, CancellationToken ct)
    {
        var externalRequest = await _portals.GetRequestByProtocolAsync(request.Protocol.Trim(), ct)
            ?? throw new NaoEncontradoException("Solicitação");
        PortalSecurity.EnsureRequestAccess(externalRequest, request.AccessKey, request.AuthenticatedEmail);
        var form = externalRequest.ExternalForm;
        var maxSize = form?.MaxFileSizeBytes ?? 50_000_000;
        DomainException.Garantir(request.Length is > 0 && request.Length <= maxSize,
            $"O anexo deve ter no máximo {Math.Round(maxSize / 1_000_000m, 1)} MB.");
        if (form is not null)
        {
            DomainException.Garantir(externalRequest.WorkItem.Attachments.Count(x => x.IsExternalVisible) < form.MaxFiles,
                $"Este formulário aceita no máximo {form.MaxFiles} anexo(s).");
            var extensions = form.AllowedExtensions.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var mimeTypes = form.AllowedMimeTypes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            DomainException.Garantir(extensions.Contains(Path.GetExtension(request.FileName)),
                "A extensão do arquivo não é permitida.");
            DomainException.Garantir(!string.IsNullOrWhiteSpace(request.MimeType) && mimeTypes.Contains(request.MimeType),
                "O tipo do arquivo não é permitido.");
        }
        var id = Guid.NewGuid();
        var fileName = Path.GetFileName(request.FileName);
        var storedFileName = $"{id:N}{Path.GetExtension(fileName)}";
        var storagePath = await _storage.SaveAsync(
            externalRequest.WorkItemId.ToString("N"), storedFileName, request.Content, ct);
        var attachment = new Attachment
        {
            Id = id, WorkItemId = externalRequest.WorkItemId, StoragePath = storagePath,
            FileName = fileName, FileSize = request.Length,
            MimeType = string.IsNullOrWhiteSpace(request.MimeType) ? "application/octet-stream" : request.MimeType,
            UploadedBy = $"external:{externalRequest.RequesterEmail}", IsExternalVisible = true,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _portals.AddAttachment(attachment);
        externalRequest.UpdatedAt = attachment.CreatedAt;
        await _portals.SaveAsync(ct);
        return new ExternalRequestAttachmentDto(
            attachment.Id, attachment.FileName, attachment.FileSize, attachment.MimeType, attachment.CreatedAt);
    }
}

public record ExternalAttachmentFileDto(Stream Content, string MimeType, string FileName);
public record GetExternalRequestAttachmentQuery(
    string Protocol, string AccessKey, Guid AttachmentId, string? AuthenticatedEmail)
    : IRequest<ExternalAttachmentFileDto>;
public class GetExternalRequestAttachmentQueryHandler
    : IRequestHandler<GetExternalRequestAttachmentQuery, ExternalAttachmentFileDto>
{
    private readonly IExternalPortalRepository _portals;
    private readonly IFileStorage _storage;
    public GetExternalRequestAttachmentQueryHandler(IExternalPortalRepository portals, IFileStorage storage)
        => (_portals, _storage) = (portals, storage);
    public async Task<ExternalAttachmentFileDto> Handle(GetExternalRequestAttachmentQuery request, CancellationToken ct)
    {
        var externalRequest = await _portals.GetRequestByProtocolAsync(request.Protocol.Trim(), ct)
            ?? throw new NaoEncontradoException("Solicitação");
        PortalSecurity.EnsureRequestAccess(externalRequest, request.AccessKey, request.AuthenticatedEmail);
        var attachment = await _portals.GetVisibleAttachmentAsync(externalRequest.Id, request.AttachmentId, ct)
            ?? throw new NaoEncontradoException("Anexo");
        var content = _storage.OpenRead(attachment.StoragePath)
            ?? throw new NaoEncontradoException("Arquivo do anexo");
        return new ExternalAttachmentFileDto(content, attachment.MimeType ?? "application/octet-stream", attachment.FileName);
    }
}
