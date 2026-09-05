using System.Text.Json;
using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Prisma.Workspace.Application.Features.Sla;

public record SlaRuleDto(
    string? Category,
    Priority? Priority,
    int? FirstResponseMinutes,
    int? ResolutionMinutes,
    int Position);

public record ProjectSlaPolicyDto(
    Guid ProjectId,
    bool IsEnabled,
    int FirstResponseMinutes,
    int ResolutionMinutes,
    string ServiceStart,
    string ServiceEnd,
    int BusinessDaysMask,
    string TimeZoneId,
    bool PauseWhileWaitingRequester,
    bool AlertsEnabled,
    int NearDueMinutes,
    IReadOnlyList<string> Holidays,
    IReadOnlyList<SlaRuleDto> Rules,
    DateTimeOffset? UpdatedAt);

public record SlaMilestoneDto(
    SlaStatus Status,
    DateTimeOffset? DueAt,
    DateTimeOffset? MetAt);

public record ExternalRequestSlaDto(
    SlaMilestoneDto FirstResponse,
    SlaMilestoneDto Resolution,
    bool IsPaused,
    DateTimeOffset? PausedAt,
    int PausedBusinessMinutes,
    bool AlertsEnabled,
    int NearDueMinutes);

public record SlaPolicySnapshotDto(
    int FirstResponseMinutes,
    int ResolutionMinutes,
    string ServiceStart,
    string ServiceEnd,
    int BusinessDaysMask,
    string TimeZoneId,
    bool PauseWhileWaitingRequester,
    bool AlertsEnabled,
    int NearDueMinutes,
    IReadOnlyList<string> Holidays,
    string? MatchedCategory,
    Priority? MatchedPriority);

public record GetProjectSlaPolicyQuery(Guid ProjectId, string ActorId)
    : IRequest<ProjectSlaPolicyDto>;

public class GetProjectSlaPolicyQueryHandler
    : IRequestHandler<GetProjectSlaPolicyQuery, ProjectSlaPolicyDto>
{
    private readonly ISlaRepository _sla;
    private readonly IProjectRepository _projects;
    private readonly IProjectAccessService _access;

    public GetProjectSlaPolicyQueryHandler(
        ISlaRepository sla,
        IProjectRepository projects,
        IProjectAccessService access)
        => (_sla, _projects, _access) = (sla, projects, access);

    public async Task<ProjectSlaPolicyDto> Handle(
        GetProjectSlaPolicyQuery request,
        CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.Viewer, ct);
        _ = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NaoEncontradoException("Projeto");
        var policy = await _sla.GetByProjectAsync(request.ProjectId, ct);
        return SlaCalculator.MapPolicy(request.ProjectId, policy);
    }
}

public record UpsertProjectSlaPolicyCommand(
    Guid ProjectId,
    bool IsEnabled,
    int FirstResponseMinutes,
    int ResolutionMinutes,
    TimeOnly ServiceStart,
    TimeOnly ServiceEnd,
    int BusinessDaysMask,
    string TimeZoneId,
    bool PauseWhileWaitingRequester,
    bool AlertsEnabled,
    int NearDueMinutes,
    IReadOnlyList<string> Holidays,
    IReadOnlyList<SlaRuleDto> Rules,
    string ActorId) : IRequest<ProjectSlaPolicyDto>;

public class UpsertProjectSlaPolicyCommandValidator
    : AbstractValidator<UpsertProjectSlaPolicyCommand>
{
    public UpsertProjectSlaPolicyCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.TimeZoneId).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Holidays).NotNull().Must(x => x.Count <= 400)
            .WithMessage("O calendário aceita no máximo 400 feriados.");
        RuleFor(x => x.Rules).NotNull().Must(x => x.Count <= 100)
            .WithMessage("O SLA aceita no máximo 100 regras.");
    }
}

