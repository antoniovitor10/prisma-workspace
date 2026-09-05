using System.Globalization;
using System.Text;
using System.Text.Json;
using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.Sla;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using MediatR;

namespace Prisma.Workspace.Application.Features.Reports;

public record ReportFieldDto(string Key, string Label, string Type, bool Numeric = false);
public record ReportSourceCatalogDto(
    ReportDataSource Source,
    string Name,
    IReadOnlyList<ReportFieldDto> Fields);
public record ReportCatalogDto(
    IReadOnlyList<ReportSourceCatalogDto> Sources,
    IReadOnlyList<string> FilterOperators,
    IReadOnlyList<ReportMetricOperation> Metrics,
    IReadOnlyList<ReportVisualization> Visualizations);

public record ReportFilterDto(string Field, string Operator, string? Value, string? ValueTo = null);
public record ReportMetricDto(ReportMetricOperation Operation, string? Field, string Label);
public record ReportDefinitionDto(
    IReadOnlyList<string> Columns,
    IReadOnlyList<ReportMetricDto> Metrics,
    IReadOnlyList<ReportFilterDto> Filters,
    string? GroupBy,
    string? OrderBy,
    bool Descending,
    DateOnly? From,
    DateOnly? To);

public record SavedReportDto(
    Guid Id,
    Guid? ProjectId,
    string OwnerId,
    string Name,
    ReportDataSource Source,
    ReportVisualization Visualization,
    ReportDefinitionDto Definition,
    bool IsShared,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record ReportExecutionDto(
    IReadOnlyList<ReportFieldDto> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    int SourceRowCount,
    int ResultRowCount,
    ReportVisualization Visualization);

public record GetReportCatalogQuery(string ActorId, Guid? ProjectId = null) : IRequest<ReportCatalogDto>;
public class GetReportCatalogQueryHandler : IRequestHandler<GetReportCatalogQuery, ReportCatalogDto>
{
    private readonly IPermissionService _permissions;
    private readonly IProjectAccessService _access;
    private readonly IProjectRepository _projects;
    public GetReportCatalogQueryHandler(
        IPermissionService permissions, IProjectAccessService access, IProjectRepository projects)
        => (_permissions, _access, _projects) = (permissions, access, projects);
    public async Task<ReportCatalogDto> Handle(GetReportCatalogQuery request, CancellationToken ct)
    {
        await ReportAuthorization.EnsureViewAsync(
            request.ActorId, request.ProjectId, _permissions, _access, ct);
        var customFields = new List<ReportFieldDto>();
        if (request.ProjectId.HasValue)
        {
            var project = await _projects.GetByIdAsync(request.ProjectId.Value, ct)
                ?? throw new NaoEncontradoException("Projeto");
            customFields.AddRange(project.CustomFields.Where(x => x.IsActive).Select(x => new ReportFieldDto(
                $"custom:{x.Id}", x.Name, CustomType(x.Type),
                x.Type is CustomFieldType.Number or CustomFieldType.Percentage)));
        }
        return ReportCatalog.Create(customFields);
    }

    private static string CustomType(CustomFieldType type) => type switch
    {
        CustomFieldType.Number or CustomFieldType.Percentage => "number",
        CustomFieldType.Date or CustomFieldType.DateTime => "date",
        CustomFieldType.Boolean => "boolean",
        _ => "text"
    };
}

public record GetSavedReportsQuery(string ActorId, Guid? ProjectId)
    : IRequest<IReadOnlyList<SavedReportDto>>;
public class GetSavedReportsQueryHandler
    : IRequestHandler<GetSavedReportsQuery, IReadOnlyList<SavedReportDto>>
{
    private readonly IAnalyticsRepository _analytics;
    private readonly IPermissionService _permissions;
    private readonly IProjectAccessService _access;
    public GetSavedReportsQueryHandler(
        IAnalyticsRepository analytics, IPermissionService permissions, IProjectAccessService access)
        => (_analytics, _permissions, _access) = (analytics, permissions, access);
    public async Task<IReadOnlyList<SavedReportDto>> Handle(GetSavedReportsQuery request, CancellationToken ct)
    {
        await ReportAuthorization.EnsureViewAsync(
            request.ActorId, request.ProjectId, _permissions, _access, ct);
        return (await _analytics.GetSavedReportsAsync(request.ActorId, request.ProjectId, ct))
            .Select(ReportDefinitionMapper.Map).ToList();
    }
}

public record SaveReportCommand(
    Guid? Id,
    Guid? ProjectId,
    string Name,
    ReportDataSource Source,
    ReportVisualization Visualization,
    ReportDefinitionDto Definition,
    bool IsShared,
    string ActorId) : IRequest<SavedReportDto>;

public class SaveReportCommandHandler : IRequestHandler<SaveReportCommand, SavedReportDto>
{
    private readonly IAnalyticsRepository _analytics;
    private readonly IOrganizationContext _organization;
    private readonly IPermissionService _permissions;
    private readonly IProjectAccessService _access;
    private readonly IProjectRepository _projects;

    public SaveReportCommandHandler(
        IAnalyticsRepository analytics,
        IOrganizationContext organization,
        IPermissionService permissions,
        IProjectAccessService access,
        IProjectRepository projects)
        => (_analytics, _organization, _permissions, _access, _projects)
            = (analytics, organization, permissions, access, projects);

    public async Task<SavedReportDto> Handle(SaveReportCommand request, CancellationToken ct)
    {
        await ReportAuthorization.EnsureCreateAsync(
            request.ActorId, request.ProjectId, _permissions, _access, ct);
        await ReportDefinitionValidator.ValidateAsync(
            request.ProjectId, request.Source, request.Visualization,
            request.Name, request.Definition, _projects, ct);
        var json = JsonSerializer.Serialize(request.Definition, ReportDefinitionMapper.JsonOptions);
        SavedReport report;
        if (request.Id.HasValue)
        {
            report = await _analytics.GetSavedReportAsync(request.Id.Value, ct)
                ?? throw new NaoEncontradoException("Relatório");
            DomainException.Garantir(report.OwnerId == request.ActorId,
                "Somente o proprietário pode editar ou compartilhar este relatório.");
            DomainException.Garantir(report.ProjectId == request.ProjectId,
                "O escopo do relatório não pode ser alterado.");
            report.Update(request.Name, request.Source, request.Visualization, json, request.IsShared);
        }
        else
        {
            report = SavedReport.Create(
                _organization.RequireOrganizationId(), request.ProjectId, request.ActorId,
                request.Name, request.Source, request.Visualization, json, request.IsShared);
            _analytics.AddSavedReport(report);
        }
        await _analytics.SaveAsync(ct);
        return ReportDefinitionMapper.Map(report);
    }
}

public record DuplicateReportCommand(Guid Id, string ActorId) : IRequest<SavedReportDto>;
public class DuplicateReportCommandHandler : IRequestHandler<DuplicateReportCommand, SavedReportDto>
{
    private readonly IAnalyticsRepository _analytics;
    private readonly IOrganizationContext _organization;
    private readonly IPermissionService _permissions;
    private readonly IProjectAccessService _access;
    public DuplicateReportCommandHandler(
        IAnalyticsRepository analytics, IOrganizationContext organization,
        IPermissionService permissions, IProjectAccessService access)
        => (_analytics, _organization, _permissions, _access)
            = (analytics, organization, permissions, access);
    public async Task<SavedReportDto> Handle(DuplicateReportCommand request, CancellationToken ct)
    {
        var source = await _analytics.GetSavedReportAsync(request.Id, ct)
            ?? throw new NaoEncontradoException("Relatório");
        DomainException.Garantir(source.OwnerId == request.ActorId || source.IsShared,
            "Relatório não compartilhado.");
        await ReportAuthorization.EnsureCreateAsync(
            request.ActorId, source.ProjectId, _permissions, _access, ct);
        var copy = SavedReport.Create(
            _organization.RequireOrganizationId(), source.ProjectId, request.ActorId,
            $"Cópia de {source.Name}", source.Source, source.Visualization,
            source.DefinitionJson, false);
        _analytics.AddSavedReport(copy);
        await _analytics.SaveAsync(ct);
        return ReportDefinitionMapper.Map(copy);
    }
}

public record DeleteReportCommand(Guid Id, string ActorId) : IRequest;
public class DeleteReportCommandHandler : IRequestHandler<DeleteReportCommand>
{
    private readonly IAnalyticsRepository _analytics;
    public DeleteReportCommandHandler(IAnalyticsRepository analytics) => _analytics = analytics;
    public async Task Handle(DeleteReportCommand request, CancellationToken ct)
    {
        var report = await _analytics.GetSavedReportAsync(request.Id, ct)
            ?? throw new NaoEncontradoException("Relatório");
        DomainException.Garantir(report.OwnerId == request.ActorId,
            "Somente o proprietário pode excluir este relatório.");
        _analytics.RemoveSavedReport(report);
        await _analytics.SaveAsync(ct);
    }
}

public record RunSavedReportQuery(Guid Id, string ActorId) : IRequest<ReportExecutionDto>;
public class RunSavedReportQueryHandler : IRequestHandler<RunSavedReportQuery, ReportExecutionDto>
{
    private readonly IAnalyticsRepository _analytics;
    private readonly ReportExecutor _executor;
    private readonly IPermissionService _permissions;
    private readonly IProjectAccessService _access;
    public RunSavedReportQueryHandler(
        IAnalyticsRepository analytics, ReportExecutor executor,
        IPermissionService permissions, IProjectAccessService access)
        => (_analytics, _executor, _permissions, _access) = (analytics, executor, permissions, access);
    public async Task<ReportExecutionDto> Handle(RunSavedReportQuery request, CancellationToken ct)
    {
        var report = await _analytics.GetSavedReportAsync(request.Id, ct)
            ?? throw new NaoEncontradoException("Relatório");
        DomainException.Garantir(report.OwnerId == request.ActorId || report.IsShared,
            "Relatório não compartilhado.");
        await ReportAuthorization.EnsureViewAsync(
            request.ActorId, report.ProjectId, _permissions, _access, ct);
        return await _executor.RunAsync(
            report.ProjectId, report.Source, report.Visualization,
            ReportDefinitionMapper.Definition(report), ct);
    }
}

public record RunAdHocReportQuery(
    Guid? ProjectId,
    ReportDataSource Source,
    ReportVisualization Visualization,
    ReportDefinitionDto Definition,
    string ActorId) : IRequest<ReportExecutionDto>;
public class RunAdHocReportQueryHandler : IRequestHandler<RunAdHocReportQuery, ReportExecutionDto>
{
    private readonly ReportExecutor _executor;
    private readonly IPermissionService _permissions;
    private readonly IProjectAccessService _access;
    private readonly IProjectRepository _projects;
    public RunAdHocReportQueryHandler(
        ReportExecutor executor, IPermissionService permissions,
        IProjectAccessService access, IProjectRepository projects)
        => (_executor, _permissions, _access, _projects) = (executor, permissions, access, projects);
    public async Task<ReportExecutionDto> Handle(RunAdHocReportQuery request, CancellationToken ct)
    {
        await ReportAuthorization.EnsureViewAsync(
            request.ActorId, request.ProjectId, _permissions, _access, ct);
        await ReportDefinitionValidator.ValidateAsync(
            request.ProjectId, request.Source, request.Visualization,
            "Prévia", request.Definition, _projects, ct);
        return await _executor.RunAsync(
            request.ProjectId, request.Source, request.Visualization, request.Definition, ct);
    }
}

public record ExportSavedReportQuery(Guid Id, string ActorId) : IRequest<ReportExportDto>;
public record ReportExportDto(byte[] Content, string FileName, string ContentType);
public class ExportSavedReportQueryHandler : IRequestHandler<ExportSavedReportQuery, ReportExportDto>
{
    private readonly IMediator _mediator;
    private readonly IAnalyticsRepository _analytics;
    public ExportSavedReportQueryHandler(IMediator mediator, IAnalyticsRepository analytics)
        => (_mediator, _analytics) = (mediator, analytics);
    public async Task<ReportExportDto> Handle(ExportSavedReportQuery request, CancellationToken ct)
    {
        var report = await _analytics.GetSavedReportAsync(request.Id, ct)
            ?? throw new NaoEncontradoException("Relatório");
        var result = await _mediator.Send(new RunSavedReportQuery(request.Id, request.ActorId), ct);
        var keys = result.Columns.Select(x => x.Key).ToList();
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(';', result.Columns.Select(x => Csv(x.Label))));
        foreach (var row in result.Rows)
            builder.AppendLine(string.Join(';', keys.Select(key => Csv(Format(row.GetValueOrDefault(key))))));
        return new ReportExportDto(
            new UTF8Encoding(true).GetBytes(builder.ToString()),
            $"{Slug(report.Name)}-{DateTime.UtcNow:yyyyMMdd}.csv", "text/csv; charset=utf-8");
    }
    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        DateOnly date => date.ToString("yyyy-MM-dd"),
        DateTimeOffset date => date.ToString("O"),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        double number => number.ToString(CultureInfo.InvariantCulture),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };
    private static string Csv(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
    private static string Slug(string value) => string.Concat(value.ToLowerInvariant()
        .Select(character => char.IsAsciiLetterOrDigit(character) ? character : '-')).Trim('-');
}

