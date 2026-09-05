using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Services;
using Detran.Kanban.Domain.Exceptions;
using System.Data;

namespace Detran.Kanban.Infrastructure.Repositories;

public class WorkItemManagementRepository : IWorkItemManagementRepository, IWorkItemSearchRepository
{
    private readonly AppDbContext _context;
    public WorkItemManagementRepository(AppDbContext context) => _context = context;

    public Task<WorkItem?> GetDetailedAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.WorkItems.AsNoTracking().AsSplitQuery()
            .Include(x => x.Board).ThenInclude(x => x.Project).ThenInclude(x => x!.CustomFields)
            .Include(x => x.Board).ThenInclude(x => x.Project).ThenInclude(x => x!.Members)
            .Include(x => x.Board).ThenInclude(x => x.Project).ThenInclude(x => x!.Teams)
            .Include(x => x.Board).ThenInclude(x => x.Project).ThenInclude(x => x!.Teams)
            .Include(x => x.Board).ThenInclude(x => x.Project).ThenInclude(x => x!.Tags).ThenInclude(x => x.Tag)
            .Include(x => x.Board).ThenInclude(x => x.Team)
            .Include(x => x.Team)
            .Include(x => x.Stage)
            .Include(x => x.WorkflowStatus)
            .Include(x => x.Sprint)
            .Include(x => x.TaskType)
            .Include(x => x.Assignees)
            .Include(x => x.Followers)
            .Include(x => x.WorkItemTags).ThenInclude(x => x.Tag)
            .Include(x => x.ChecklistItems)
            .Include(x => x.SubItems)
            .Include(x => x.Attachments)
            .Include(x => x.Comments)
            .Include(x => x.TimeEntries)
            .Include(x => x.CustomFieldValues).ThenInclude(x => x.FieldDefinition)
            .Include(x => x.ExternalRequest).ThenInclude(x => x!.Messages)
            .Include(x => x.OutgoingLinks).ThenInclude(x => x.TargetWorkItem).ThenInclude(x => x.Board).ThenInclude(x => x.Project)
            .Include(x => x.IncomingLinks).ThenInclude(x => x.SourceWorkItem).ThenInclude(x => x.Board).ThenInclude(x => x.Project)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<WorkItem?> GetEditableAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.WorkItems.AsSplitQuery()
            .Include(x => x.Board).ThenInclude(x => x.Project).ThenInclude(x => x!.CustomFields)
            .Include(x => x.Board).ThenInclude(x => x.Project).ThenInclude(x => x!.Members)
            .Include(x => x.Board).ThenInclude(x => x.Project).ThenInclude(x => x!.Teams)
            .Include(x => x.Team)
            .Include(x => x.Stage)
            .Include(x => x.Assignees)
            .Include(x => x.Followers)
            .Include(x => x.WorkItemTags)
            .Include(x => x.ChecklistItems)
            .Include(x => x.StageHistories)
            .Include(x => x.CustomFieldValues)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<WorkItemLink?> GetLinkAsync(
        Guid workItemId,
        Guid linkId,
        CancellationToken cancellationToken = default)
        => _context.WorkItemLinks.FirstOrDefaultAsync(
            x => x.Id == linkId
                && (x.SourceWorkItemId == workItemId || x.TargetWorkItemId == workItemId),
            cancellationToken);

