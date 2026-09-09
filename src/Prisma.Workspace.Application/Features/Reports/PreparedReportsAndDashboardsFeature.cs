using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using MediatR;

namespace Prisma.Workspace.Application.Features.Reports;

public record ReportBreakdownDto(string Key, string Label, decimal Value);
public record ReportPeriodPointDto(string Period, decimal Value, decimal? SecondaryValue = null);
public record PreparedTaskSummaryDto(int Open, int Completed, int Overdue, int Blocked, int Total);
public record PreparedExternalSummaryDto(
    int Total, IReadOnlyList<ReportBreakdownDto> ByCategory,
    IReadOnlyList<ReportBreakdownDto> ByRequester);
public record PreparedHoursSummaryDto(decimal Planned, decimal Realized, decimal Variance);
public record PreparedSprintMetricDto(
    Guid Id, string Name, string Status, decimal PlannedPoints,
    decimal CompletedPoints, decimal Velocity, decimal ProgressPercentage);
public record PreparedReportsDto(
    DateOnly From,
    DateOnly To,
    PreparedTaskSummaryDto Tasks,
    IReadOnlyList<ReportBreakdownDto> TasksByResponsible,
    IReadOnlyList<ReportBreakdownDto> TasksByTeam,
    IReadOnlyList<ReportBreakdownDto> TasksByProject,
    IReadOnlyList<ReportBreakdownDto> TasksByStatus,
    IReadOnlyList<ReportBreakdownDto> TasksByPriority,
    IReadOnlyList<ReportBreakdownDto> TasksByOrigin,
    PreparedExternalSummaryDto ExternalRequests,
    PreparedHoursSummaryDto Hours,
    IReadOnlyList<PreparedSprintMetricDto> SprintVelocity,
    IReadOnlyList<ReportPeriodPointDto> Burndown,
    IReadOnlyList<ReportPeriodPointDto> WorkloadByPeriod);

public record AnalyticsFiltersDto(
    Guid? ProjectId,
    Guid? TeamId,
    string? UserId,
    string? Status,
    Priority? Priority,
    DateOnly? From,
    DateOnly? To);

public record GetPreparedReportsQuery(AnalyticsFiltersDto Filters, string ActorId)
    : IRequest<PreparedReportsDto>;

