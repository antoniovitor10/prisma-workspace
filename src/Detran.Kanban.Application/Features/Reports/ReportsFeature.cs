using Detran.Kanban.Application.Common.Exceptions;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;
using MediatR;

namespace Detran.Kanban.Application.Features.Reports;

public record TimeReportSummaryDto(
    string Key,
    string Name,
    decimal PlannedHours,
    decimal RealizedHours,
    decimal VarianceHours,
    decimal AutomaticHours,
    decimal ManualHours,
    decimal BusinessHours);

public record TimeReportEntryDto(
    Guid Id,
    Guid WorkItemId,
    long WorkItemNumber,
    string Reference,
    string WorkItemTitle,
    string UserId,
    string UserName,
    Guid? TeamId,
    string TeamName,
    DateTimeOffset StartedAt,
    DateTimeOffset? EndedAt,
    int DurationSeconds,
    string? Note,
    bool IsManual);

public record TimeReportDto(
    Guid ProjectId,
    string ProjectKey,
    string ProjectName,
    DateOnly From,
    DateOnly To,
    TimeReportSummaryDto Project,
    IReadOnlyList<TimeReportSummaryDto> ByUser,
    IReadOnlyList<TimeReportSummaryDto> ByTeam,
    IReadOnlyList<TimeReportEntryDto> Entries);

public record GetTimeReportQuery(
    Guid ProjectId,
    DateOnly? From,
    DateOnly? To,
    Guid? TeamId,
    string? UserId,
    string ActorId) : IRequest<TimeReportDto>;

public class GetTimeReportQueryHandler : IRequestHandler<GetTimeReportQuery, TimeReportDto>
{
    private readonly IReportRepository _reports;
    private readonly IProjectRepository _projects;
    private readonly IProjectAccessService _access;
    private readonly IUserDirectory _users;

    public GetTimeReportQueryHandler(
        IReportRepository reports,
        IProjectRepository projects,
        IProjectAccessService access,
        IUserDirectory users)
        => (_reports, _projects, _access, _users) = (reports, projects, access, users);

    public async Task<TimeReportDto> Handle(GetTimeReportQuery request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.Viewer, ct);
        var project = await _projects.GetByIdWithMembersAsync(request.ProjectId, ct)
            ?? throw new NaoEncontradoException("Projeto");
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = request.From ?? today.AddDays(-29);
        var to = request.To ?? today;
        DomainException.Garantir(to >= from, "A data final deve ser igual ou posterior à inicial.");
        DomainException.Garantir(to.DayNumber - from.DayNumber <= 730,
            "O relatório aceita um período de até 730 dias.");
        DomainException.Garantir(!request.TeamId.HasValue
            || project.Teams.Any(x => x.TeamId == request.TeamId),
            "A equipe não pertence ao projeto.");
        DomainException.Garantir(string.IsNullOrWhiteSpace(request.UserId)
            || request.UserId == project.OwnerId
            || project.Members.Any(x => x.UserId == request.UserId),
            "O usuário não pertence ao projeto.");

        var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toExclusive = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var items = await _reports.GetTimeReportItemsAsync(
            request.ProjectId, fromUtc, toExclusive, request.TeamId,
            string.IsNullOrWhiteSpace(request.UserId) ? null : request.UserId, ct);
        var userIds = items.SelectMany(x => x.TimeEntries.Select(entry => entry.UserId))
            .Concat(items.Select(x => x.ResponsibleId ?? string.Empty))
            .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
        var names = await _users.GetDisplayNamesAsync(userIds, ct);

        var plannedItems = items.Where(item => IsPlannedInRange(item, from, to)).ToList();
        var entries = items.SelectMany(item => item.TimeEntries
                .Where(entry => string.IsNullOrWhiteSpace(request.UserId) || entry.UserId == request.UserId)
                .Select(entry => MapEntry(item, entry, project.Key, names, fromUtc, toExclusive)))
            .OrderByDescending(x => x.StartedAt).ToList();

        var businessHours = BusinessDays(from, to) * 8m;
        decimal SumHours(IEnumerable<TimeReportEntryDto> source) =>
            SecondsToHours(source.Sum(entry => entry.DurationSeconds));