public class ReportExecutor
{
    private readonly IAnalyticsRepository _analytics;
    private readonly IUserDirectory _users;
    private readonly IProjectRepository _projects;
    public ReportExecutor(
        IAnalyticsRepository analytics, IUserDirectory users, IProjectRepository projects)
        => (_analytics, _users, _projects) = (analytics, users, projects);

    public async Task<ReportExecutionDto> RunAsync(
        Guid? projectId,
        ReportDataSource source,
        ReportVisualization visualization,
        ReportDefinitionDto definition,
        CancellationToken ct)
    {
        var rows = await SourceRowsAsync(source, projectId, ct);
        var sourceCount = rows.Count;
        rows = ApplyPeriod(source, rows, definition.From, definition.To);
        foreach (var filter in definition.Filters)
            rows = rows.Where(row => Matches(row.GetValueOrDefault(filter.Field), filter)).ToList();

        IReadOnlyList<Dictionary<string, object?>> projected;
        var columnKeys = new List<string>();
        if (definition.Metrics.Count > 0)
        {
            var groups = string.IsNullOrWhiteSpace(definition.GroupBy)
                ? new[] { new { Key = "Todos", Rows = rows } }
                : rows.GroupBy(row => Display(row.GetValueOrDefault(definition.GroupBy!)))
                    .Select(group => new { Key = group.Key, Rows = group.ToList() });
            if (!string.IsNullOrWhiteSpace(definition.GroupBy)) columnKeys.Add(definition.GroupBy!);
            projected = groups.Select(group =>
            {
                var result = new Dictionary<string, object?>();
                if (!string.IsNullOrWhiteSpace(definition.GroupBy))
                    result[definition.GroupBy!] = group.Key;
                foreach (var metric in definition.Metrics)
                {
                    if (metric.Operation == ReportMetricOperation.PlannedVersusActual)
                    {
                        result["plannedHours"] = Sum(group.Rows, "plannedHours");
                        result["realizedHours"] = Sum(group.Rows, "realizedHours");
                        result["varianceHours"] = Math.Round(
                            Convert.ToDecimal(result["realizedHours"], CultureInfo.InvariantCulture)
                            - Convert.ToDecimal(result["plannedHours"], CultureInfo.InvariantCulture), 2);
                        continue;
                    }
                    result[MetricKey(metric)] = Aggregate(group.Rows, metric);
                }
                return result;
            }).ToList();
            columnKeys.AddRange(projected.SelectMany(x => x.Keys).Where(x => !columnKeys.Contains(x)).Distinct());
        }
        else
        {
            columnKeys.AddRange(definition.Columns);
            projected = rows.Select(row => definition.Columns.ToDictionary(
                key => key, key => row.GetValueOrDefault(key))).ToList();
        }

        var orderBy = string.IsNullOrWhiteSpace(definition.OrderBy)
            ? columnKeys.FirstOrDefault() : definition.OrderBy;
        if (!string.IsNullOrWhiteSpace(orderBy) && columnKeys.Contains(orderBy))
            projected = (definition.Descending
                    ? projected.OrderByDescending(row => SortValue(row.GetValueOrDefault(orderBy)))
                    : projected.OrderBy(row => SortValue(row.GetValueOrDefault(orderBy))))
                .ToList();
        projected = projected.Take(2_000).ToList();
        var catalogFields = ReportCatalog.Fields(source).ToList();
        if (projectId.HasValue && source is ReportDataSource.WorkItems or ReportDataSource.ExternalRequests)
        {
            var project = await _projects.GetByIdAsync(projectId.Value, ct);
            if (project is not null)
                catalogFields.AddRange(project.CustomFields.Where(x => x.IsActive).Select(x => new ReportFieldDto(
                    $"custom:{x.Id}", x.Name,
                    x.Type is CustomFieldType.Number or CustomFieldType.Percentage ? "number"
                        : x.Type is CustomFieldType.Date or CustomFieldType.DateTime ? "date"
                        : x.Type == CustomFieldType.Boolean ? "boolean" : "text",
                    x.Type is CustomFieldType.Number or CustomFieldType.Percentage)));
        }
        var catalog = catalogFields.ToDictionary(x => x.Key);
        var columns = columnKeys.Select(key => catalog.GetValueOrDefault(key)
            ?? new ReportFieldDto(key, MetricLabel(key, definition.Metrics), "number", true)).ToList();
        return new ReportExecutionDto(columns, projected, sourceCount, projected.Count, visualization);
    }