public class GetPreparedReportsQueryHandler
    : IRequestHandler<GetPreparedReportsQuery, PreparedReportsDto>
{
    private readonly IAnalyticsRepository _analytics;
    private readonly IUserDirectory _users;
    private readonly IPermissionService _permissions;
    private readonly IProjectAccessService _access;

    public GetPreparedReportsQueryHandler(
        IAnalyticsRepository analytics, IUserDirectory users,
        IPermissionService permissions, IProjectAccessService access)
        => (_analytics, _users, _permissions, _access) = (analytics, users, permissions, access);

    public async Task<PreparedReportsDto> Handle(GetPreparedReportsQuery request, CancellationToken ct)
    {
        await ReportAuthorization.EnsureViewAsync(
            request.ActorId, request.Filters.ProjectId, _permissions, _access, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = request.Filters.From ?? today.AddDays(-29);
        var to = request.Filters.To ?? today;
        DomainException.Garantir(to >= from && to.DayNumber - from.DayNumber <= 730,
            "O período deve ter entre 1 e 730 dias.");
        var items = (await _analytics.GetWorkItemsAsync(request.Filters.ProjectId, ct))
            .Where(item => Matches(item, request.Filters)).ToList();
        var requests = (await _analytics.GetExternalRequestsAsync(request.Filters.ProjectId, ct))
            .Where(requestItem => Matches(requestItem.WorkItem, request.Filters))
            .Where(x => InPeriod(x.CreatedAt, from, to)).ToList();
        var sprints = (await _analytics.GetSprintsAsync(request.Filters.ProjectId, ct))
            .Where(x => !request.Filters.TeamId.HasValue || x.TeamId == request.Filters.TeamId)
            .ToList();
        var userIds = items.Select(x => x.ResponsibleId).Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!).Distinct();
        var names = await _users.GetDisplayNamesAsync(userIds, ct);
        return Build(items, requests, sprints, names, from, to, request.Filters.UserId);
    }

    internal static PreparedReportsDto Build(
        IReadOnlyList<WorkItem> items,
        IReadOnlyList<ExternalRequest> requests,
        IReadOnlyList<Sprint> sprints,
        IReadOnlyDictionary<string, string> names,
        DateOnly from,
        DateOnly to,
        string? timeUserId = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var open = items.Where(x => x.CompletedAt == null && !x.IsArchived).ToList();
        var completed = items.Where(x => x.CompletedAt.HasValue
            && InPeriod(x.CompletedAt.Value, from, to)).ToList();
        var overdue = open.Count(x => x.DueDate < today);
        var blocked = open.Count(IsBlocked);
        var periodItems = items.Where(x => InPeriod(x.CreatedAt, from, to)
            || (x.CompletedAt.HasValue && InPeriod(x.CompletedAt.Value, from, to))).ToList();
        var entries = items.SelectMany(x => x.TimeEntries.Select(entry => new { Item = x, Entry = entry }))
            .Where(x => string.IsNullOrWhiteSpace(timeUserId) || x.Entry.UserId == timeUserId)
            .Where(x => EntryOverlaps(x.Entry, from, to)).ToList();
        var planned = periodItems.Sum(x => x.EstimatedHours ?? 0);
        var realized = Math.Round(entries.Sum(x => ClippedHours(x.Entry, from, to)), 2);

        var velocity = sprints.Where(x => x.Status is SprintStatus.Closed or SprintStatus.Active)
            .OrderBy(x => x.StartDate).TakeLast(12).Select(SprintMetric).ToList();
        var active = sprints.OrderByDescending(x => x.StartDate).FirstOrDefault(x => x.Status == SprintStatus.Active);

        return new PreparedReportsDto(
            from, to, new PreparedTaskSummaryDto(open.Count, completed.Count, overdue, blocked, periodItems.Count),
            Breakdown(periodItems, x => x.ResponsibleId is null ? "unassigned" : x.ResponsibleId,
                key => key == "unassigned" ? "Sem responsável" : names.GetValueOrDefault(key, key)),
            Breakdown(periodItems, x => (x.TeamId ?? x.Board.TeamId)?.ToString() ?? "unassigned",
                key => periodItems.FirstOrDefault(x => (x.TeamId ?? x.Board.TeamId)?.ToString() == key)?.Team?.Name
                    ?? periodItems.FirstOrDefault(x => x.Board.TeamId?.ToString() == key)?.Board.Team?.Name ?? "Sem equipe"),
            Breakdown(periodItems, x => x.Board.ProjectId?.ToString() ?? "legacy",
                key => periodItems.FirstOrDefault(x => x.Board.ProjectId?.ToString() == key)?.Board.Project?.Name ?? "Sem projeto"),
            Breakdown(periodItems, x => x.CompletedAt.HasValue ? "completed" : x.WorkflowStatus?.Name ?? x.Stage?.Name ?? "backlog",
                key => key == "completed" ? "Concluída" : key),
            Breakdown(periodItems, x => x.Priority.ToString(), key => PriorityLabel(key)),
            Breakdown(periodItems, x => x.Origin.ToString(), key => OriginLabel(key)),
            new PreparedExternalSummaryDto(requests.Count,
                Breakdown(requests, x => x.Category ?? "uncategorized", key => key == "uncategorized" ? "Sem categoria" : key),
                Breakdown(requests, x => x.RequesterEmail, key => requests.First(x => x.RequesterEmail == key).WorkItem.RequesterName ?? key)),
            new PreparedHoursSummaryDto(Math.Round(planned, 2), realized, Math.Round(realized - planned, 2)),
            velocity, active is null ? [] : Burndown(active, from, to),
            Workload(periodItems, from, to));
    }

    internal static bool Matches(WorkItem item, AnalyticsFiltersDto filters)
    {
        if (filters.TeamId.HasValue && (item.TeamId ?? item.Board.TeamId) != filters.TeamId) return false;
        if (!string.IsNullOrWhiteSpace(filters.UserId) && item.ResponsibleId != filters.UserId
            && item.Assignees.All(x => x.UserId != filters.UserId)) return false;
        if (filters.Priority.HasValue && item.Priority != filters.Priority) return false;
        if (!string.IsNullOrWhiteSpace(filters.Status))
        {
            var status = item.CompletedAt.HasValue ? "Concluída"
                : item.WorkflowStatus?.Name ?? item.Stage?.Name ?? "Backlog";
            if (!status.Equals(filters.Status, StringComparison.OrdinalIgnoreCase)) return false;
        }
        return true;
    }

    private static IReadOnlyList<ReportBreakdownDto> Breakdown<T>(
        IEnumerable<T> items, Func<T,string> key, Func<string,string> label)
        => items.GroupBy(key).Select(group => new ReportBreakdownDto(
                group.Key, label(group.Key), group.Count()))
            .OrderByDescending(x => x.Value).ThenBy(x => x.Label).ToList();

    private static PreparedSprintMetricDto SprintMetric(Sprint sprint)
    {
        var snapshot = sprint.ItemSnapshots.Count > 0;
        var planned = snapshot ? sprint.ItemSnapshots.Sum(x => x.Points ?? 0)
            : sprint.WorkItems.Sum(x => x.Points ?? 0);
        var completed = snapshot ? sprint.ItemSnapshots.Where(x => x.WasCompleted).Sum(x => x.Points ?? 0)
            : sprint.WorkItems.Where(x => x.CompletedAt.HasValue).Sum(x => x.Points ?? 0);
        return new PreparedSprintMetricDto(sprint.Id, sprint.Name, sprint.Status.ToString(),
            planned, completed, completed, planned == 0 ? 0 : Math.Round(completed * 100m / planned, 2));
    }

    private static IReadOnlyList<ReportPeriodPointDto> Burndown(Sprint sprint, DateOnly from, DateOnly to)
    {
        var start = sprint.StartDate > from ? sprint.StartDate : from;
        var end = sprint.EndDate < to ? sprint.EndDate : to;
        if (end < start) return [];
        var snapshotItems = sprint.ItemSnapshots.Count > 0
            ? sprint.ItemSnapshots.Select(x => (
                WorkItemCompletedAt: x.WorkItemCompletedAt,
                WasCompleted: x.WasCompleted)).ToList()
            : sprint.WorkItems.Select(x => (
                WorkItemCompletedAt: x.CompletedAt,
                WasCompleted: x.CompletedAt.HasValue)).ToList();
        var total = snapshotItems.Count;
        var days = end.DayNumber - start.DayNumber;
        return Enumerable.Range(0, days + 1).Select(index =>
        {
            var date = start.AddDays(index);
            var open = snapshotItems.Count(x => !x.WasCompleted
                || !x.WorkItemCompletedAt.HasValue
                || DateOnly.FromDateTime(x.WorkItemCompletedAt.Value.UtcDateTime) > date);
            var ideal = days == 0 ? 0 : Math.Max(0, total - total * index / (decimal)days);
            return new ReportPeriodPointDto(date.ToString("yyyy-MM-dd"), open, Math.Round(ideal, 2));
        }).ToList();
    }

    private static IReadOnlyList<ReportPeriodPointDto> Workload(
        IReadOnlyList<WorkItem> items, DateOnly from, DateOnly to)
    {
        var days = to.DayNumber - from.DayNumber;
        return Enumerable.Range(0, days + 1).Select(index =>
        {
            var date = from.AddDays(index);
            var created = items.Count(x => DateOnly.FromDateTime(x.CreatedAt.UtcDateTime) == date);
            var completed = items.Count(x => x.CompletedAt.HasValue
                && DateOnly.FromDateTime(x.CompletedAt.Value.UtcDateTime) == date);
            return new ReportPeriodPointDto(date.ToString("yyyy-MM-dd"), created, completed);
        }).ToList();
    }

    internal static bool IsBlocked(WorkItem item)
        => item.OutgoingLinks.Any(x => x.Type == WorkItemLinkType.DependsOn && x.TargetWorkItem.CompletedAt == null)
            || item.IncomingLinks.Any(x => x.Type == WorkItemLinkType.Blocks && x.SourceWorkItem.CompletedAt == null);
    private static bool InPeriod(DateTimeOffset value, DateOnly from, DateOnly to)
    { var date=DateOnly.FromDateTime(value.UtcDateTime);return date>=from&&date<=to; }
    internal static bool EntryOverlaps(TimeEntry entry, DateOnly from, DateOnly to)
    { var start=new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero);var end=new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue),TimeSpan.Zero);return entry.StartedAt<end&&(entry.EndedAt??DateTimeOffset.UtcNow)>start; }
    private static decimal ClippedHours(TimeEntry entry,DateOnly from,DateOnly to)
    { var start=new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue),TimeSpan.Zero);var end=new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue),TimeSpan.Zero);var actualStart=entry.StartedAt<start?start:entry.StartedAt;var naturalEnd=entry.EndedAt??DateTimeOffset.UtcNow;var actualEnd=naturalEnd>end?end:naturalEnd;return (decimal)Math.Max(0,(actualEnd-actualStart).TotalHours); }
    private static string PriorityLabel(string value)=>value switch{"Low"=>"Baixa","Medium"=>"Média","High"=>"Alta","Critical"=>"Crítica",_=>value};
    private static string OriginLabel(string value)=>value switch{"Internal"=>"Interna","ExternalPortal"=>"Portal externo","Form"=>"Formulário","Integration"=>"Integração","Import"=>"Importação",_=>value};
}