        var plannedByUser = plannedItems
            .Where(x => !string.IsNullOrWhiteSpace(x.ResponsibleId))
            .GroupBy(x => x.ResponsibleId!)
            .ToDictionary(x => x.Key, x => x.Sum(item => item.EstimatedHours ?? 0));
        var actualByUser = entries.GroupBy(x => x.UserId)
            .ToDictionary(x => x.Key, x => SumHours(x));
        var autoByUser = entries.Where(e => !e.IsManual).GroupBy(x => x.UserId)
            .ToDictionary(x => x.Key, x => SumHours(x));
        var manualByUser = entries.Where(e => e.IsManual).GroupBy(x => x.UserId)
            .ToDictionary(x => x.Key, x => SumHours(x));
        var byUser = plannedByUser.Keys.Union(actualByUser.Keys)
            .Select(id => Summary(id, names.GetValueOrDefault(id, id),
                plannedByUser.GetValueOrDefault(id), actualByUser.GetValueOrDefault(id),
                autoByUser.GetValueOrDefault(id), manualByUser.GetValueOrDefault(id), businessHours))
            .OrderByDescending(x => x.RealizedHours).ThenBy(x => x.Name).ToList();

        var plannedByTeam = plannedItems.GroupBy(TeamKey)
            .ToDictionary(x => x.Key, x => x.Sum(item => item.EstimatedHours ?? 0));
        var actualByTeam = entries.GroupBy(x => x.TeamId?.ToString() ?? "unassigned")
            .ToDictionary(x => x.Key, x => SumHours(x));
        var autoByTeam = entries.Where(e => !e.IsManual).GroupBy(x => x.TeamId?.ToString() ?? "unassigned")
            .ToDictionary(x => x.Key, x => SumHours(x));
        var manualByTeam = entries.Where(e => e.IsManual).GroupBy(x => x.TeamId?.ToString() ?? "unassigned")
            .ToDictionary(x => x.Key, x => SumHours(x));
        var teamNames = items.GroupBy(TeamKey).ToDictionary(
            x => x.Key,
            x => x.Select(TeamName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name))
                ?? "Sem equipe");
        var byTeam = plannedByTeam.Keys.Union(actualByTeam.Keys)
            .Select(id => Summary(id, teamNames.GetValueOrDefault(id, "Sem equipe"),
                plannedByTeam.GetValueOrDefault(id), actualByTeam.GetValueOrDefault(id),
                autoByTeam.GetValueOrDefault(id), manualByTeam.GetValueOrDefault(id), businessHours))
            .OrderByDescending(x => x.RealizedHours).ThenBy(x => x.Name).ToList();

        var planned = plannedItems.Sum(x => x.EstimatedHours ?? 0);
        var actual = SumHours(entries);
        var auto = SumHours(entries.Where(e => !e.IsManual));
        var manual = SumHours(entries.Where(e => e.IsManual));
        return new TimeReportDto(
            project.Id, project.Key, project.Name, from, to,
            Summary(project.Key, project.Name, planned, actual, auto, manual, businessHours),
            byUser, byTeam, entries);
    }

    private static int BusinessDays(DateOnly from, DateOnly to)
    {
        var days = 0;
        for (var day = from; day <= to; day = day.AddDays(1))
            if (day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday)
                days++;
        return days;
    }

    private static TimeReportEntryDto MapEntry(
        WorkItem item,
        TimeEntry entry,
        string projectKey,
        IReadOnlyDictionary<string, string> names,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        var startedAt = entry.StartedAt < from ? from : entry.StartedAt;
        var naturalEnd = entry.EndedAt ?? DateTimeOffset.UtcNow;
        var endedAt = naturalEnd > to ? to : naturalEnd;
        var seconds = Math.Max(0, (int)(endedAt - startedAt).TotalSeconds);
        return new TimeReportEntryDto(
            entry.Id, item.Id, item.Number, $"{projectKey}-{item.Number}", item.Title,
            entry.UserId, names.GetValueOrDefault(entry.UserId, entry.UserId),
            item.TeamId ?? item.Board.TeamId, TeamName(item), entry.StartedAt,
            entry.EndedAt, seconds, entry.Note, entry.IsManual);
    }

    private static bool IsPlannedInRange(WorkItem item, DateOnly from, DateOnly to)
    {
        var start = item.StartDate ?? DateOnly.FromDateTime(item.CreatedAt.UtcDateTime);
        var end = item.DueDate ?? start;
        return start <= to && end >= from;
    }

    private static string TeamKey(WorkItem item)
        => (item.TeamId ?? item.Board.TeamId)?.ToString() ?? "unassigned";
    private static string TeamName(WorkItem item)
        => item.Team?.Name ?? item.Board.Team?.Name ?? "Sem equipe";
    private static decimal SecondsToHours(int seconds)
        => Math.Round(seconds / 3600m, 2);
    private static TimeReportSummaryDto Summary(
        string key, string name, decimal planned, decimal actual,
        decimal automatic, decimal manual, decimal business)
        => new(key, name, Math.Round(planned, 2), Math.Round(actual, 2),
            Math.Round(actual - planned, 2), Math.Round(automatic, 2),
            Math.Round(manual, 2), Math.Round(business, 2));
}

