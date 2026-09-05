using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Exceptions;
using MediatR;

namespace Detran.Kanban.Application.Features.Reports;

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

    public GetOrganizationHoursReportQueryHandler(IReportRepository reports, IUserDirectory users)
        => (_reports, _users) = (reports, users);

    public async Task<OrganizationHoursReportDto> Handle(
        GetOrganizationHoursReportQuery request, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = request.From ?? today.AddDays(-29);
        var to = request.To ?? today;
        DomainException.Garantir(to >= from, "A data final deve ser igual ou posterior à inicial.");
        DomainException.Garantir(to.DayNumber - from.DayNumber <= 730,
            "O relatório aceita um período de até 730 dias.");

        var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toExclusive = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var entries = await _reports.GetOrganizationTimeEntriesAsync(
            fromUtc, toExclusive,
            string.IsNullOrWhiteSpace(request.UserId) ? null : request.UserId, request.TeamId, ct);

        var businessHours = BusinessDays(from, to) * 8m;

        var lines = entries.Select(entry =>
        {
            var start = entry.StartedAt < fromUtc ? fromUtc : entry.StartedAt;
            var naturalEnd = entry.EndedAt ?? DateTimeOffset.UtcNow;
            var end = naturalEnd > toExclusive ? toExclusive : naturalEnd;
            var seconds = Math.Max(0, (end - start).TotalSeconds);
            var project = entry.WorkItem.Board.Project;
            return new
            {
                entry.UserId,
                ProjectKey = project?.Key ?? "—",
                ProjectName = project?.Name ?? "Sem projeto",
                entry.IsManual,
                Hours = Math.Round((decimal)seconds / 3600m, 2),
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
                        projectGroup.Where(x => !x.IsManual).Sum(x => x.Hours),
                        projectGroup.Where(x => x.IsManual).Sum(x => x.Hours),
                        projectGroup.Sum(x => x.Hours)))
                    .OrderByDescending(project => project.TotalHours)
                    .ToList();
                var auto = userGroup.Where(x => !x.IsManual).Sum(x => x.Hours);
                var manual = userGroup.Where(x => x.IsManual).Sum(x => x.Hours);
                return new HoursResponsibleDto(
                    userGroup.Key, names.GetValueOrDefault(userGroup.Key, userGroup.Key),
                    businessHours, auto, manual, auto + manual, projects);
            })
            .OrderByDescending(responsible => responsible.TotalHours)
            .ThenBy(responsible => responsible.Name)
            .ToList();

        var totalAuto = lines.Where(x => !x.IsManual).Sum(x => x.Hours);
        var totalManual = lines.Where(x => x.IsManual).Sum(x => x.Hours);
        return new OrganizationHoursReportDto(
            from, to, businessHours, totalAuto, totalManual, totalAuto + totalManual, byResponsible);
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