public record DashboardKpiDto(string Key, string Label, decimal Value, string? Unit = null, string? Tone = null);
public record DashboardSeriesDto(string Key, string Title, string Visualization, IReadOnlyList<ReportPeriodPointDto> Points);
public record DashboardListItemDto(Guid Id, long Number, string Title, string? Status, DateOnly? DueDate, string? ProjectKey);
public record DashboardDto(
    string Kind, DateOnly From, DateOnly To,
    IReadOnlyList<DashboardKpiDto> Kpis,
    IReadOnlyList<DashboardSeriesDto> Series,
    IReadOnlyList<DashboardListItemDto> Items);

public record GetManagerDashboardQuery(AnalyticsFiltersDto Filters, string ActorId) : IRequest<DashboardDto>;
public class GetManagerDashboardQueryHandler : IRequestHandler<GetManagerDashboardQuery, DashboardDto>
{
    private readonly IMediator _mediator;
    public GetManagerDashboardQueryHandler(IMediator mediator) => _mediator = mediator;
    public async Task<DashboardDto> Handle(GetManagerDashboardQuery request, CancellationToken ct)
    {
        var report = await _mediator.Send(new GetPreparedReportsQuery(request.Filters, request.ActorId), ct);
        return DashboardMapper.FromPrepared("manager", report);
    }
}

