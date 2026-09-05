using Detran.Kanban.Application.Common.Exceptions;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Detran.Kanban.Application.Features.Notifications;

public sealed record NotificationDto(
    Guid Id,
    NotificationType Type,
    string TypeName,
    string Title,
    string Message,
    string? Link,
    Guid? WorkItemId,
    Guid? ExternalRequestId,
    Guid? ProjectId,
    Guid? SprintId,
    bool IsRead,
    DateTimeOffset? ReadAt,
    DateTimeOffset CreatedAt);

public sealed record NotificationPageDto(
    IReadOnlyList<NotificationDto> Items,
    int Total,
    int Unread,
    int Page,
    int PageSize);

public sealed record NotificationPreferenceDto(
    NotificationType Type,
    string Name,
    bool InAppEnabled,
    bool EmailEnabled);

public sealed record GetNotificationsQuery(
    string UserId,
    bool UnreadOnly = false,
    int Page = 1,
    int PageSize = 30) : IRequest<NotificationPageDto>;

public sealed class GetNotificationsQueryValidator : AbstractValidator<GetNotificationsQuery>
{
    public GetNotificationsQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, NotificationPageDto>
{
    private readonly INotificationRepository _repository;
    public GetNotificationsQueryHandler(INotificationRepository repository) => _repository = repository;

    public async Task<NotificationPageDto> Handle(GetNotificationsQuery request, CancellationToken ct)
    {
        var result = await _repository.GetAsync(
            request.UserId, request.UnreadOnly, request.Page, request.PageSize, ct);
        return new NotificationPageDto(
            result.Items.Select(Map).ToList(), result.Total, result.Unread, request.Page, request.PageSize);
    }

    internal static NotificationDto Map(Notification item) => new(
        item.Id, item.Type, NotificationCatalog.Name(item.Type), item.Title, item.Message, item.Link,
        item.WorkItemId, item.ExternalRequestId, item.ProjectId, item.SprintId,
        item.IsRead, item.ReadAt, item.CreatedAt);
}

public sealed record MarkNotificationReadCommand(Guid NotificationId, string UserId) : IRequest;
public sealed class MarkNotificationReadCommandHandler : IRequestHandler<MarkNotificationReadCommand>
{
    private readonly INotificationRepository _repository;
    public MarkNotificationReadCommandHandler(INotificationRepository repository) => _repository = repository;
    public async Task Handle(MarkNotificationReadCommand request, CancellationToken ct)
    {
        var notification = await _repository.GetAsync(request.NotificationId, request.UserId, ct)
            ?? throw new NaoEncontradoException("Notificação");
        notification.MarkAsRead();
        await _repository.SaveAsync(ct);
    }
}

public sealed record MarkAllNotificationsReadCommand(string UserId) : IRequest;
public sealed class MarkAllNotificationsReadCommandHandler : IRequestHandler<MarkAllNotificationsReadCommand>
{
    private readonly INotificationRepository _repository;
    public MarkAllNotificationsReadCommandHandler(INotificationRepository repository) => _repository = repository;
    public Task Handle(MarkAllNotificationsReadCommand request, CancellationToken ct)
        => _repository.MarkAllAsReadAsync(request.UserId, ct);
}

public sealed record GetNotificationPreferencesQuery(string UserId)
    : IRequest<IReadOnlyList<NotificationPreferenceDto>>;
public sealed class GetNotificationPreferencesQueryHandler
    : IRequestHandler<GetNotificationPreferencesQuery, IReadOnlyList<NotificationPreferenceDto>>
{
    private readonly INotificationRepository _repository;
    public GetNotificationPreferencesQueryHandler(INotificationRepository repository) => _repository = repository;

    public async Task<IReadOnlyList<NotificationPreferenceDto>> Handle(
        GetNotificationPreferencesQuery request, CancellationToken ct)
    {
        var saved = (await _repository.GetPreferencesAsync(request.UserId, ct)).ToDictionary(x => x.Type);
        return Enum.GetValues<NotificationType>().Select(type =>
        {
            var defaults = NotificationCatalog.Default(type);
            return saved.TryGetValue(type, out var preference)
                ? new NotificationPreferenceDto(type, NotificationCatalog.Name(type),
                    preference.InAppEnabled, preference.EmailEnabled)
                : new NotificationPreferenceDto(type, NotificationCatalog.Name(type),
                    defaults.InApp, defaults.Email);
        }).ToList();
    }
}

public sealed record SetNotificationPreferenceCommand(
    string UserId,
    NotificationType Type,
    bool InAppEnabled,
    bool EmailEnabled) : IRequest<NotificationPreferenceDto>;

public sealed class SetNotificationPreferenceCommandValidator
    : AbstractValidator<SetNotificationPreferenceCommand>
{
    public SetNotificationPreferenceCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Type).IsInEnum();
    }
}

public sealed class SetNotificationPreferenceCommandHandler
    : IRequestHandler<SetNotificationPreferenceCommand, NotificationPreferenceDto>
{
    private readonly INotificationRepository _repository;
    private readonly IOrganizationContext _organization;
    public SetNotificationPreferenceCommandHandler(
        INotificationRepository repository,
        IOrganizationContext organization)
        => (_repository, _organization) = (repository, organization);

    public async Task<NotificationPreferenceDto> Handle(
        SetNotificationPreferenceCommand request, CancellationToken ct)
    {
        var preference = await _repository.GetPreferenceAsync(request.UserId, request.Type, ct);
        if (preference is null)
        {
            preference = new NotificationPreference
            {
                Id = Guid.NewGuid(),
                OrganizationId = _organization.RequireOrganizationId(),
                UserId = request.UserId,
                Type = request.Type
            };
            _repository.AddPreference(preference);
        }
        preference.Update(request.InAppEnabled, request.EmailEnabled);
        await _repository.SaveAsync(ct);
        return new NotificationPreferenceDto(
            request.Type, NotificationCatalog.Name(request.Type),
            request.InAppEnabled, request.EmailEnabled);
    }
}

public static class NotificationCatalog
{
    public static string Name(NotificationType type) => type switch
    {
        NotificationType.TaskAssigned => "Tarefa atribuída",
        NotificationType.Mention => "Menção",
        NotificationType.Comment => "Comentário",
        NotificationType.PublicReply => "Resposta pública",
        NotificationType.DeadlineNear => "Prazo próximo",
        NotificationType.TaskOverdue => "Tarefa atrasada",
        NotificationType.ExternalRequestReceived => "Solicitação recebida",
        NotificationType.ExternalReply => "Resposta externa",
        NotificationType.SlaNearDue => "SLA próximo do vencimento",
        NotificationType.SlaOverdue => "SLA vencido",
        NotificationType.SprintStarted => "Sprint iniciada",
        NotificationType.SprintCompleted => "Sprint encerrada",
        NotificationType.StatusChanged => "Mudança de status",
        _ => type.ToString()
    };

    public static (bool InApp, bool Email) Default(NotificationType type) => type switch
    {
        NotificationType.TaskAssigned or NotificationType.Mention
            or NotificationType.ExternalRequestReceived or NotificationType.ExternalReply
            or NotificationType.SlaNearDue or NotificationType.SlaOverdue => (true, true),
        _ => (true, false)
    };
}
