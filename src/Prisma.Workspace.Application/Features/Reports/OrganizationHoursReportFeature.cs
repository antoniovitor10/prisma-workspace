using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Features.TimeEntries;
using MediatR;

namespace Prisma.Workspace.Application.Features.Reports;

public record HoursProjectLineDto(
    string ProjectKey,
    string ProjectName,
    decimal AutomaticHours,
    decimal ManualHours,
    decimal TotalHours);

public record HoursResponsibleDto(
    string UserId,
    string Name,
    decimal BusinessHours,
    decimal AutomaticHours,
    decimal ManualHours,
    decimal TotalHours,
    IReadOnlyList<HoursProjectLineDto> Projects);

public record OrganizationHoursReportDto(
    DateOnly From,
    DateOnly To,
    decimal BusinessHours,
    decimal AutomaticHours,
    decimal ManualHours,
    decimal TotalHours,
    IReadOnlyList<HoursResponsibleDto> ByResponsible);

public record GetOrganizationHoursReportQuery(
    DateOnly? From,
    DateOnly? To,
    string? UserId,
    Guid? TeamId,
    string ActorId) : IRequest<OrganizationHoursReportDto>;

public class GetOrganizationHoursReportQueryHandler
    : IRequestHandler<GetOrganizationHoursReportQuery, OrganizationHoursReportDto>
{
    private readonly IReportRepository _reports;
    private readonly IUserDirectory _users;
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationContext _organization;
    private readonly ITeamRepository _teams;
    private readonly IPermissionService _permissions;

    public GetOrganizationHoursReportQueryHandler(IReportRepository reports, IUserDirectory users,
        IOrganizationRepository organizations, IOrganizationContext organization,
        ITeamRepository teams, IPermissionService permissions)
        => (_reports, _users, _organizations, _organization, _teams, _permissions) =
            (reports, users, organizations, organization, teams, permissions);

    public async Task<OrganizationHoursReportDto> Handle(
        GetOrganizationHoursReportQuery request, CancellationToken ct)
    {
        var organizationId = _organization.RequireOrganizationId();
        var member = await _organizations.GetMemberAsync(organizationId, request.ActorId, ct);
        if (member is null || !member.IsActive || member.Role == OrganizationRole.ExternalRequester)
            throw new AcessoNegadoException();
        await _permissions.EnsureAsync(request.ActorId, PlatformPermission.View, PermissionScope.Report, null, ct);
        var grants = await _organizations.GetGrantsAsync(organizationId, ct);
        if (grants.Any(g => g.UserId == request.ActorId && !g.IsAllowed
            && g.Permission == PlatformPermission.ViewReport
            && (g.Scope is PermissionScope.Organization or PermissionScope.Report) && g.ScopeId is null))
            throw new AcessoNegadoException();

        var ownOnly = member.Role is not (OrganizationRole.Administrator or OrganizationRole.Manager);
        IReadOnlyCollection<Guid>? allowedTeamIds = null;
        if (!ownOnly)
        {
            var managedTeams = new List<Guid>();
            if (member.Role == OrganizationRole.Administrator) managedTeams.Add(Guid.Empty); // Sem equipe.
            foreach (var team in await _teams.GetAllWithMembersAsync(ct))
                if ((member.Role == OrganizationRole.Administrator || team.LeaderId == request.ActorId)
                    && await _permissions.HasAsync(request.ActorId, PlatformPermission.ViewReport,
                        PermissionScope.Team, team.Id, ct))
                    managedTeams.Add(team.Id);
            allowedTeamIds = managedTeams;
        }
        var today = TimeTrackingDay.Date(DateTimeOffset.UtcNow);
        var from = request.From ?? today.AddDays(-29);
        var to = request.To ?? today;
        DomainException.Garantir(to >= from, "A data final deve ser igual ou posterior à inicial.");
        DomainException.Garantir(to.DayNumber - from.DayNumber <= 730,
            "O relatório aceita um período de até 730 dias.");

        var fromUtc = TimeTrackingDay.Start(from);
        var toExclusive = TimeTrackingDay.Start(to.AddDays(1));
        var entries = ownOnly && !string.IsNullOrWhiteSpace(request.UserId) && request.UserId != request.ActorId
            ? [] : await _reports.GetOrganizationTimeEntriesAsync(
            fromUtc, toExclusive,
            ownOnly ? request.ActorId : string.IsNullOrWhiteSpace(request.UserId) ? null : request.UserId,
            request.TeamId, allowedTeamIds, ct);

        var businessHours = BusinessDays(from, to) * 8m;

        var now = DateTimeOffset.UtcNow;
        var lines = entries.Select(entry =>
        {
            var seconds = TimeTrackingDay.SecondsInRange(entry.StartedAt, entry.EndedAt ?? now, fromUtc, toExclusive);
            var project = entry.WorkItem.Board.Project;
            return new
            {
                entry.UserId,
                ProjectKey = project?.Key ?? "—",
                ProjectName = project?.Name ?? "Sem projeto",
                entry.IsManual,
                Hours = (decimal)seconds / 3600m,
            };
        }).ToList();

        var names = await _users.GetDisplayNamesAsync(lines.Select(x => x.UserId).Distinct().ToList(), ct);

        var byResponsible = lines
            .GroupBy(x => x.UserId)
            .Select(userGroup =>
            {
                var projects = userGroup
                    .GroupBy(x => new { x.ProjectKey, x.ProjectName })
                    .Select(projectGroup => new HoursProjectLineDto(
                        projectGroup.Key.ProjectKey,
                        projectGroup.Key.ProjectName,
                        Math.Round(projectGroup.Where(x => !x.IsManual).Sum(x => x.Hours), 2),
                        Math.Round(projectGroup.Where(x => x.IsManual).Sum(x => x.Hours), 2),
                        Math.Round(projectGroup.Sum(x => x.Hours), 2)))
                    .OrderByDescending(project => project.TotalHours)
                    .ToList();
                var auto = userGroup.Where(x => !x.IsManual).Sum(x => x.Hours);
                var manual = userGroup.Where(x => x.IsManual).Sum(x => x.Hours);
                return new HoursResponsibleDto(
                    userGroup.Key, names.GetValueOrDefault(userGroup.Key, userGroup.Key),
                    businessHours, Math.Round(auto, 2), Math.Round(manual, 2), Math.Round(auto + manual, 2), projects);
            })
            .OrderByDescending(responsible => responsible.TotalHours)
            .ThenBy(responsible => responsible.Name)
            .ToList();

        var totalAuto = lines.Where(x => !x.IsManual).Sum(x => x.Hours);
        var totalManual = lines.Where(x => x.IsManual).Sum(x => x.Hours);
        return new OrganizationHoursReportDto(
            from, to, businessHours, Math.Round(totalAuto, 2), Math.Round(totalManual, 2), Math.Round(totalAuto + totalManual, 2), byResponsible);
    }

    private static int BusinessDays(DateOnly from, DateOnly to)
    {
        var days = 0;
        for (var day = from; day <= to; day = day.AddDays(1))
            if (day.DayOfWeek != DayOfWeek.Saturday && day.DayOfWeek != DayOfWeek.Sunday)
                days++;
        return days;
    }
}
