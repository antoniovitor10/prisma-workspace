using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Prisma.Workspace.Infrastructure.Services;

public sealed class NotificationDeliveryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationDeliveryWorker> _logger;
    public NotificationDeliveryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationDeliveryWorker> logger)
        => (_scopeFactory, _logger) = (scopeFactory, logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DeliverBatchAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { _logger.LogError(ex, "Falha no processamento da fila de notificações."); }
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
        }
    }

    private async Task DeliverBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IApplicationEmailSender>();
        var pending = await db.Notifications.IgnoreQueryFilters()
            .Where(x => x.EmailStatus == NotificationEmailStatus.Pending && x.EmailAttempts < 5)
            .OrderBy(x => x.CreatedAt).Take(20).ToListAsync(cancellationToken);
        if (pending.Count == 0) return;

        var userIds = pending.Select(x => x.UserId).Distinct().ToList();
        var emails = await db.Users.AsNoTracking().Where(x => userIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Email, cancellationToken);
        foreach (var notification in pending)
        {
            if (!sender.IsConfigured || !emails.TryGetValue(notification.UserId, out var email)
                || string.IsNullOrWhiteSpace(email))
            {
                notification.MarkEmailSkipped();
                continue;
            }

            try
            {
                var delivered = await sender.SendAsync(email, notification.Title,
                    $"{notification.Message}\n\nAcesse a plataforma para ver os detalhes.", cancellationToken);
                if (delivered) notification.MarkEmailSent();
                else notification.MarkEmailFailed("Falha temporária de entrega.");
            }
            catch (Exception ex)
            {
                notification.MarkEmailFailed("Falha temporária de entrega.");
                _logger.LogWarning(ex, "Falha ao entregar a notificação {NotificationId}.", notification.Id);
            }
        }
        await db.SaveChangesAsync(cancellationToken);
    }
}

public sealed class NotificationReminderWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationReminderWorker> _logger;
    public NotificationReminderWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationReminderWorker> logger)
        => (_scopeFactory, _logger) = (scopeFactory, logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await GenerateAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { _logger.LogError(ex, "Falha ao gerar lembretes de prazo."); }
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }

    private async Task GenerateAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPlatformNotificationPublisher>();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var tomorrow = today.AddDays(1);

        var tasks = await db.WorkItems.IgnoreQueryFilters().AsNoTracking()
            .Include(x => x.Assignees)
            .Include(x => x.Board).ThenInclude(x => x.Project)
            .Where(x => !x.IsArchived && !x.CompletedAt.HasValue && x.DueDate.HasValue
                && x.DueDate <= tomorrow)
            .OrderBy(x => x.DueDate).Take(500).ToListAsync(cancellationToken);
        var taskMessages = tasks.SelectMany(item =>
        {
            var isOverdue = item.DueDate < today;
            var recipients = item.Assignees.Select(x => x.UserId)
                .Append(item.ResponsibleId ?? string.Empty)
                .Append(item.Board.Project?.OwnerId ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct();
            var projectId = item.Board.ProjectId;
            var type = isOverdue ? NotificationType.TaskOverdue : NotificationType.DeadlineNear;
            return recipients.Select(userId => new NotificationEnvelope(
                item.Board.OrganizationId, userId, type,
                isOverdue ? "Tarefa atrasada" : "Prazo próximo",
                $"#{item.Number} {item.Title} vence em {item.DueDate:dd/MM/yyyy}.",
                projectId != Guid.Empty ? $"/projects/{projectId}/backlog?item={item.Id}" : "/my-work",
                WorkItemId: item.Id, ProjectId: projectId,
                DeduplicationKey: $"task-due:{item.Id}:{item.DueDate:yyyyMMdd}:{type}"));
        });
        await publisher.PublishManyAsync(taskMessages, cancellationToken);

    }
}