    private async Task<List<Dictionary<string, object?>>> SourceRowsAsync(
        ReportDataSource source, Guid? projectId, CancellationToken ct)
    {
        var users = await _users.GetAllAsync(ct);
        var names = users.ToDictionary(x => x.Id, x => x.UserName ?? x.Email ?? x.Id);
        return source switch
        {
            ReportDataSource.WorkItems => (await _analytics.GetWorkItemsAsync(projectId, ct))
                .Select(item => TaskRow(item, names)).ToList(),
            ReportDataSource.ExternalRequests => (await _analytics.GetExternalRequestsAsync(projectId, ct))
                .Select(RequestRow).ToList(),
            ReportDataSource.Slas => (await _analytics.GetExternalRequestsAsync(projectId, ct))
                .Where(x => x.FirstResponseDueAt.HasValue || x.ResolutionDueAt.HasValue)
                .Select(SlaRow).ToList(),
            ReportDataSource.Projects => (await _analytics.GetProjectsAsync(ct))
                .Where(x => !projectId.HasValue || x.Id == projectId).Select(ProjectRow).ToList(),
            ReportDataSource.Teams => (await _analytics.GetTeamsAsync(ct))
                .Where(x => !projectId.HasValue || x.Projects.Any(link => link.ProjectId == projectId.Value))
                .Select(TeamRow).ToList(),
            ReportDataSource.Users => await UserRowsAsync(projectId, users, names, ct),
            ReportDataSource.Sprints => (await _analytics.GetSprintsAsync(projectId, ct))
                .Select(SprintRow).ToList(),
            ReportDataSource.TimeEntries => (await _analytics.GetTimeEntriesAsync(projectId, ct))
                .Select(entry => TimeRow(entry, names)).ToList(),
            _ => []
        };
    }