public record GetProjectDashboardQuery(Guid ProjectId, AnalyticsFiltersDto Filters, string ActorId) : IRequest<DashboardDto>;
public class GetProjectDashboardQueryHandler : IRequestHandler<GetProjectDashboardQuery, DashboardDto>
{
    private readonly IMediator _mediator;
    private readonly IAnalyticsRepository _analytics;
    public GetProjectDashboardQueryHandler(IMediator mediator, IAnalyticsRepository analytics)
        => (_mediator, _analytics) = (mediator, analytics);
    public async Task<DashboardDto> Handle(GetProjectDashboardQuery request, CancellationToken ct)
    {
        var filters = request.Filters with { ProjectId = request.ProjectId };
        var report = await _mediator.Send(new GetPreparedReportsQuery(filters, request.ActorId), ct);
        var dashboard = DashboardMapper.FromPrepared("project", report);
        var tasks = (await _analytics.GetWorkItemsAsync(request.ProjectId, ct))
            .Where(x => !x.IsArchived && GetPreparedReportsQueryHandler.Matches(x, filters)).ToList();
        var completed = tasks.Count(x => x.CompletedAt.HasValue);
        var progress = tasks.Count == 0 ? 0 : Math.Round(completed * 100m / tasks.Count, 2);
        var activeSprint = report.SprintVelocity.FirstOrDefault(x => x.Status == SprintStatus.Active.ToString());
        var kpis = new List<DashboardKpiDto>
        {
            new("progress", "Progresso do projeto", progress, "%"),
            new("tasks", "Tarefas do projeto", tasks.Count)
        };
        kpis.AddRange(dashboard.Kpis.Where(x => x.Key is "overdue" or "blocked" or "external"));
        if (activeSprint is not null)
            kpis.Add(new DashboardKpiDto(
                "currentSprint", $"Sprint atual · {activeSprint.Name}",
                activeSprint.ProgressPercentage, "%"));
        var series = dashboard.Series.Where(x => x.Key is "status" or "team" or "burndown" or "workload").ToList();
        series.Insert(1, DashboardMapper.BreakdownSeries(
            "responsible", "Tarefas por responsável", "bar", report.TasksByResponsible));
        var items = tasks.Where(x => x.CompletedAt == null)
            .OrderBy(x => x.DueDate == null).ThenBy(x => x.DueDate)
            .Take(30).Select(DashboardMapper.Item).ToList();
        return dashboard with { Kpis = kpis, Series = series, Items = items };
    }
}

