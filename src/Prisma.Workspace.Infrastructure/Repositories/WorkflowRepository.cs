using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Repositories;

public sealed class WorkflowRepository : IWorkflowRepository
{
    private readonly AppDbContext _context;
    public WorkflowRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<WorkflowStatus>> GetStatusesAsync(
        Guid projectId, bool tracking = false, CancellationToken ct = default)
    {
        var query = _context.WorkflowStatuses.Where(x => x.ProjectId == projectId);
        if (!tracking) query = query.AsNoTracking();
        return await query.OrderBy(x => x.Position).ThenBy(x => x.Name).ToListAsync(ct);
    }

    public Task<WorkflowStatus?> GetStatusAsync(Guid statusId, CancellationToken ct = default)
        => _context.WorkflowStatuses.FirstOrDefaultAsync(x => x.Id == statusId, ct);

    public Task<bool> NameExistsAsync(
        Guid projectId, string name, Guid? excludingId = null, CancellationToken ct = default)
        => _context.WorkflowStatuses.AnyAsync(x => x.ProjectId == projectId
            && x.Name == name && (!excludingId.HasValue || x.Id != excludingId), ct);

    public async Task<bool> IsStatusInUseAsync(Guid statusId, CancellationToken ct = default)
        => await _context.Stages.AnyAsync(x => x.WorkflowStatusId == statusId, ct)
            || await _context.WorkItems.AnyAsync(x => x.WorkflowStatusId == statusId, ct);

    public async Task<IReadOnlyList<WorkflowTransition>> GetTransitionsAsync(
        Guid projectId, CancellationToken ct = default)
        => await _context.WorkflowTransitions
            .Where(x => x.SourceStatus.ProjectId == projectId)
            .OrderBy(x => x.SourceStatus.Position).ThenBy(x => x.TargetStatus.Position)
            .ToListAsync(ct);

    public Task<bool> IsTransitionAllowedAsync(
        Guid sourceStatusId, Guid targetStatusId, CancellationToken ct = default)
        => _context.WorkflowTransitions.AnyAsync(x => x.SourceStatusId == sourceStatusId
            && x.TargetStatusId == targetStatusId, ct);

    public Task<Board?> GetBoardAsync(Guid boardId, CancellationToken ct = default)
        => _context.Boards.Include(x => x.Stages).FirstOrDefaultAsync(x => x.Id == boardId, ct);

    public Task<Stage?> GetStageAsync(Guid stageId, CancellationToken ct = default)
        => _context.Stages.Include(x => x.Board).FirstOrDefaultAsync(x => x.Id == stageId, ct);

    public Task<int> CountActiveItemsInStageAsync(
        Guid stageId, Guid? excludingWorkItemId = null, CancellationToken ct = default)
        => _context.WorkItems.CountAsync(x => x.StageId == stageId && !x.IsArchived
            && (!excludingWorkItemId.HasValue || x.Id != excludingWorkItemId), ct);

    public Task<WorkflowInheritanceMode> GetProjectInheritanceModeAsync(Guid projectId, CancellationToken ct = default)
        => _context.Projects.Where(x => x.Id == projectId)
            .Select(x => x.WorkflowInheritanceMode).SingleAsync(ct);

    public void AddStatus(WorkflowStatus status) => _context.WorkflowStatuses.Add(status);

    public void DeleteStatus(WorkflowStatus status)
    {
        var transitions = _context.WorkflowTransitions
            .Where(x => x.SourceStatusId == status.Id || x.TargetStatusId == status.Id);
        _context.WorkflowTransitions.RemoveRange(transitions);
        _context.WorkflowStatuses.Remove(status);
    }

    public void ReplaceTransitions(
        IReadOnlyCollection<WorkflowTransition> current,
        IReadOnlyCollection<WorkflowTransition> replacements)
    {
        var replacementKeys = replacements
            .Select(x => (x.SourceStatusId, x.TargetStatusId)).ToHashSet();
        var currentKeys = current
            .Select(x => (x.SourceStatusId, x.TargetStatusId)).ToHashSet();
        _context.WorkflowTransitions.RemoveRange(current.Where(x =>
            !replacementKeys.Contains((x.SourceStatusId, x.TargetStatusId))));
        _context.WorkflowTransitions.AddRange(replacements.Where(x =>
            !currentKeys.Contains((x.SourceStatusId, x.TargetStatusId))));
    }

    public void DeleteStage(Stage stage) => _context.Stages.Remove(stage);

    public Task SynchronizeStageWorkItemsAsync(
        Guid stageId, Guid? workflowStatusId, CancellationToken ct = default)
        => _context.WorkItems.Where(x => x.StageId == stageId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.WorkflowStatusId, workflowStatusId)
                .SetProperty(x => x.UpdatedAt, DateTimeOffset.UtcNow), ct);

    public Task SaveAsync(CancellationToken ct = default) => _context.SaveChangesAsync(ct);
}