public class UpsertProjectSlaPolicyCommandHandler
    : IRequestHandler<UpsertProjectSlaPolicyCommand, ProjectSlaPolicyDto>
{
    private readonly ISlaRepository _sla;
    private readonly IProjectRepository _projects;
    private readonly IProjectAccessService _access;

    public UpsertProjectSlaPolicyCommandHandler(
        ISlaRepository sla,
        IProjectRepository projects,
        IProjectAccessService access)
        => (_sla, _projects, _access) = (sla, projects, access);

    public async Task<ProjectSlaPolicyDto> Handle(
        UpsertProjectSlaPolicyCommand request,
        CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(
            request.ProjectId, request.ActorId, ProjectRole.ProjectAdmin, ct);
        _ = await _projects.GetByIdAsync(request.ProjectId, ct)
            ?? throw new NaoEncontradoException("Projeto");

        var timeZoneId = SlaCalculator.ValidateTimeZone(request.TimeZoneId);
        var holidays = SlaCalculator.NormalizeHolidays(request.Holidays);
        var rules = SlaCalculator.NormalizeRules(request.Rules);
        var policy = await _sla.GetByProjectAsync(request.ProjectId, ct);
        if (policy is null)
        {
            policy = ProjectSlaPolicy.Create(request.ProjectId);
            _sla.Add(policy);
        }

        policy.Configure(
            request.IsEnabled,
            request.FirstResponseMinutes,
            request.ResolutionMinutes,
            request.ServiceStart,
            request.ServiceEnd,
            request.BusinessDaysMask,
            timeZoneId,
            request.PauseWhileWaitingRequester,
            request.AlertsEnabled,
            request.NearDueMinutes,
            JsonSerializer.Serialize(holidays, SlaCalculator.JsonOptions),
            JsonSerializer.Serialize(rules, SlaCalculator.JsonOptions));
        await _sla.SaveAsync(ct);

        _projects.AddEvent(ProjectEvent.Register(
            request.ProjectId,
            request.ActorId,
            "sla_policy_saved",
            JsonSerializer.Serialize(new
            {
                request.IsEnabled,
                request.FirstResponseMinutes,
                request.ResolutionMinutes,
                request.BusinessDaysMask,
                Rules = rules.Count
            }, SlaCalculator.JsonOptions)));
        await _projects.SaveAsync(ct);
        return SlaCalculator.MapPolicy(request.ProjectId, policy);
    }
}