    public async Task AddLinkAsync(WorkItemLink link, CancellationToken cancellationToken = default)
    {
        await _context.WorkItemLinks.AddAsync(link, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryAddLinkAcyclicAsync(
        WorkItemLink link,
        CancellationToken cancellationToken = default)
    {
        if (!_context.Database.IsRelational())
            return await ValidateAndAddLinkAsync(link, cancellationToken);

        await _context.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await _context.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        try
        {
            var organizationId = await GetLinkOrganizationAsync(link, cancellationToken);
            await AcquireOrganizationDependencyLockAsync(
                organizationId, transaction.GetDbTransaction(), cancellationToken);
            var added = await ValidateAndAddLinkAsync(link, cancellationToken);
            if (added) await transaction.CommitAsync(cancellationToken);
            else await transaction.RollbackAsync(cancellationToken);
            return added;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
        finally
        {
            await _context.Database.CloseConnectionAsync();
        }
    }

    private async Task<bool> ValidateAndAddLinkAsync(
        WorkItemLink link,
        CancellationToken cancellationToken)
    {
        var organizationId = await GetLinkOrganizationAsync(link, cancellationToken);
        if (DependencyGraphService.TryCanonicalize(
                link.SourceWorkItemId, link.TargetWorkItemId, link.Type, out var proposed))
        {
            var links = await _context.WorkItemLinks.AsNoTracking()
                .Where(current =>
                    current.SourceWorkItem.Board.OrganizationId == organizationId
                    && current.TargetWorkItem.Board.OrganizationId == organizationId
                    && (current.Type == WorkItemLinkType.DependsOn || current.Type == WorkItemLinkType.Blocks))
                .Select(current => new
                {
                    current.SourceWorkItemId,
                    current.TargetWorkItemId,
                    current.Type
                })
                .ToListAsync(cancellationToken);
            var edges = links.Select(current =>
            {
                DependencyGraphService.TryCanonicalize(
                    current.SourceWorkItemId, current.TargetWorkItemId, current.Type, out var edge);
                return edge;
            });
            if (DependencyGraphService.WouldCreateCycle(edges, proposed)) return false;
        }

        await _context.WorkItemLinks.AddAsync(link, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<Guid> GetLinkOrganizationAsync(
        WorkItemLink link,
        CancellationToken cancellationToken)
    {
        var organizations = await _context.WorkItems.AsNoTracking()
            .Where(item => item.Id == link.SourceWorkItemId || item.Id == link.TargetWorkItemId)
            .Select(item => item.Board.OrganizationId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (organizations.Count != 1)
            throw new DomainException("As tarefas relacionadas devem pertencer à mesma organização.");
        return organizations[0];
    }

    private async Task AcquireOrganizationDependencyLockAsync(
        Guid organizationId,
        System.Data.Common.DbTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = _context.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource, 'Exclusive', 'Transaction', 10000; SELECT @result;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@Resource";
        parameter.Value = $"work-item-dependencies:{organizationId:N}";
        command.Parameters.Add(parameter);
        var result = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        if (result < 0)
            throw new DomainException("Não foi possível serializar a alteração de dependências.");
    }

    public async Task<IReadOnlyList<WorkItemSearchEntry>> SearchVisibleAsync(
        string query,
        string userId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var deniedProjects = await _context.PermissionGrants.AsNoTracking()
            .Where(grant => grant.UserId == userId
                && grant.Permission == PlatformPermission.View
                && !grant.IsAllowed
                && grant.Scope == PermissionScope.Project
                && grant.ScopeId.HasValue)
            .Select(grant => grant.ScopeId!.Value)
            .ToListAsync(cancellationToken);
        var term = query.Trim();
        var separator = term.LastIndexOf('-');
        var keyTerm = separator > 0 ? term[..separator] : null;
        var numberTerm = separator > 0 ? term[(separator + 1)..] : term;

        return await _context.WorkItems.AsNoTracking()
            .Where(item => !item.IsArchived
                && item.Board.ProjectId.HasValue
                && !deniedProjects.Contains(item.Board.ProjectId.Value)
                && (item.Title.Contains(term)
                    || (keyTerm == null && item.Number.ToString().Contains(numberTerm))
                    || (keyTerm != null && item.Board.Project != null
                        && item.Board.Project.Key.Contains(keyTerm)
                        && item.Number.ToString().Contains(numberTerm))))
            .OrderByDescending(item => item.UpdatedAt)
            .Take(limit)
            .Select(item => new WorkItemSearchEntry(
                item.Id,
                item.Board.ProjectId!.Value,
                item.Board.Project!.Key,
                item.Number,
                item.Title,
                item.WorkflowStatus != null
                    ? item.WorkflowStatus.Name
                    : item.Stage != null ? item.Stage.Name : "Backlog"))
            .ToListAsync(cancellationToken);
    }

    public async Task RemoveLinkAsync(WorkItemLink link, CancellationToken cancellationToken = default)
    {
        _context.WorkItemLinks.Remove(link);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AddFollowerAsync(
        Guid workItemId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (await _context.WorkItemFollowers.AnyAsync(
                x => x.WorkItemId == workItemId && x.UserId == userId, cancellationToken))
            return;
        await _context.WorkItemFollowers.AddAsync(new WorkItemFollower
        {
            WorkItemId = workItemId,
            UserId = userId,
            FollowedAt = DateTimeOffset.UtcNow
        }, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveFollowerAsync(
        Guid workItemId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var follower = await _context.WorkItemFollowers.FirstOrDefaultAsync(
            x => x.WorkItemId == workItemId && x.UserId == userId, cancellationToken);
        if (follower is null) return;
        _context.WorkItemFollowers.Remove(follower);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);

    public async Task SaveWithEventAsync(
        TaskEvent taskEvent,
        CancellationToken cancellationToken = default)
    {
        await _context.TaskEvents.AddAsync(taskEvent, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