    private async Task<List<Dictionary<string, object?>>> UserRowsAsync(
        Guid? projectId, IReadOnlyList<UserSummary> users,
        IReadOnlyDictionary<string, string> names, CancellationToken ct)
    {
        var items = await _analytics.GetWorkItemsAsync(projectId, ct);
        var visibleUsers = users.AsEnumerable();
        if (projectId.HasValue)
        {
            var project = await _projects.GetByIdWithMembersAsync(projectId.Value, ct);
            var allowed = new HashSet<string>();
            if (project is not null)
            {
                allowed.Add(project.OwnerId);
                allowed.UnionWith(project.Members.Select(x => x.UserId));
            }
            allowed.UnionWith(items.SelectMany(x => x.Assignees.Select(a => a.UserId)));
            allowed.UnionWith(items.SelectMany(x => x.TimeEntries.Select(entry => entry.UserId)));
            allowed.UnionWith(items.Select(x => x.ResponsibleId ?? string.Empty)
                .Where(x => !string.IsNullOrWhiteSpace(x)));
            visibleUsers = visibleUsers.Where(x => allowed.Contains(x.Id));
        }
        return visibleUsers.Select(user => new Dictionary<string, object?>
        {
            ["userId"] = user.Id, ["user"] = names.GetValueOrDefault(user.Id, user.Id),
            ["assignedTasks"] = items.Count(x => x.ResponsibleId == user.Id
                || x.Assignees.Any(a => a.UserId == user.Id)),
            ["openTasks"] = items.Count(x => x.CompletedAt == null && (x.ResponsibleId == user.Id
                || x.Assignees.Any(a => a.UserId == user.Id))),
            ["realizedHours"] = Math.Round(items.SelectMany(x => x.TimeEntries)
                .Where(x => x.UserId == user.Id).Sum(DurationHours), 2)
        }).ToList();
    }

    private static Dictionary<string, object?> TaskRow(
        WorkItem item, IReadOnlyDictionary<string, string> names)
    {
        var realized = Math.Round(item.TimeEntries.Sum(DurationHours), 2);
        var blocked = item.OutgoingLinks.Any(x => x.Type == WorkItemLinkType.DependsOn
                && x.TargetWorkItem.CompletedAt == null)
            || item.IncomingLinks.Any(x => x.Type == WorkItemLinkType.Blocks
                && x.SourceWorkItem.CompletedAt == null);
        var row = new Dictionary<string, object?>
        {
            ["number"] = item.Number, ["title"] = item.Title,
            ["project"] = item.Board.Project?.Name, ["projectKey"] = item.Board.Project?.Key,
            ["team"] = item.Team?.Name ?? item.Board.Team?.Name,
            ["responsible"] = item.ResponsibleId is null ? null : names.GetValueOrDefault(item.ResponsibleId, item.ResponsibleId),
            ["status"] = item.CompletedAt.HasValue ? "Concluída" : item.WorkflowStatus?.Name ?? item.Stage?.Name ?? "Backlog",
            ["priority"] = item.Priority.ToString(), ["type"] = item.TaskType?.Name ?? item.Kind.ToString(),
            ["origin"] = item.Origin.ToString(), ["requester"] = item.RequesterName ?? item.RequesterEmail,
            ["sprint"] = item.Sprint?.Name, ["createdAt"] = item.CreatedAt,
            ["dueDate"] = item.DueDate, ["completedAt"] = item.CompletedAt,
            ["plannedHours"] = item.EstimatedHours ?? 0, ["realizedHours"] = realized,
            ["isOpen"] = !item.CompletedAt.HasValue, ["isCompleted"] = item.CompletedAt.HasValue,
            ["isOverdue"] = !item.CompletedAt.HasValue && item.DueDate < DateOnly.FromDateTime(DateTime.UtcNow),
            ["isBlocked"] = blocked
        };
        foreach (var value in item.CustomFieldValues) row[$"custom:{value.FieldDefinitionId}"] = value.Value;
        return row;
    }