public static class SlaCalculator
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static ProjectSlaPolicyDto MapPolicy(Guid projectId, ProjectSlaPolicy? policy)
    {
        if (policy is null)
            return new ProjectSlaPolicyDto(
                projectId, false, 240, 1440, "08:00", "18:00", 62,
                "E. South America Standard Time", true, true, 60, [], [], null);
        return new ProjectSlaPolicyDto(
            policy.ProjectId, policy.IsEnabled, policy.FirstResponseMinutes,
            policy.ResolutionMinutes, policy.ServiceStart.ToString("HH:mm"),
            policy.ServiceEnd.ToString("HH:mm"), policy.BusinessDaysMask,
            policy.TimeZoneId, policy.PauseWhileWaitingRequester, policy.AlertsEnabled,
            policy.NearDueMinutes, Deserialize<string>(policy.HolidaysJson),
            Deserialize<SlaRuleDto>(policy.RulesJson).OrderBy(x => x.Position).ToList(),
            policy.UpdatedAt);
    }

    public static void ApplyPolicy(
        ExternalRequest request,
        ProjectSlaPolicy? policy,
        string? category,
        Priority priority,
        DateTimeOffset createdAt)
    {
        if (policy?.IsEnabled != true) return;
        var rules = Deserialize<SlaRuleDto>(policy.RulesJson).OrderBy(x => x.Position);
        var rule = rules.FirstOrDefault(x => RuleMatches(x, category, priority));
        var firstResponseMinutes = rule?.FirstResponseMinutes ?? policy.FirstResponseMinutes;
        var resolutionMinutes = rule?.ResolutionMinutes ?? policy.ResolutionMinutes;
        var snapshot = new SlaPolicySnapshotDto(
            firstResponseMinutes, resolutionMinutes,
            policy.ServiceStart.ToString("HH:mm"), policy.ServiceEnd.ToString("HH:mm"),
            policy.BusinessDaysMask, policy.TimeZoneId, policy.PauseWhileWaitingRequester,
            policy.AlertsEnabled, policy.NearDueMinutes,
            Deserialize<string>(policy.HolidaysJson), rule?.Category, rule?.Priority);
        request.SlaPolicySnapshotJson = JsonSerializer.Serialize(snapshot, JsonOptions);
        request.FirstResponseDueAt = AddBusinessMinutes(createdAt, firstResponseMinutes, snapshot);
        request.ResolutionDueAt = AddBusinessMinutes(createdAt, resolutionMinutes, snapshot);
    }

    public static void MarkFirstResponse(ExternalRequest request, DateTimeOffset at)
        => request.FirstRespondedAt ??= at;

    public static void Pause(ExternalRequest request, DateTimeOffset at)
    {
        var snapshot = Snapshot(request);
        if (snapshot?.PauseWhileWaitingRequester != true || request.SlaPausedAt.HasValue
            || request.WorkItem.CompletedAt.HasValue)
            return;
        request.SlaPausedAt = at;
    }

    public static void Resume(ExternalRequest request, DateTimeOffset at)
    {
        if (!request.SlaPausedAt.HasValue) return;
        var snapshot = Snapshot(request);
        if (snapshot is null)
        {
            request.SlaPausedAt = null;
            return;
        }
        var pausedMinutes = BusinessMinutesBetween(request.SlaPausedAt.Value, at, snapshot);
        request.SlaPausedBusinessMinutes += pausedMinutes;
        if (!request.FirstRespondedAt.HasValue && request.FirstResponseDueAt.HasValue)
            request.FirstResponseDueAt = AddBusinessMinutes(
                request.FirstResponseDueAt.Value, pausedMinutes, snapshot);
        if (!request.WorkItem.CompletedAt.HasValue && request.ResolutionDueAt.HasValue)
            request.ResolutionDueAt = AddBusinessMinutes(
                request.ResolutionDueAt.Value, pausedMinutes, snapshot);
        request.SlaPausedAt = null;
    }

    public static ExternalRequestSlaDto MapRequest(
        ExternalRequest request,
        DateTimeOffset? now = null)
    {
        var snapshot = Snapshot(request);
        var current = now ?? DateTimeOffset.UtcNow;
        var nearDueMinutes = snapshot?.NearDueMinutes ?? 60;
        return new ExternalRequestSlaDto(
            Milestone(request.FirstResponseDueAt, request.FirstRespondedAt,
                request.SlaPausedAt.HasValue && !request.FirstRespondedAt.HasValue,
                nearDueMinutes, current),
            Milestone(request.ResolutionDueAt, request.WorkItem.CompletedAt,
                request.SlaPausedAt.HasValue && !request.WorkItem.CompletedAt.HasValue,
                nearDueMinutes, current),
            request.SlaPausedAt.HasValue,
            request.SlaPausedAt,
            request.SlaPausedBusinessMinutes,
            snapshot?.AlertsEnabled ?? false,
            nearDueMinutes);
    }

    public static DateTimeOffset AddBusinessMinutes(
        DateTimeOffset start,
        int minutes,
        SlaPolicySnapshotDto snapshot)
    {
        if (minutes <= 0) return start;
        var zone = FindTimeZone(snapshot.TimeZoneId);
        var local = TimeZoneInfo.ConvertTime(start, zone).DateTime;
        var serviceStart = TimeOnly.Parse(snapshot.ServiceStart);
        var serviceEnd = TimeOnly.Parse(snapshot.ServiceEnd);
        var holidays = snapshot.Holidays.Select(DateOnly.Parse).ToHashSet();
        var remaining = minutes;

        while (remaining > 0)
        {
            local = NormalizeToBusinessTime(
                local, serviceStart, serviceEnd, snapshot.BusinessDaysMask, holidays);
            var endOfDay = local.Date + serviceEnd.ToTimeSpan();
            var available = Math.Max(0, (int)Math.Floor((endOfDay - local).TotalMinutes));
            if (available == 0)
            {
                local = local.Date.AddDays(1) + serviceStart.ToTimeSpan();
                continue;
            }
            var consumed = Math.Min(remaining, available);
            local = local.AddMinutes(consumed);
            remaining -= consumed;
            if (remaining > 0)
                local = local.Date.AddDays(1) + serviceStart.ToTimeSpan();
        }
        return ToUtc(local, zone);
    }

    public static int BusinessMinutesBetween(
        DateTimeOffset start,
        DateTimeOffset end,
        SlaPolicySnapshotDto snapshot)
    {
        if (end <= start) return 0;
        var zone = FindTimeZone(snapshot.TimeZoneId);
        var localStart = TimeZoneInfo.ConvertTime(start, zone).DateTime;
        var localEnd = TimeZoneInfo.ConvertTime(end, zone).DateTime;
        var serviceStart = TimeOnly.Parse(snapshot.ServiceStart);
        var serviceEnd = TimeOnly.Parse(snapshot.ServiceEnd);
        var holidays = snapshot.Holidays.Select(DateOnly.Parse).ToHashSet();
        var cursor = localStart.Date;
        double total = 0;
        while (cursor <= localEnd.Date)
        {
            var date = DateOnly.FromDateTime(cursor);
            if (IsBusinessDay(date, snapshot.BusinessDaysMask, holidays))
            {
                var dayStart = cursor + serviceStart.ToTimeSpan();
                var dayEnd = cursor + serviceEnd.ToTimeSpan();
                var from = localStart > dayStart ? localStart : dayStart;
                var to = localEnd < dayEnd ? localEnd : dayEnd;
                if (to > from) total += (to - from).TotalMinutes;
            }
            cursor = cursor.AddDays(1);
        }
        return Math.Max(0, (int)Math.Floor(total));
    }

    public static string ValidateTimeZone(string value)
    {
        var normalized = value.Trim();
        _ = FindTimeZone(normalized);
        return normalized;
    }

    public static IReadOnlyList<string> NormalizeHolidays(IEnumerable<string> values)
        => values.Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => DateOnly.TryParse(x.Trim(), out var date)
                ? date.ToString("yyyy-MM-dd")
                : throw new DomainException($"Feriado inválido: {x}. Use aaaa-mm-dd."))
            .Distinct().OrderBy(x => x).ToList();

    public static IReadOnlyList<SlaRuleDto> NormalizeRules(IEnumerable<SlaRuleDto> rules)
    {
        var result = rules.OrderBy(x => x.Position).Select((rule, index) =>
        {
            var category = string.IsNullOrWhiteSpace(rule.Category) ? null : rule.Category.Trim();
            DomainException.Garantir(category is not null || rule.Priority.HasValue,
                "Cada regra de SLA precisa informar categoria ou prioridade.");
            DomainException.Garantir(category?.Length <= 120,
                "A categoria da regra deve ter no máximo 120 caracteres.");
            DomainException.Garantir(!rule.FirstResponseMinutes.HasValue
                || rule.FirstResponseMinutes.Value is >= 1 and <= 525_600,
                "O prazo de primeira resposta da regra é inválido.");
            DomainException.Garantir(!rule.ResolutionMinutes.HasValue
                || rule.ResolutionMinutes.Value is >= 1 and <= 2_102_400,
                "O prazo de resolução da regra é inválido.");
            DomainException.Garantir(rule.FirstResponseMinutes.HasValue
                || rule.ResolutionMinutes.HasValue,
                "A regra precisa substituir ao menos um prazo.");
            return rule with { Category = category, Position = index * 100 };
        }).ToList();
        return result;
    }

    public static SlaPolicySnapshotDto? Snapshot(ExternalRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SlaPolicySnapshotJson)) return null;
        try
        {
            return JsonSerializer.Deserialize<SlaPolicySnapshotDto>(
                request.SlaPolicySnapshotJson, JsonOptions);
        }
        catch (JsonException) { return null; }
    }

    private static SlaMilestoneDto Milestone(
        DateTimeOffset? dueAt,
        DateTimeOffset? metAt,
        bool paused,
        int nearDueMinutes,
        DateTimeOffset now)
    {
        if (!dueAt.HasValue)
            return new SlaMilestoneDto(SlaStatus.NotApplicable, null, metAt);
        if (metAt.HasValue)
            return new SlaMilestoneDto(
                metAt.Value <= dueAt.Value ? SlaStatus.Met : SlaStatus.Overdue,
                dueAt, metAt);
        if (paused)
            return new SlaMilestoneDto(SlaStatus.Paused, dueAt, null);
        if (now > dueAt.Value)
            return new SlaMilestoneDto(SlaStatus.Overdue, dueAt, null);
        if (dueAt.Value - now <= TimeSpan.FromMinutes(nearDueMinutes))
            return new SlaMilestoneDto(SlaStatus.NearDue, dueAt, null);
        return new SlaMilestoneDto(SlaStatus.WithinDeadline, dueAt, null);
    }

    private static bool RuleMatches(SlaRuleDto rule, string? category, Priority priority)
        => (string.IsNullOrWhiteSpace(rule.Category)
                || string.Equals(rule.Category, category, StringComparison.OrdinalIgnoreCase))
            && (!rule.Priority.HasValue || rule.Priority == priority);

    private static DateTime NormalizeToBusinessTime(
        DateTime local,
        TimeOnly serviceStart,
        TimeOnly serviceEnd,
        int mask,
        IReadOnlySet<DateOnly> holidays)
    {
        while (true)
        {
            var date = DateOnly.FromDateTime(local);
            if (!IsBusinessDay(date, mask, holidays))
            {
                local = local.Date.AddDays(1) + serviceStart.ToTimeSpan();
                continue;
            }
            var dayStart = local.Date + serviceStart.ToTimeSpan();
            var dayEnd = local.Date + serviceEnd.ToTimeSpan();
            if (local < dayStart) return dayStart;
            if (local >= dayEnd)
            {
                local = local.Date.AddDays(1) + serviceStart.ToTimeSpan();
                continue;
            }
            return local;
        }
    }

    private static bool IsBusinessDay(
        DateOnly date,
        int mask,
        IReadOnlySet<DateOnly> holidays)
    {
        var bit = 1 << (int)date.DayOfWeek;
        return (mask & bit) != 0 && !holidays.Contains(date);
    }

    private static TimeZoneInfo FindTimeZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException)
        {
            throw new DomainException("Fuso horário do SLA não encontrado.");
        }
        catch (InvalidTimeZoneException)
        {
            throw new DomainException("Fuso horário do SLA é inválido.");
        }
    }

    private static DateTimeOffset ToUtc(DateTime local, TimeZoneInfo zone)
    {
        var unspecified = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(unspecified)) unspecified = unspecified.AddMinutes(30);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(unspecified, zone), TimeSpan.Zero);
    }

    private static IReadOnlyList<T> Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }
}
