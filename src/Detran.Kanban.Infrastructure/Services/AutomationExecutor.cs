using System.Text.Json;
using Detran.Kanban.Application.Features.Workflow;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;
using Detran.Kanban.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Detran.Kanban.Infrastructure.Services;

/// <summary>Executa automações de entrada em etapa com limite, fluxo, WIP e proteção contra ciclos.</summary>
public sealed class AutomationExecutor : IAutomationExecutor
{
    private const int MaxActions = 5;
    private readonly AppDbContext _context;
    private readonly IWorkflowRepository _workflow;

    public AutomationExecutor(AppDbContext context, IWorkflowRepository workflow)
        => (_context, _workflow) = (context, workflow);

    public async Task ExecuteStageEnteredAsync(
        Guid workItemId,
        Guid stageId,
        string actorId,
        CancellationToken ct = default)
    {
        var item = await _context.WorkItems
            .Include(x => x.Assignees)
            .Include(x => x.WorkItemTags)
            .Include(x => x.StageHistories)
            .FirstOrDefaultAsync(x => x.Id == workItemId, ct);
        if (item is null) return;

        var currentStageId = stageId;
        var visitedRules = new HashSet<Guid>();
        var visitedStages = new HashSet<Guid> { stageId };
        var processed = 0;
        var hasChanges = false;

        while (processed < MaxActions)
        {
            var rules = await _context.AutomationRules.AsNoTracking()
                .Where(x => x.BoardId == item.BoardId
                    && x.TriggerStageId == currentStageId
                    && x.IsActive)
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(ct);
            var moved = false;

            foreach (var rule in rules.Where(x => visitedRules.Add(x.Id)))
            {
                if (processed >= MaxActions) break;
                processed++;
                var outcome = await ApplyAsync(item, rule, visitedStages, ct);
                if (outcome.Changed)
                {
                    hasChanges = true;
                    _context.TaskEvents.Add(TaskEvent.Registrar(item.Id, actorId, "automation_executed",
                        JsonSerializer.Serialize(new
                        {
                            ruleId = rule.Id,
                            action = rule.ActionType.ToString(),
                            rule.ActionValue
                        })));
                }
                else
                {
                    _context.TaskEvents.Add(TaskEvent.Registrar(item.Id, actorId, "automation_skipped",
                        JsonSerializer.Serialize(new { ruleId = rule.Id, reason = outcome.Reason })));
                }

                if (!outcome.DestinationStageId.HasValue) continue;
                currentStageId = outcome.DestinationStageId.Value;
                moved = true;
                break;
            }

            if (!moved) break;
        }

        if (processed >= MaxActions)
            _context.TaskEvents.Add(TaskEvent.Registrar(item.Id, actorId, "automation_limit_reached",
                JsonSerializer.Serialize(new { maxActions = MaxActions })));
        if (hasChanges) item.UpdatedAt = DateTimeOffset.UtcNow;
        if (processed > 0) await _context.SaveChangesAsync(ct);
    }

    private async Task<AutomationOutcome> ApplyAsync(
        WorkItem item,
        AutomationRule rule,
        ISet<Guid> visitedStages,
        CancellationToken ct)
    {
        switch (rule.ActionType)
        {
            case AutomationActionType.AssignUser:
                if (item.Assignees.Any(x => x.UserId == rule.ActionValue))
                    return AutomationOutcome.Skip("already_assigned");
                var userExists = await _context.Users.AsNoTracking().AnyAsync(x => x.Id == rule.ActionValue, ct);
                if (!userExists) return AutomationOutcome.Skip("user_not_found");
                item.Assignees.Add(new WorkItemAssignee
                {
                    WorkItemId = item.Id,
                    UserId = rule.ActionValue,
                    AssignedAt = DateTimeOffset.UtcNow
                });
                return AutomationOutcome.Change();
            case AutomationActionType.SetPriority:
                if (!Enum.TryParse<Priority>(rule.ActionValue, true, out var priority))
                    return AutomationOutcome.Skip("invalid_priority");
                if (item.Priority == priority) return AutomationOutcome.Skip("no_change");
                item.Priority = priority;
                return AutomationOutcome.Change();
            case AutomationActionType.AddTag:
                if (!Guid.TryParse(rule.ActionValue, out var tagId))
                    return AutomationOutcome.Skip("invalid_tag");
                if (item.WorkItemTags.Any(x => x.TagId == tagId))
                    return AutomationOutcome.Skip("already_tagged");
                if (!await _context.Tags.AsNoTracking().AnyAsync(x => x.Id == tagId, ct))
                    return AutomationOutcome.Skip("tag_not_found");
                item.WorkItemTags.Add(new WorkItemTag { WorkItemId = item.Id, TagId = tagId });
                return AutomationOutcome.Change();
            case AutomationActionType.MoveToStage:
                return await MoveAsync(item, rule.ActionValue, visitedStages, ct);
            default:
                return AutomationOutcome.Skip("unsupported_action");
        }
    }

    private async Task<AutomationOutcome> MoveAsync(
        WorkItem item,
        string value,
        ISet<Guid> visitedStages,
        CancellationToken ct)
    {
        if (!Guid.TryParse(value, out var destinationId))
            return AutomationOutcome.Skip("invalid_stage");
        var destination = await _context.Stages.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == destinationId && x.BoardId == item.BoardId, ct);
        if (destination is null) return AutomationOutcome.Skip("stage_not_found");
        if (destination.Id == item.StageId) return AutomationOutcome.Skip("no_change");
        if (!visitedStages.Add(destination.Id)) return AutomationOutcome.Skip("runtime_cycle");

        try
        {
            await WorkflowMoveGuard.EnsureAllowedAsync(item, destination, _workflow, ct);
        }
        catch (DomainException exception)
        {
            return AutomationOutcome.Skip(exception.Message);
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var history in item.StageHistories.Where(x => x.LeftAt is null))
            history.LeftAt = now;
        item.StageHistories.Add(new StageHistory
        {
            WorkItemId = item.Id,
            StageId = destination.Id,
            EnteredAt = now
        });
        item.StageId = destination.Id;
        item.WorkflowStatusId = destination.WorkflowStatusId;
        item.CompletedAt = destination.Category == StageCategory.Done
            ? item.CompletedAt ?? now
            : null;
        if (destination.Category == StageCategory.InProgress && item.StartDate is null)
            item.StartDate = DateOnly.FromDateTime(now.UtcDateTime);
        return AutomationOutcome.Change(destination.Id);
    }

    private sealed record AutomationOutcome(bool Changed, Guid? DestinationStageId, string? Reason)
    {
        public static AutomationOutcome Change(Guid? destinationStageId = null)
            => new(true, destinationStageId, null);
        public static AutomationOutcome Skip(string reason) => new(false, null, reason);
    }
}