    private static Dictionary<string, object?> RequestRow(ExternalRequest request)
    {
        var sla = SlaCalculator.MapRequest(request);
        var row = new Dictionary<string, object?>
        {
            ["protocol"] = request.Protocol, ["title"] = request.WorkItem.Title,
            ["project"] = request.WorkItem.Board.Project?.Name ?? request.ExternalPortal.Project.Name,
            ["category"] = request.Category, ["requester"] = request.WorkItem.RequesterName ?? request.RequesterEmail,
            ["status"] = request.WorkItem.CompletedAt.HasValue ? "Concluída" : request.WorkItem.WorkflowStatus?.Name ?? request.WorkItem.Stage?.Name ?? "Nova",
            ["triageStatus"] = request.TriageStatus.ToString(), ["priority"] = request.WorkItem.Priority.ToString(),
            ["createdAt"] = request.CreatedAt, ["completedAt"] = request.WorkItem.CompletedAt,
            ["firstResponseMinutes"] = MilestoneMinutes(request, true),
            ["resolutionMinutes"] = MilestoneMinutes(request, false),
            ["slaStatus"] = OverallStatus(sla).ToString(),
            ["plannedHours"] = request.WorkItem.EstimatedHours ?? 0,
            ["realizedHours"] = Math.Round(request.WorkItem.TimeEntries.Sum(DurationHours), 2)
        };
        foreach (var value in request.WorkItem.CustomFieldValues)
            row[$"custom:{value.FieldDefinitionId}"] = value.Value;
        return row;
    }

    private static Dictionary<string, object?> SlaRow(ExternalRequest request)
    {
        var row = RequestRow(request);
        var sla = SlaCalculator.MapRequest(request);
        row["firstResponseStatus"] = sla.FirstResponse.Status.ToString();
        row["resolutionStatus"] = sla.Resolution.Status.ToString();
        row["firstResponseDueAt"] = sla.FirstResponse.DueAt;
        row["resolutionDueAt"] = sla.Resolution.DueAt;
        return row;
    }

    private static Dictionary<string, object?> ProjectRow(Project project)
    {
        var items = project.Boards.SelectMany(x => x.WorkItems).ToList();
        return new Dictionary<string, object?>
        {
            ["projectKey"] = project.Key, ["project"] = project.Name,
            ["status"] = project.Status.ToString(),
            ["startDate"] = project.StartDate, ["dueDate"] = project.DueDate,
            ["taskCount"] = items.Count, ["openTasks"] = items.Count(x => x.CompletedAt == null),
            ["completedTasks"] = items.Count(x => x.CompletedAt != null),
            ["sprintCount"] = project.Sprints.Count
        };
    }

    private static Dictionary<string, object?> TeamRow(Team team) => new()
    {
        ["teamId"] = team.Id, ["team"] = team.Name, ["isActive"] = team.IsActive,
        ["memberCount"] = team.Members.Count, ["projectCount"] = team.Projects.Count,
        ["capacityHours"] = team.DefaultWeeklyCapacityHours
    };

    private static Dictionary<string, object?> SprintRow(Sprint sprint)
    {
        var historical = sprint.ItemSnapshots.Count > 0;
        var items = sprint.WorkItems;
        var plannedPoints = historical
            ? sprint.ItemSnapshots.Sum(x => x.Points ?? 0) : items.Sum(x => x.Points ?? 0);
        var completedPoints = historical
            ? sprint.ItemSnapshots.Where(x => x.WasCompleted).Sum(x => x.Points ?? 0)
            : items.Where(x => x.CompletedAt.HasValue).Sum(x => x.Points ?? 0);
        var plannedHours = historical
            ? sprint.ItemSnapshots.Sum(x => x.EstimatedHours ?? 0)
            : items.Sum(x => x.EstimatedHours ?? 0);
        return new Dictionary<string, object?>
        {
            ["sprint"] = sprint.Name, ["project"] = sprint.Project.Name, ["team"] = sprint.Team.Name,
            ["status"] = sprint.Status.ToString(), ["startDate"] = sprint.StartDate,
            ["endDate"] = sprint.EndDate, ["plannedPoints"] = plannedPoints,
            ["completedPoints"] = completedPoints, ["velocity"] = completedPoints,
            ["plannedHours"] = plannedHours,
            ["progress"] = plannedPoints == 0 ? 0 : Math.Round(completedPoints * 100m / plannedPoints, 2)
        };
    }

    private static Dictionary<string, object?> TimeRow(
        TimeEntry entry, IReadOnlyDictionary<string, string> names) => new()
    {
        ["project"] = entry.WorkItem.Board.Project?.Name, ["team"] = entry.WorkItem.Team?.Name ?? entry.WorkItem.Board.Team?.Name,
        ["user"] = names.GetValueOrDefault(entry.UserId, entry.UserId), ["userId"] = entry.UserId,
        ["task"] = entry.WorkItem.Title, ["taskNumber"] = entry.WorkItem.Number,
        ["startedAt"] = entry.StartedAt, ["endedAt"] = entry.EndedAt,
        ["hours"] = Math.Round(DurationHours(entry), 2), ["realizedHours"] = Math.Round(DurationHours(entry), 2),
        ["plannedHours"] = entry.WorkItem.EstimatedHours ?? 0, ["note"] = entry.Note
    };

