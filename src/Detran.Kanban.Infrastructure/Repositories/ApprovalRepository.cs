using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Detran.Kanban.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de aprovações usando EF Core.
/// </summary>
public class ApprovalRepository : IApprovalRepository
{
    private readonly AppDbContext _context;

    public ApprovalRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Approval?> GetByIdForApproverAsync(
        Guid id,
        string approverId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Approvals
            .FirstOrDefaultAsync(a => a.Id == id && a.ApproverId == approverId, cancellationToken);
    }

    public async Task<IReadOnlyList<Approval>> GetByWorkItemAsync(
        Guid workItemId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Approvals
            .AsNoTracking()
            .Include(a => a.WorkItem).ThenInclude(w => w.Board)
            .Where(a => a.WorkItemId == workItemId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Approval>> GetByApproverAsync(
        string approverId,
        int limite,
        CancellationToken cancellationToken = default)
    {
        return await _context.Approvals
            .AsNoTracking()
            .Include(a => a.WorkItem).ThenInclude(w => w.Board)
            .Where(a => a.ApproverId == approverId)
            .OrderBy(a => a.Status)
            .ThenByDescending(a => a.CreatedAt)
            .Take(limite)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasPendingAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        return await _context.Approvals
            .AnyAsync(a => a.WorkItemId == workItemId && a.Status == ApprovalStatus.Pendente, cancellationToken);
    }

    public async Task AddAsync(Approval approval, CancellationToken cancellationToken = default)
    {
        await _context.Approvals.AddAsync(approval, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
