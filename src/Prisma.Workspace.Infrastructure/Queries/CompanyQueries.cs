using Prisma.Workspace.Application.Features.Company;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Queries;

/// <summary>
/// Lado de leitura da área Empresa: projeção da galeria de projetos.
/// </summary>
public class CompanyQueries : ICompanyQueries
{
    private readonly AppDbContext _context;

    private readonly IHoursReadAccessService _hours;
    private readonly IProjectAccessService _access;
    public CompanyQueries(AppDbContext context, IHoursReadAccessService hours, IProjectAccessService access)
        => (_context,_hours,_access)=(context,hours,access);

    public async Task<IReadOnlyList<ProjectSummaryDto>> GetProjectsAsync(string actorId, CancellationToken cancellationToken = default)
    {
        var scope = await _hours.GetScopeAsync(actorId,true,cancellationToken);
        var projectIds = await _context.Boards.Select(b=>b.ProjectId).Distinct().ToListAsync(cancellationToken);
        var accessible = await _access.GetAccessibleProjectIdsAsync(projectIds,actorId,cancellationToken);
        var visibleEntries = _context.TimeEntries.Where(scope.EntryPredicate());
        var boards = await _context.Boards.AsNoTracking().Where(b=>accessible.Contains(b.ProjectId))
            .Select(b => new
            {
                b.Id,
                b.ProjectId,
                b.Name,
                b.Description,
                b.ClientId,
                ClientName = b.Client != null ? b.Client.Name : null,
                Stages = b.Project.Stages.OrderBy(s => s.Position).Select(s => s.Id).ToList(),
                Items = b.WorkItems.Where(w => w.ParentId == null)
                    .Select(w => new { w.StageId }).ToList(),
                TotalSeconds = visibleEntries.Where(e=>e.WorkItem.BoardId==b.Id)
                    .Sum(e => (double?)EF.Functions.DateDiffSecond(e.StartedAt, e.EndedAt ?? DateTimeOffset.UtcNow)) ?? 0
            })
            .ToListAsync(cancellationToken);

        return boards.Select(b =>
        {
            var lastStageId = b.Stages.LastOrDefault();
            var total = b.Items.Count;
            var done = lastStageId == Guid.Empty ? 0 : b.Items.Count(i => i.StageId == lastStageId);
            return new ProjectSummaryDto
            {
                Id = b.Id,
                ProjectId = b.ProjectId,
                Name = b.Name,
                Description = b.Description,
                ClientId = b.ClientId,
                ClientName = b.ClientName,
                TasksTotal = total,
                TasksDone = done,
                Progress = total == 0 ? 0 : (int)Math.Round(100.0 * done / total),
                TotalHours = Math.Round(b.TotalSeconds / 3600.0, 1)
            };
        }).OrderBy(x => x.Name).ToList();
    }
}