    private static List<Dictionary<string, object?>> ApplyPeriod(
        ReportDataSource source, List<Dictionary<string, object?>> rows,
        DateOnly? from, DateOnly? to)
    {
        if (!from.HasValue && !to.HasValue) return rows;
        var field = ReportCatalog.DateField(source);
        if (string.IsNullOrWhiteSpace(field)) return rows;
        return rows.Where(row =>
        {
            if (!TryDate(row.GetValueOrDefault(field), out var date)) return false;
            return (!from.HasValue || date >= from.Value) && (!to.HasValue || date <= to.Value);
        }).ToList();
    }

    private static bool Matches(object? candidate, ReportFilterDto filter)
    {
        var left = Display(candidate);
        var right = filter.Value?.Trim() ?? string.Empty;
        return filter.Operator switch
        {
            "eq" => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            "neq" => !string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            "contains" => left.Contains(right, StringComparison.OrdinalIgnoreCase),
            "startsWith" => left.StartsWith(right, StringComparison.OrdinalIgnoreCase),
            "isEmpty" => string.IsNullOrWhiteSpace(left),
            "isNotEmpty" => !string.IsNullOrWhiteSpace(left),
            "gt" => Compare(candidate, right) > 0,
            "gte" => Compare(candidate, right) >= 0,
            "lt" => Compare(candidate, right) < 0,
            "lte" => Compare(candidate, right) <= 0,
            "between" => Compare(candidate, right) >= 0 && Compare(candidate, filter.ValueTo ?? string.Empty) <= 0,
            "in" => right.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Contains(left, StringComparer.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static object? Aggregate(List<Dictionary<string, object?>> rows, ReportMetricDto metric)
    {
        if (metric.Operation == ReportMetricOperation.Count) return rows.Count;
        var field = metric.Field ?? string.Empty;
        if (metric.Operation == ReportMetricOperation.Percentage)
            return rows.Count == 0 ? 0 : Math.Round(rows.Count(row => Truthy(row.GetValueOrDefault(field))) * 100m / rows.Count, 2);
        var values = rows.Select(row => Number(row.GetValueOrDefault(field))).Where(x => x.HasValue).Select(x => x!.Value).ToList();
        if (values.Count == 0) return 0m;
        return metric.Operation switch
        {
            ReportMetricOperation.Sum => Math.Round(values.Sum(), 2),
            ReportMetricOperation.Average => Math.Round(values.Average(), 2),
            ReportMetricOperation.Minimum => values.Min(),
            ReportMetricOperation.Maximum => values.Max(),
            _ => 0m
        };
    }

    private static decimal Sum(List<Dictionary<string, object?>> rows, string field)
        => Math.Round(rows.Sum(row => Number(row.GetValueOrDefault(field)) ?? 0), 2);
    private static string MetricKey(ReportMetricDto metric)
        => $"metric:{metric.Operation}:{metric.Field ?? "all"}:{Math.Abs(metric.Label.GetHashCode())}";
    private static string MetricLabel(string key, IReadOnlyList<ReportMetricDto> metrics)
    {
        if (key == "plannedHours") return "Horas previstas";
        if (key == "realizedHours") return "Horas realizadas";
        if (key == "varianceHours") return "Desvio (h)";
        return metrics.FirstOrDefault(metric => MetricKey(metric) == key)?.Label ?? key;
    }
    private static string Display(object? value) => value switch
    {
        null => string.Empty, DateOnly date => date.ToString("yyyy-MM-dd"),
        DateTimeOffset date => date.ToString("O"), bool flag => flag ? "true" : "false",
        _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
    };
    private static string SortValue(object? value)
        => value is IFormattable formatted ? formatted.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty : Display(value);
    private static int Compare(object? candidate, string other)
    {
        if (Number(candidate) is { } number && decimal.TryParse(other, NumberStyles.Number, CultureInfo.InvariantCulture, out var otherNumber))
            return number.CompareTo(otherNumber);
        if (TryDate(candidate, out var date) && DateOnly.TryParse(other, out var otherDate))
            return date.CompareTo(otherDate);
        return string.Compare(Display(candidate), other, StringComparison.OrdinalIgnoreCase);
    }
    private static decimal? Number(object? value)
    {
        if (value is null) return null;
        try { return Convert.ToDecimal(value, CultureInfo.InvariantCulture); }
        catch (Exception) when (value is not null) { return null; }
    }
    private static bool Truthy(object? value) => value switch
    {
        true => true, false or null => false,
        _ => bool.TryParse(Display(value), out var flag) ? flag : !string.IsNullOrWhiteSpace(Display(value))
    };
    private static bool TryDate(object? value, out DateOnly date)
    {
        switch (value)
        {
            case DateOnly direct: date = direct; return true;
            case DateTimeOffset offset: date = DateOnly.FromDateTime(offset.UtcDateTime); return true;
            case DateTime time: date = DateOnly.FromDateTime(time); return true;
            default: return DateOnly.TryParse(Display(value), out date);
        }
    }
    private static decimal DurationHours(TimeEntry entry)
        => (decimal)Math.Max(0, ((entry.EndedAt ?? DateTimeOffset.UtcNow) - entry.StartedAt).TotalHours);
    private static decimal? MilestoneMinutes(ExternalRequest request, bool firstResponse)
    {
        var end = firstResponse ? request.FirstRespondedAt : request.WorkItem.CompletedAt;
        if (!end.HasValue) return null;
        var snapshot = SlaCalculator.Snapshot(request);
        var minutes = snapshot is null
            ? Math.Round((decimal)(end.Value - request.CreatedAt).TotalMinutes, 2)
            : SlaCalculator.BusinessMinutesBetween(request.CreatedAt, end.Value, snapshot);
        return firstResponse ? minutes : Math.Max(0, minutes - request.SlaPausedBusinessMinutes);
    }
    private static SlaStatus OverallStatus(ExternalRequestSlaDto sla)
    {
        var values = new[] { sla.FirstResponse.Status, sla.Resolution.Status };
        if (values.Contains(SlaStatus.Overdue)) return SlaStatus.Overdue;
        if (values.Contains(SlaStatus.Paused)) return SlaStatus.Paused;
        if (values.Contains(SlaStatus.NearDue)) return SlaStatus.NearDue;
        if (values.All(x => x == SlaStatus.Met)) return SlaStatus.Met;
        if (values.All(x => x == SlaStatus.NotApplicable)) return SlaStatus.NotApplicable;
        return SlaStatus.WithinDeadline;
    }
}

internal static class ReportDefinitionValidator
{
    public static async Task ValidateAsync(
        Guid? projectId, ReportDataSource source, ReportVisualization visualization,
        string name, ReportDefinitionDto definition, IProjectRepository projects, CancellationToken ct)
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(name) && name.Trim().Length <= 200,
            "Informe um nome de até 200 caracteres.");
        DomainException.Garantir(Enum.IsDefined(source) && Enum.IsDefined(visualization),
            "Fonte ou visualização inválida.");
        DomainException.Garantir(definition.Columns.Count <= 30 && definition.Metrics.Count <= 10
            && definition.Filters.Count <= 20, "A definição excede os limites do construtor.");
        DomainException.Garantir(definition.Columns.Count > 0 || definition.Metrics.Count > 0,
            "Selecione ao menos uma coluna ou métrica.");
        DomainException.Garantir(!definition.From.HasValue || !definition.To.HasValue
            || definition.To >= definition.From, "O período do relatório é inválido.");
        var allowed = ReportCatalog.Fields(source).Select(x => x.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var numeric = ReportCatalog.Fields(source).Where(x => x.Numeric).Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (projectId.HasValue)
        {
            var project = await projects.GetByIdAsync(projectId.Value, ct)
                ?? throw new NaoEncontradoException("Projeto");
            foreach (var field in project.CustomFields.Where(x => x.IsActive))
            {
                allowed.Add($"custom:{field.Id}");
                if (field.Type is CustomFieldType.Number or CustomFieldType.Percentage)
                    numeric.Add($"custom:{field.Id}");
            }
        }
        var referenced = definition.Columns
            .Concat(definition.Filters.Select(x => x.Field))
            .Concat(definition.Metrics.Where(x => !string.IsNullOrWhiteSpace(x.Field)).Select(x => x.Field!))
            .Concat(string.IsNullOrWhiteSpace(definition.GroupBy) ? [] : [definition.GroupBy!])
            .Concat(string.IsNullOrWhiteSpace(definition.OrderBy) ? [] : [definition.OrderBy!]);
        DomainException.Garantir(referenced.All(allowed.Contains),
            "A definição contém uma coluna não permitida para a fonte selecionada.");
        DomainException.Garantir(definition.Filters.All(x => ReportCatalog.FilterOperators.Contains(x.Operator)),
            "A definição contém um operador de filtro inválido.");
        DomainException.Garantir(definition.Metrics.All(x => Enum.IsDefined(x.Operation)
            && !string.IsNullOrWhiteSpace(x.Label) && x.Label.Length <= 120),
            "A definição contém uma métrica inválida.");
        DomainException.Garantir(definition.Metrics.All(metric =>
            metric.Operation is ReportMetricOperation.Count or ReportMetricOperation.PlannedVersusActual
            || metric.Operation == ReportMetricOperation.Percentage
            || (!string.IsNullOrWhiteSpace(metric.Field) && numeric.Contains(metric.Field))),
            "Soma, média, mínimo e máximo exigem uma coluna numérica.");
    }
}

internal static class ReportDefinitionMapper
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public static SavedReportDto Map(SavedReport report) => new(
        report.Id, report.ProjectId, report.OwnerId, report.Name, report.Source,
        report.Visualization, Definition(report), report.IsShared,
        report.CreatedAt, report.UpdatedAt);
    public static ReportDefinitionDto Definition(SavedReport report)
    {
        try { return JsonSerializer.Deserialize<ReportDefinitionDto>(report.DefinitionJson, JsonOptions) ?? Empty; }
        catch (JsonException) { return Empty; }
    }
    private static ReportDefinitionDto Empty => new([], [], [], null, null, false, null, null);
}

internal static class ReportAuthorization
{
    public static async Task EnsureViewAsync(
        string actorId, Guid? projectId, IPermissionService permissions,
        IProjectAccessService access, CancellationToken ct)
    {
        if (projectId.HasValue)
            await access.EnsureAtLeastAsync(projectId.Value, actorId, ProjectRole.Viewer, ct);
        await permissions.EnsureAsync(actorId, PlatformPermission.ViewReport,
            PermissionScope.Report, projectId, ct);
    }
    public static async Task EnsureCreateAsync(
        string actorId, Guid? projectId, IPermissionService permissions,
        IProjectAccessService access, CancellationToken ct)
    {
        if (projectId.HasValue)
            await access.EnsureAtLeastAsync(projectId.Value, actorId, ProjectRole.ProductOwner, ct);
        await permissions.EnsureAsync(actorId, PlatformPermission.CreateReport,
            PermissionScope.Report, projectId, ct);
    }
}

internal static class ReportCatalog
{
    public static readonly string[] FilterOperators =
        ["eq", "neq", "contains", "startsWith", "gt", "gte", "lt", "lte", "between", "in", "isEmpty", "isNotEmpty"];

