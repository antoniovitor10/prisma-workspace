using System.Linq.Expressions;
using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Application.Interfaces;

/// <summary>População autorizada de horas, antes de somar ou serializar lançamentos.</summary>
public sealed record HoursReadScope(string ActorId, bool OwnOnly, IReadOnlySet<Guid>? TeamIds,
    IReadOnlySet<string>? UserIds, bool ReadEnabled = true, bool IncludeOwn = false)
{
    public IReadOnlySet<Guid> DeniedProjectIds { get; init; } = new HashSet<Guid>();
    public IReadOnlySet<Guid> DeniedReportIds { get; init; } = new HashSet<Guid>();
    public IReadOnlySet<Guid> DeniedWorkItemIds { get; init; } = new HashSet<Guid>();
    public IReadOnlySet<Guid> DeniedTeamIds { get; init; } = new HashSet<Guid>();

    public static HoursReadScope Own(string actorId)
        => new(actorId, true, null, new HashSet<string> { actorId });

    public bool IncludesTeam(WorkItem item)
        => !DeniedTeamIds.Contains(item.TeamId ?? item.Board?.TeamId ?? Guid.Empty)
            && (TeamIds is null || TeamIds.Contains(item.TeamId ?? item.Board?.TeamId ?? Guid.Empty));

    public bool CanRead(WorkItem item, TimeEntry entry)
        => (IncludeOwn && entry.UserId == ActorId)
            || (ReadEnabled && !DeniedWorkItemIds.Contains(item.Id)
                && (DeniedProjectIds.Count == 0 || (item.Board is not null && !DeniedProjectIds.Contains(item.Board.ProjectId)))
                && IncludesTeam(item) && (!OwnOnly || entry.UserId == ActorId));

    public bool CanRead(TimeEntry entry) => CanRead(entry.WorkItem, entry);

    public bool Includes(WorkItem item)
        => ReadEnabled && !DeniedWorkItemIds.Contains(item.Id)
            && (DeniedProjectIds.Count == 0 || !DeniedProjectIds.Contains(item.Board.ProjectId))
            && IncludesTeam(item) && (!OwnOnly || item.ResponsibleId == ActorId
                || item.Assignees.Any(a => a.UserId == ActorId) || item.TimeEntries.Any(e => e.UserId == ActorId));

    // Mesmas regras para consultas SQL. Não usar Invoke nem filtrar depois do SUM.
    public Expression<Func<WorkItem, bool>> ItemPredicate()
    {
        var actor = ActorId;
        var own = OwnOnly;
        var enabled = ReadEnabled;
        var teams = TeamIds?.ToArray();
        var denied = DeniedProjectIds.ToArray();
        var deniedItems = DeniedWorkItemIds.ToArray();
        var deniedTeams = DeniedTeamIds.ToArray();
        return item => enabled && !deniedItems.Contains(item.Id) && !denied.Contains(item.Board.ProjectId)
            && !deniedTeams.Contains(item.TeamId ?? item.Board.TeamId ?? Guid.Empty)
            && (teams == null || teams.Contains(item.TeamId ?? item.Board.TeamId ?? Guid.Empty))
            && (!own || item.ResponsibleId == actor || item.Assignees.Any(a => a.UserId == actor)
                || item.TimeEntries.Any(e => e.UserId == actor));
    }

    public Expression<Func<TimeEntry, bool>> EntryPredicate()
    {
        var actor = ActorId;
        var own = OwnOnly;
        var self = IncludeOwn;
        var enabled = ReadEnabled;
        var teams = TeamIds?.ToArray();
        var denied = DeniedProjectIds.ToArray();
        var deniedItems = DeniedWorkItemIds.ToArray();
        var deniedTeams = DeniedTeamIds.ToArray();
        return entry => (self && entry.UserId == actor)
            || (enabled && !deniedItems.Contains(entry.WorkItemId) && !denied.Contains(entry.WorkItem.Board.ProjectId)
                && !deniedTeams.Contains(entry.WorkItem.TeamId ?? entry.WorkItem.Board.TeamId ?? Guid.Empty)
                && (own ? entry.UserId == actor
                    : teams == null || teams.Contains(entry.WorkItem.TeamId ?? entry.WorkItem.Board.TeamId ?? Guid.Empty)));
    }
}

public interface IHoursReadAccessService
{
    Task<HoursReadScope> GetScopeAsync(string actorId, bool forReporting, CancellationToken ct = default);
    Task<IReadOnlyList<WorkItem>> FilterItemsAsync(HoursReadScope scope, IEnumerable<WorkItem> items, CancellationToken ct = default);
    Task<IReadOnlyList<TimeEntry>> FilterEntriesAsync(HoursReadScope scope, IEnumerable<TimeEntry> entries, CancellationToken ct = default);
}
