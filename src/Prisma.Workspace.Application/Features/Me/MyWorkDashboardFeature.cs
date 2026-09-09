using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using MediatR;

namespace Prisma.Workspace.Application.Features.Me;

public sealed record MyWorkTaskDto(
    Guid Id, long Number, string Title, Guid BoardId, string BoardName,
    Guid? ProjectId, string? ProjectKey, string? StageName, string? StatusColor,
    Priority Priority, WorkItemKind Kind, WorkItemOrigin Origin,
    string? ResponsibleId, Guid? TeamId, string? TeamName, string? RequesterName,
    DateOnly? StartDate, DateOnly? DueDate, DateTimeOffset? CompletedAt,
    bool Assigned, bool Created, bool Following, bool Today, bool ThisWeek,
    bool Overdue, bool Blocked, bool ExternalRequest, bool UpcomingDeadline,
    IReadOnlyList<string> Tags,
    // Tempo ja gasto pelo usuario nesta tarefa (somente lancamentos encerrados;
    // o cronometro em andamento e somado no front, evitando contagem dupla).
    int UserTimeSeconds = 0);

public sealed record MyWorkCommentDto(
    Guid Id, Guid WorkItemId, long WorkItemNumber, string WorkItemTitle,
    string Author, string Content, DateTimeOffset CreatedAt, bool IsMention);
public sealed record MyWorkApprovalDto(
    Guid Id, Guid WorkItemId, long WorkItemNumber, string WorkItemTitle,
    string BoardName, DateTimeOffset CreatedAt);
public sealed record MyWorkNotificationDto(
    string Type, string Severity, string Title, string Message,
    Guid? WorkItemId, DateTimeOffset OccurredAt);
public sealed record MyWorkSummaryDto(
    int Assigned, int Today, int ThisWeek, int Overdue, int Blocked,
    int ExternalRequests, int PendingApprovals, int Mentions);
public sealed record MyWorkDashboardDto(
    MyWorkSummaryDto Summary, IReadOnlyList<MyWorkTaskDto> Tasks,
    IReadOnlyList<MyWorkCommentDto> Comments, IReadOnlyList<MyWorkCommentDto> Mentions,
    IReadOnlyList<MyWorkApprovalDto> PendingApprovals,
    IReadOnlyList<MyWorkTaskDto> UpcomingDeadlines,
    IReadOnlyList<MyWorkNotificationDto> ImportantNotifications);