public record GetCollaboratorDashboardQuery(DateOnly? From, DateOnly? To, string ActorId) : IRequest<DashboardDto>;
public class GetCollaboratorDashboardQueryHandler : IRequestHandler<GetCollaboratorDashboardQuery, DashboardDto>
{
    private readonly IAnalyticsRepository _analytics;
    private readonly IUserDirectory _users;
    public GetCollaboratorDashboardQueryHandler(IAnalyticsRepository analytics, IUserDirectory users)
        => (_analytics, _users) = (analytics, users);
    public async Task<DashboardDto> Handle(GetCollaboratorDashboardQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = request.From ?? today.AddDays(-6);
        var to = request.To ?? today.AddDays(14);
        var all = await _analytics.GetWorkItemsAsync(null, ct);
        var tasks = all.Where(x => !x.IsArchived && (x.ResponsibleId == request.ActorId
            || x.Assignees.Any(a => a.UserId == request.ActorId)
            || x.CreatedBy == request.ActorId || x.Followers.Any(f => f.UserId == request.ActorId))).ToList();
        var time = tasks.SelectMany(x => x.TimeEntries).Where(x => x.UserId == request.ActorId
            && GetPreparedReportsQueryHandler.EntryOverlaps(x, from, to)).ToList();
        var open = tasks.Where(x => x.CompletedAt == null).ToList();
        var hours = Math.Round(time.Sum(x => (decimal)Math.Max(0, ((x.EndedAt ?? DateTimeOffset.UtcNow) - x.StartedAt).TotalHours)), 2);
        var kpis = new List<DashboardKpiDto>
        {
            new("mine","Minhas tarefas",open.Count),
            new("today","Tarefas de hoje",open.Count(x=>x.DueDate==today)),
            new("overdue","Atrasadas",open.Count(x=>x.DueDate<today),null,"danger"),
            new("upcoming","Próximos prazos",open.Count(x=>x.DueDate>=today&&x.DueDate<=to)),
            new("hours","Horas registradas",hours,"h"),
            new("blocked","Bloqueios",open.Count(GetPreparedReportsQueryHandler.IsBlocked),null,"warning")
        };
        var items = open.OrderBy(x => x.DueDate == null).ThenBy(x => x.DueDate).Take(30)
            .Select(DashboardMapper.Item).ToList();
        return new DashboardDto("collaborator", from, to, kpis, [], items);
    }
}

internal static class DashboardMapper
{
    public static DashboardDto FromPrepared(string kind, PreparedReportsDto report)
    {
        var kpis = new List<DashboardKpiDto>
        {
            new("open","Tarefas abertas",report.Tasks.Open),
            new("completed","Concluídas no período",report.Tasks.Completed),
            new("overdue","Atrasadas",report.Tasks.Overdue,null,"danger"),
            new("blocked","Bloqueadas",report.Tasks.Blocked,null,"warning"),
            new("external","Solicitações externas",report.ExternalRequests.Total),
            new("planned","Horas previstas",report.Hours.Planned,"h"),
            new("realized","Horas realizadas",report.Hours.Realized,"h")
        };
        var series = new List<DashboardSeriesDto>
        {
            Series("status","Tarefas por status","donut",report.TasksByStatus),
            Series("team","Carga da equipe","bar",report.TasksByTeam),
            Series("projects","Projetos","column",report.TasksByProject),
            new("workload","Volume por período","line",report.WorkloadByPeriod)
        };
        if (report.SprintVelocity.Count > 0)
            series.Add(new DashboardSeriesDto(
                "velocity", "Velocidade das sprints", "column",
                report.SprintVelocity.Select(x => new ReportPeriodPointDto(
                    x.Name, x.Velocity, x.PlannedPoints)).ToList()));
        if (report.Burndown.Count > 0) series.Add(new("burndown","Burndown da sprint","line",report.Burndown));
        return new DashboardDto(kind, report.From, report.To, kpis, series, []);
    }
    public static DashboardListItemDto Item(WorkItem item) => new(
        item.Id,item.Number,item.Title,item.WorkflowStatus?.Name??item.Stage?.Name,item.DueDate,item.Board.Project?.Key);
    public static DashboardSeriesDto BreakdownSeries(string key,string title,string visualization,IReadOnlyList<ReportBreakdownDto> values)
        => new(key,title,visualization,values.Select(x=>new ReportPeriodPointDto(x.Label,x.Value)).ToList());
    private static DashboardSeriesDto Series(string key,string title,string visualization,IReadOnlyList<ReportBreakdownDto> values)
        => BreakdownSeries(key, title, visualization, values);
}

internal static class PreparedDashboardHelpers
{
}
