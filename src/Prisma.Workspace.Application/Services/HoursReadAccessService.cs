using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Services;

public sealed class HoursReadAccessService(IOrganizationContext organization,
    IOrganizationRepository organizations, ITeamRepository teams, IProjectAccessService access)
    : IHoursReadAccessService
{
    public async Task<HoursReadScope> GetScopeAsync(string actorId, bool forReporting, CancellationToken ct = default)
    {
        var organizationId = organization.RequireOrganizationId();
        var member = await organizations.GetMemberAsync(organizationId, actorId, ct);
        if (member is null || !member.IsActive || member.Role == OrganizationRole.ExternalRequester)
            throw new AcessoNegadoException();

        var denies = (await organizations.GetGrantsAsync(organizationId, ct))
            .Where(g => g.UserId == actorId && !g.IsAllowed && g.Permission == PlatformPermission.ViewReport).ToList();
        var enabled = !denies.Any(g => (g.Scope is PermissionScope.Organization or PermissionScope.Report) && g.ScopeId is null);
        var deniedReports = denies.Where(g => g.Scope == PermissionScope.Report && g.ScopeId.HasValue)
            .Select(g => g.ScopeId!.Value).ToHashSet();
        // ReportAuthorization usa também o ProjectId como ScopeId de Report nas consultas por projeto.
        var deniedProjects = denies.Where(g => g.Scope == PermissionScope.Project && g.ScopeId.HasValue)
            .Select(g => g.ScopeId!.Value).Concat(deniedReports).ToHashSet();
        var deniedItems = denies.Where(g => g.Scope == PermissionScope.WorkItem && g.ScopeId.HasValue)
            .Select(g => g.ScopeId!.Value).ToHashSet();
        var deniedTeams = denies.Where(g => g.Scope == PermissionScope.Team && g.ScopeId.HasValue)
            .Select(g => g.ScopeId!.Value).ToHashSet();
        var ownOnly = member.Role is not (OrganizationRole.Administrator or OrganizationRole.Manager);
        if (ownOnly) return HoursReadScope.Own(actorId) with
        {
            ReadEnabled = enabled, IncludeOwn = !forReporting,
            DeniedProjectIds = deniedProjects, DeniedReportIds = deniedReports, DeniedWorkItemIds = deniedItems,
            DeniedTeamIds = deniedTeams
        };

        var administrator = member.Role == OrganizationRole.Administrator;
        var visible = (await teams.GetAllWithMembersAsync(ct))
            .Where(t => !deniedTeams.Contains(t.Id) && (administrator || t.LeaderId == actorId)).ToList();
        var ids = visible.Select(t => t.Id).ToHashSet();
        if (administrator) ids.Add(Guid.Empty);
        return new(actorId, false, ids,
            administrator ? null : visible.SelectMany(t => t.Members).Select(m => m.UserId).ToHashSet(),
            enabled, !forReporting)
        {
            DeniedProjectIds = deniedProjects, DeniedReportIds = deniedReports, DeniedWorkItemIds = deniedItems,
            DeniedTeamIds = deniedTeams
        };
    }

    public async Task<IReadOnlyList<WorkItem>> FilterItemsAsync(HoursReadScope scope,
        IEnumerable<WorkItem> items, CancellationToken ct = default)
    {
        var candidates = items.Where(scope.Includes).ToList();
        var ids = await access.GetAccessibleProjectIdsAsync(candidates.Select(i => i.Board.ProjectId), scope.ActorId, ct);
        return candidates.Where(i => ids.Contains(i.Board.ProjectId)).ToList();
    }

    public async Task<IReadOnlyList<TimeEntry>> FilterEntriesAsync(HoursReadScope scope,
        IEnumerable<TimeEntry> entries, CancellationToken ct = default)
    {
        var candidates = entries.Where(scope.CanRead).ToList();
        var ids = await access.GetAccessibleProjectIdsAsync(candidates.Select(e => e.WorkItem.Board.ProjectId), scope.ActorId, ct);
        return candidates.Where(e => ids.Contains(e.WorkItem.Board.ProjectId)).ToList();
    }
}
