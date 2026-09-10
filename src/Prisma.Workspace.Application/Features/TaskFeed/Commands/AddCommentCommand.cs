using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.TaskFeed.Dtos;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using FluentValidation;
using MediatR;

namespace Prisma.Workspace.Application.Features.TaskFeed.Commands;

/// <summary>Adiciona um comentário no feed da tarefa.</summary>
public record AddCommentCommand(
    Guid WorkItemId,
    string UserId,
    string UserName,
    string Text,
    IReadOnlyList<string>? MentionedUserIds = null) : IRequest<CommentDto>;

public class AddCommentCommandValidator : AbstractValidator<AddCommentCommand>
{
    public AddCommentCommandValidator()
    {
        RuleFor(c => c.Text).NotEmpty().WithMessage("O comentário não pode ser vazio.");
    }
}

public class AddCommentCommandHandler : IRequestHandler<AddCommentCommand, CommentDto>
{
    private readonly ITaskFeedRepository _feed;
    private readonly IWorkItemRepository _workItems;
    private readonly IPlatformNotificationPublisher? _notifications;
    private readonly IUserDirectory _users;
    private readonly IWorkItemAccessService _access;

    public AddCommentCommandHandler(
        ITaskFeedRepository feed,
        IWorkItemRepository workItems,
        IWorkItemAccessService access,
        IUserDirectory users,
        IPlatformNotificationPublisher? notifications = null)
    {
        _feed = feed;
        _workItems = workItems;
        _access = access;
        _notifications = notifications;
        _users = users;
    }

    public async Task<CommentDto> Handle(AddCommentCommand request, CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(request.WorkItemId, request.UserId,
            PlatformPermission.Comment, ProjectRole.Viewer, cancellationToken);
        var item = await _workItems.GetByIdAsync(request.WorkItemId, cancellationToken)
            ?? throw new NaoEncontradoException("Tarefa");
        var actor = (await _users.GetByIdsAsync(
            [request.UserId], includeInactive: true, cancellationToken)).FirstOrDefault();
        var actorName = actor?.DisplayName
            ?? UserDisplayName.Resolve(request.UserId, null, request.UserName, null);

        var comment = await _feed.AddCommentAsync(new Comment
        {
            Id = Guid.NewGuid(),
            WorkItemId = request.WorkItemId,
            UserId = request.UserId,
            UserName = actorName,
            Content = request.Text.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        if (_notifications is not null)
        {
            var mentionedIds = new HashSet<string>(StringComparer.Ordinal);
            var organizationUsers = await _users.GetAllAsync(cancellationToken);
            var validIds = organizationUsers.Select(user => user.Id).ToHashSet(StringComparer.Ordinal);
            if (request.MentionedUserIds is { Count: > 0 })
            {
                // Menções explícitas vindas do seletor (@) do frontend.
                foreach (var id in request.MentionedUserIds.Where(validIds.Contains))
                    mentionedIds.Add(id);
            }
            else if (request.Text.Contains('@'))
            {
                // Compatibilidade: menção digitando o e-mail completo.
                foreach (var user in organizationUsers.Where(user =>
                    !string.IsNullOrWhiteSpace(user.Email)
                    && request.Text.Contains($"@{user.Email}", StringComparison.OrdinalIgnoreCase)))
                    mentionedIds.Add(user.Id);
            }

            var link = $"/projects/{item.Board.ProjectId}/backlog?item={item.Id}";
            var collaborators = item.Assignees.Select(x => x.UserId)
                .Concat(item.Followers.Select(x => x.UserId))
                .Append(item.ResponsibleId ?? string.Empty)
                .Append(item.CreatedBy ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x) && x != request.UserId)
                .Concat(mentionedIds.Where(x => x != request.UserId))
                .Distinct().ToList();
            var envelopes = collaborators.Select(userId => new NotificationEnvelope(
                item.Board.OrganizationId, userId,
                mentionedIds.Contains(userId) ? NotificationType.Mention : NotificationType.Comment,
                mentionedIds.Contains(userId) ? "Você foi mencionado" : "Novo comentário",
                $"{actorName} comentou em #{item.Number} {item.Title}.", link,
                WorkItemId: item.Id, ProjectId: item.Board.ProjectId));
            await _notifications.PublishManyAsync(envelopes, cancellationToken);
        }

        return CommentDto.From(comment);
    }
}