    public static ReportCatalogDto Create(IReadOnlyList<ReportFieldDto>? customFields = null) => new(
        Enum.GetValues<ReportDataSource>().Select(source => new ReportSourceCatalogDto(
            source, SourceName(source), Fields(source, customFields))).ToList(), FilterOperators,
        Enum.GetValues<ReportMetricOperation>(), Enum.GetValues<ReportVisualization>());

    private static IReadOnlyList<ReportFieldDto> Fields(
        ReportDataSource source, IReadOnlyList<ReportFieldDto>? customFields)
    {
        var fields = Fields(source);
        return customFields is { Count: > 0 }
            && source is ReportDataSource.WorkItems or ReportDataSource.ExternalRequests
                ? fields.Concat(customFields).ToList()
                : fields;
    }

    public static IReadOnlyList<ReportFieldDto> Fields(ReportDataSource source) => source switch
    {
        ReportDataSource.WorkItems =>
        [F("number","Número","number",true),F("title","Título"),F("project","Projeto"),F("projectKey","Chave do projeto"),F("team","Equipe"),F("responsible","Responsável"),F("status","Status"),F("priority","Prioridade"),F("type","Tipo"),F("origin","Origem"),F("requester","Solicitante"),F("sprint","Sprint"),F("createdAt","Criação","date"),F("dueDate","Prazo","date"),F("completedAt","Conclusão","date"),F("plannedHours","Horas previstas","number",true),F("realizedHours","Horas realizadas","number",true),F("isOpen","Aberta","boolean"),F("isCompleted","Concluída","boolean"),F("isOverdue","Atrasada","boolean"),F("isBlocked","Bloqueada","boolean")],
        ReportDataSource.ExternalRequests =>
        [F("protocol","Protocolo"),F("title","Título"),F("project","Projeto"),F("category","Categoria"),F("requester","Solicitante"),F("status","Status"),F("triageStatus","Triagem"),F("priority","Prioridade"),F("createdAt","Criação","date"),F("completedAt","Conclusão","date"),F("firstResponseMinutes","Tempo de 1ª resposta (min)","number",true),F("resolutionMinutes","Tempo de resolução (min)","number",true),F("slaStatus","SLA"),F("plannedHours","Horas previstas","number",true),F("realizedHours","Horas realizadas","number",true)],
        ReportDataSource.Projects =>
        [F("projectKey","Chave"),F("project","Projeto"),F("status","Status"),F("startDate","Início","date"),F("dueDate","Prazo","date"),F("taskCount","Tarefas","number",true),F("openTasks","Abertas","number",true),F("completedTasks","Concluídas","number",true),F("sprintCount","Sprints","number",true)],
        ReportDataSource.Teams =>
        [F("teamId","ID da equipe"),F("team","Equipe"),F("isActive","Ativa","boolean"),F("memberCount","Membros","number",true),F("projectCount","Projetos","number",true),F("capacityHours","Capacidade diária","number",true)],
        ReportDataSource.Users =>
        [F("userId","ID do usuário"),F("user","Usuário"),F("assignedTasks","Tarefas atribuídas","number",true),F("openTasks","Tarefas abertas","number",true),F("realizedHours","Horas realizadas","number",true)],
        ReportDataSource.Sprints =>
        [F("sprint","Sprint"),F("project","Projeto"),F("team","Equipe"),F("status","Status"),F("startDate","Início","date"),F("endDate","Fim","date"),F("plannedPoints","Pontos planejados","number",true),F("completedPoints","Pontos concluídos","number",true),F("velocity","Velocidade","number",true),F("plannedHours","Horas previstas","number",true),F("progress","Progresso (%)","number",true)],
        ReportDataSource.TimeEntries =>
        [F("project","Projeto"),F("team","Equipe"),F("user","Usuário"),F("userId","ID do usuário"),F("task","Tarefa"),F("taskNumber","Número","number",true),F("startedAt","Início","date"),F("endedAt","Fim","date"),F("hours","Horas","number",true),F("plannedHours","Horas previstas","number",true),F("realizedHours","Horas realizadas","number",true),F("note","Observação")],
        ReportDataSource.Slas =>
        [F("protocol","Protocolo"),F("title","Título"),F("project","Projeto"),F("category","Categoria"),F("requester","Solicitante"),F("priority","Prioridade"),F("createdAt","Criação","date"),F("slaStatus","SLA"),F("firstResponseStatus","SLA 1ª resposta"),F("resolutionStatus","SLA resolução"),F("firstResponseDueAt","Vencimento 1ª resposta","date"),F("resolutionDueAt","Vencimento resolução","date"),F("firstResponseMinutes","Tempo de 1ª resposta (min)","number",true),F("resolutionMinutes","Tempo de resolução (min)","number",true)],
        _ => []
    };

    public static string DateField(ReportDataSource source) => source switch
    {
        ReportDataSource.WorkItems or ReportDataSource.ExternalRequests or ReportDataSource.Slas => "createdAt",
        ReportDataSource.Projects => "startDate",
        ReportDataSource.Teams or ReportDataSource.Users => string.Empty,
        ReportDataSource.Sprints => "startDate",
        ReportDataSource.TimeEntries => "startedAt",
        _ => "createdAt"
    };
    private static ReportFieldDto F(string key,string label,string type="text",bool numeric=false)
        => new(key,label,type,numeric);
    private static string SourceName(ReportDataSource source) => source switch
    {
        ReportDataSource.WorkItems => "Tarefas", ReportDataSource.ExternalRequests => "Solicitações",
        ReportDataSource.Projects => "Projetos", ReportDataSource.Teams => "Equipes",
        ReportDataSource.Users => "Usuários", ReportDataSource.Sprints => "Sprints",
        ReportDataSource.TimeEntries => "Apontamentos", ReportDataSource.Slas => "SLAs", _ => source.ToString()
    };
}