public record CustomFieldReportDefinitionDto(
    Guid Id,
    string Name,
    CustomFieldType Type,
    bool IsRequired,
    string? OptionsJson);

public record CustomFieldReportRowDto(
    Guid WorkItemId,
    long WorkItemNumber,
    string Reference,
    string Title,
    string Status,
    IReadOnlyDictionary<Guid, string?> Values);

public record CustomFieldReportDto(
    Guid ProjectId,
    string ProjectKey,
    IReadOnlyList<CustomFieldReportDefinitionDto> Fields,
    IReadOnlyList<CustomFieldReportRowDto> Rows);

public record GetCustomFieldReportQuery(
    Guid ProjectId,
    Guid? FieldId,
    string? Value,
    string ActorId) : IRequest<CustomFieldReportDto>;

public class GetCustomFieldReportQueryHandler
    : IRequestHandler<GetCustomFieldReportQuery, CustomFieldReportDto>
{
    private readonly IReportRepository _reports;
    private readonly IProjectRepository _projects;
    private readonly IProjectAccessService _access;

    public GetCustomFieldReportQueryHandler(
        IReportRepository reports,
        IProjectRepository projects,
        IProjectAccessService access)
        => (_reports, _projects, _access) = (reports, projects, access);

    public async Task<CustomFieldReportDto> Handle(
        GetCustomFieldReportQuery request,
        CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.Viewer, ct);
        var project = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NaoEncontradoException("Projeto");
        var fields = project.CustomFields.Where(x => x.IsActive)
            .OrderBy(x => x.Position).Select(x => new CustomFieldReportDefinitionDto(
                x.Id, x.Name, x.Type, x.IsRequired, x.OptionsJson)).ToList();
        DomainException.Garantir(!request.FieldId.HasValue
            || fields.Any(x => x.Id == request.FieldId),
            "O campo personalizado não pertence ao projeto.");
        var items = await _reports.GetCustomFieldReportItemsAsync(request.ProjectId, ct);
        var valueFilter = request.Value?.Trim();
        var rows = items.Select(item => new CustomFieldReportRowDto(
                item.Id, item.Number, $"{project.Key}-{item.Number}", item.Title,
                item.CompletedAt.HasValue ? "Concluída"
                    : item.WorkflowStatus?.Name ?? item.Stage?.Name ?? "Backlog",
                item.CustomFieldValues.ToDictionary(x => x.FieldDefinitionId, x => x.Value)))
            .Where(row => !request.FieldId.HasValue
                || (row.Values.TryGetValue(request.FieldId.Value, out var value)
                    && (string.IsNullOrWhiteSpace(valueFilter)
                        || (value?.Contains(valueFilter, StringComparison.OrdinalIgnoreCase) ?? false))))
            .ToList();
        return new CustomFieldReportDto(project.Id, project.Key, fields, rows);
    }
}