public sealed record GetMyWorkDashboardQuery(string UserId) : IRequest<MyWorkDashboardDto>;
public sealed class GetMyWorkDashboardQueryHandler
    : IRequestHandler<GetMyWorkDashboardQuery, MyWorkDashboardDto>
{
    private readonly IMyWorkRepository _myWork;
    private readonly IApprovalRepository _approvals;
    private readonly IUserDirectory _users;
    public GetMyWorkDashboardQueryHandler(
        IMyWorkRepository myWork, IApprovalRepository approvals, IUserDirectory users)
        => (_myWork, _approvals, _users) = (myWork, approvals, users);

    public async Task<MyWorkDashboardDto> Handle(GetMyWorkDashboardQuery request, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(request.UserId, ct);
        var tasks = await _myWork.GetTasksAsync(request.UserId, ct);
        var comments = await _myWork.GetRecentCommentsAsync(request.UserId, user?.Email, 40, ct);
        var approvals = (await _approvals.GetByApproverAsync(request.UserId, 50, ct))
            .Where(x => x.EstaPendente).ToList();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekEnd = today.AddDays(7);
        var upcomingEnd = today.AddDays(14);

        var mappedTasks = tasks.Select(item => MapTask(item, request.UserId, today, weekEnd, upcomingEnd)).ToList();
        var mappedComments = comments.Select(x => new MyWorkCommentDto(
            x.Id, x.WorkItemId, x.WorkItem.Number, x.WorkItem.Title,
            string.IsNullOrWhiteSpace(x.UserName) ? "Usuário" : x.UserName,
            x.Content, x.CreatedAt,
            !string.IsNullOrWhiteSpace(user?.Email)
                && x.Content.Contains($"@{user.Email}", StringComparison.OrdinalIgnoreCase))).ToList();
        var mentions = mappedComments.Where(x => x.IsMention).ToList();
        var mappedApprovals = approvals.Select(x => new MyWorkApprovalDto(
            x.Id, x.WorkItemId, x.WorkItem.Number, x.WorkItem.Title,
            x.WorkItem.Board.Name, x.CreatedAt)).ToList();

        return new MyWorkDashboardDto(
            new MyWorkSummaryDto(
                mappedTasks.Count(x => x.Assigned), mappedTasks.Count(x => x.Today),
                mappedTasks.Count(x => x.ThisWeek), mappedTasks.Count(x => x.Overdue),
                mappedTasks.Count(x => x.Blocked), mappedTasks.Count(x => x.ExternalRequest && x.Assigned),
                mappedApprovals.Count, mentions.Count),
            mappedTasks, mappedComments, mentions, mappedApprovals,
            mappedTasks.Where(x => x.UpcomingDeadline).OrderBy(x => x.DueDate).ToList(),
            BuildNotifications(mappedTasks, mentions, mappedApprovals, tasks));
    }

    private static MyWorkTaskDto MapTask(
        WorkItem item, string userId, DateOnly today, DateOnly weekEnd, DateOnly upcomingEnd)
    {
        var assigned = item.ResponsibleId == userId || item.Assignees.Any(x => x.UserId == userId);
        var blocked = item.OutgoingLinks.Any(x => x.Type == WorkItemLinkType.DependsOn
                && x.TargetWorkItem.CompletedAt is null)
            || item.IncomingLinks.Any(x => x.Type == WorkItemLinkType.Blocks
                && x.SourceWorkItem.CompletedAt is null);
        var open = item.CompletedAt is null;
        return new MyWorkTaskDto(
            item.Id, item.Number, item.Title, item.BoardId, item.Board.Name,
            item.Board.ProjectId, item.Board.Project?.Key, item.Stage?.Name,
            item.WorkflowStatus?.Color, item.Priority, item.Kind, item.Origin,
            item.ResponsibleId, item.TeamId, item.Team?.Name, item.RequesterName,
            item.StartDate, item.DueDate, item.CompletedAt,
            assigned, item.CreatedBy == userId || item.RequesterId == userId,
            item.Followers.Any(x => x.UserId == userId),
            // "Meu trabalho" fala do que e SEU: prazos, atrasos e bloqueios so contam
            // quando voce e responsavel (criador/seguidor ve pela aba propria).
            assigned && open && item.DueDate == today,
            assigned && open && item.DueDate >= today && item.DueDate <= weekEnd,
            assigned && open && item.DueDate < today, assigned && open && blocked,
            item.Origin != WorkItemOrigin.Internal,
            assigned && open && item.DueDate >= today && item.DueDate <= upcomingEnd,
            item.WorkItemTags.Select(x => x.Tag.Name).ToList(),
            item.TimeEntries
                .Where(e => e.UserId == userId && e.EndedAt != null)
                .Sum(e => Math.Max(0, (int)(e.EndedAt!.Value - e.StartedAt).TotalSeconds)));
    }

    private static IReadOnlyList<MyWorkNotificationDto> BuildNotifications(
        IReadOnlyList<MyWorkTaskDto> tasks, IReadOnlyList<MyWorkCommentDto> mentions,
        IReadOnlyList<MyWorkApprovalDto> approvals,
        IReadOnlyList<WorkItem> sourceTasks)
    {
        var now = DateTimeOffset.UtcNow;
        var result = new List<MyWorkNotificationDto>();
        result.AddRange(tasks.Where(x => x.Overdue).Take(8).Select(x => new MyWorkNotificationDto(
            "overdue", "danger", $"#{x.Number} está atrasada", x.Title, x.Id, now)));
        result.AddRange(tasks.Where(x => x.Blocked).Take(8).Select(x => new MyWorkNotificationDto(
            "blocked", "warning", $"#{x.Number} está bloqueada", x.Title, x.Id, now)));
        result.AddRange(mentions.Take(8).Select(x => new MyWorkNotificationDto(
            "mention", "info", $"Você foi mencionado em #{x.WorkItemNumber}",
            x.Content, x.WorkItemId, x.CreatedAt)));
        result.AddRange(approvals.Take(8).Select(x => new MyWorkNotificationDto(
            "approval", "info", $"Aprovação pendente em #{x.WorkItemNumber}",
            x.WorkItemTitle, x.WorkItemId, x.CreatedAt)));
        return result.OrderByDescending(x => x.OccurredAt).Take(24).ToList();
    }
}
