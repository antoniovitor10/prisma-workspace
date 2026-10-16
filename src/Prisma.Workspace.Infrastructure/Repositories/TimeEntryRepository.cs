using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Prisma.Workspace.Domain.Exceptions;
using System.Security.Cryptography;
using System.Text;

namespace Prisma.Workspace.Infrastructure.Repositories;

/// <summary>
/// Implementacao do repositorio de lancamentos de tempo usando EF Core.
/// </summary>
public class TimeEntryRepository : ITimeEntryRepository
{
    private readonly AppDbContext _context;
    private readonly IProjectAccessService? _projectAccess;

    public TimeEntryRepository(AppDbContext context, IProjectAccessService? projectAccess = null)
    {
        _context = context;
        _projectAccess = projectAccess;
    }

    public async Task<TimeEntry?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.TimeEntries
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<TimeEntry?> GetRunningByUserAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TimeEntries
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.EndedAt == null)
            .OrderByDescending(t => t.StartedAt)
            .ThenBy(t => t.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<TimeEntry> StartTimerAsync(Guid workItemId, string userId, string? note,
        CancellationToken cancellationToken = default) => MutateTimerAsync(userId, async () =>
    {
        var running = await RunningTimersAsync(userId, cancellationToken);
        // Preserva a idempotência da tarefa solicitada e torna legados duplicados encerráveis/editáveis.
        var retained = running.FirstOrDefault(entry => entry.WorkItemId == workItemId);
        foreach (var entry in running)
            if (retained is null || entry.Id != retained.Id) entry.Encerrar();
        if (retained is not null) return retained;
        var created = TimeEntry.IniciarAgora(workItemId, userId, note);
        await _context.TimeEntries.AddAsync(created, cancellationToken);
        return created;
    }, cancellationToken);

    public Task<TimeEntry> StopTimerAsync(string userId, Guid? workItemId, string? note,
        CancellationToken cancellationToken = default) => MutateTimerAsync(userId, async () =>
    {
        var running = await RunningTimersAsync(userId, cancellationToken);
        var current = running.FirstOrDefault();
        DomainException.Garantir(current is not null, "Não existe timer em andamento.");
        DomainException.Garantir(!workItemId.HasValue || current!.WorkItemId == workItemId.Value,
            "O timer em andamento pertence a outro card.");
        foreach (var entry in running) entry.Encerrar(entry.Id == current!.Id ? note : null);
        return current!;
    }, cancellationToken);

    private Task<List<TimeEntry>> RunningTimersAsync(string userId, CancellationToken ct) =>
        // D58: manter os filtros do tenant, inclusive ao recuperar dados legados.
        _context.TimeEntries.Where(entry => entry.UserId == userId && entry.EndedAt == null)
            .OrderByDescending(entry => entry.StartedAt).ThenBy(entry => entry.Id).ToListAsync(ct);

    private async Task<TimeEntry> MutateTimerAsync(string userId, Func<Task<TimeEntry>> mutate, CancellationToken ct)
    {
        if (!_context.Database.IsRelational())
        {
            _context.ChangeTracker.Clear();
            var result = await mutate();
            await _context.SaveChangesAsync(ct);
            return result;
        }
        return await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            _context.ChangeTracker.Clear();
            await using var transaction = await _context.Database.BeginTransactionAsync(ct);
            await using var command = _context.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; SELECT @result;";
            var resource = command.CreateParameter();
            resource.ParameterName = "@resource";
            resource.Value = "PrismaTimeEntry:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(userId)));
            command.Parameters.Add(resource);
            var acquired = await command.ExecuteScalarAsync(ct);
            DomainException.Garantir(acquired is int status && status >= 0,
                "Não foi possível atualizar o cronômetro agora. Tente novamente.");
            var result = await mutate();
            // Encerramentos, inclusão e auditoria são gravados e confirmados juntos.
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return result;
        });
    }

    public async Task<IReadOnlyList<TimeEntry>> GetByWorkItemIdAsync(
        Guid workItemId,
        CancellationToken cancellationToken = default)
    {
        return await _context.TimeEntries
            .AsNoTracking()
            .Where(t => t.WorkItemId == workItemId)
            .OrderByDescending(t => t.StartedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<TimeEntry> AddAsync(TimeEntry timeEntry, CancellationToken cancellationToken = default)
    {
        await _context.TimeEntries.AddAsync(timeEntry, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return timeEntry;
    }

    public async Task DeleteAsync(TimeEntry timeEntry, CancellationToken cancellationToken = default)
    {
        _context.TimeEntries.Remove(timeEntry);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> HasOverlapAsync(string userId, Guid exceptId, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken = default)
        => _context.TimeEntries.IgnoreQueryFilters().AnyAsync(t => t.UserId == userId && t.Id != exceptId
            && t.StartedAt < end && (t.EndedAt == null || t.EndedAt > start), cancellationToken);

    public async Task<IReadOnlyList<TimeEntry>> GetUserPeriodsForValidationAsync(string userId,
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await _context.TimeEntries.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.UserId == userId && t.StartedAt < to && (t.EndedAt == null || t.EndedAt > from))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Prisma.Workspace.Application.Features.MeTime.Queries.TimeAdjustmentTaskDto>> SearchAdjustmentTasksAsync(
        string userId, string query, CancellationToken cancellationToken = default)
    {
        // Uma consulta de candidatos, sem carregar cartões nem fazer uma requisição por quadro.
        var text = query.Trim().TrimStart('#');
        var candidates = await _context.WorkItems.AsNoTracking()
            .Where(w => !w.IsArchived && !w.Board.Project.IsArchived
                && (text == "" || w.Title.Contains(text) || w.Number.ToString().Contains(text)))
            .OrderByDescending(w => w.ResponsibleId == userId || w.Assignees.Any(a => a.UserId == userId))
            .ThenBy(w => w.Title).ThenBy(w => w.Id)
            .Select(w => new { w.Id, w.Number, w.Title, BoardName = w.Board.Name,
                w.Board.ProjectId, Assigned = w.ResponsibleId == userId || w.Assignees.Any(a => a.UserId == userId) })
            .ToListAsync(cancellationToken);
        var accessible = _projectAccess is null ? new HashSet<Guid>()
            : await _projectAccess.GetAccessibleProjectIdsAsync(candidates.Select(w => w.ProjectId), userId, cancellationToken);
        return candidates.Where(w => accessible.Contains(w.ProjectId)).Take(50)
            .Select(w => new Prisma.Workspace.Application.Features.MeTime.Queries.TimeAdjustmentTaskDto(
                w.Id, w.Number, w.Title, w.BoardName, w.Assigned)).ToList();
    }

    public async Task UpdateAsync(TimeEntry timeEntry, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(timeEntry).State == EntityState.Detached)
            _context.TimeEntries.Update(timeEntry);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TimeEntry>> GetVisibleEntriesAsync(HoursReadScope scope,
        Guid? workItemId = null, Guid? boardId = null, string? userId = null, CancellationToken ct = default)
    {
        var query = _context.TimeEntries.AsNoTracking().Include(e=>e.WorkItem).ThenInclude(i=>i.Board)
            .Where(scope.EntryPredicate());
        if (workItemId.HasValue) query = query.Where(e=>e.WorkItemId==workItemId);
        if (boardId.HasValue) query = query.Where(e=>e.WorkItem.BoardId==boardId);
        if (!string.IsNullOrWhiteSpace(userId)) query = query.Where(e=>e.UserId==userId);
        return await query.OrderByDescending(e=>e.StartedAt).ToListAsync(ct);
    }

    public async Task<int> GetTotalSecondsByWorkItemIdAsync(
        Guid workItemId,
        CancellationToken cancellationToken = default)
    {
        var entries = await _context.TimeEntries
            .AsNoTracking()
            .Where(t => t.WorkItemId == workItemId)
            .ToListAsync(cancellationToken);

        return SumDurations(entries);
    }

    public async Task<int> GetTotalSecondsByUserIdAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var entries = await _context.TimeEntries
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .ToListAsync(cancellationToken);

        return SumDurations(entries);
    }

    public async Task<int> GetTotalSecondsByBoardIdAsync(
        Guid boardId,
        CancellationToken cancellationToken = default)
    {
        var entries = await _context.TimeEntries
            .AsNoTracking()
            .Where(t => t.WorkItem.BoardId == boardId)
            .ToListAsync(cancellationToken);

        return SumDurations(entries);
    }

    public async Task<IReadOnlyList<TimeEntry>> GetByUserInRangeAsync(
        string userId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken = default)
    {
        return await _context.TimeEntries
            .AsNoTracking()
            .Where(t => t.UserId == userId && t.StartedAt < toExclusive
                && (t.EndedAt == null || t.EndedAt > fromInclusive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TimeEntry>> GetByUserInRangeWithWorkItemAsync(
        string userId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken = default)
    {
        return await _context.TimeEntries
            .AsNoTracking()
            .Include(t => t.WorkItem)
            .Where(t => t.UserId == userId && t.StartedAt < toExclusive
                && (t.EndedAt == null || t.EndedAt > fromInclusive))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TimeEntry>> GetByUsersInRangeAsync(
        IReadOnlyCollection<string> userIds,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        CancellationToken cancellationToken = default)
    {
        return await _context.TimeEntries
            .AsNoTracking()
            .Include(t=>t.WorkItem).ThenInclude(i=>i.Board)
            .Where(t => userIds.Contains(t.UserId) && t.StartedAt < toExclusive
                && (t.EndedAt == null || t.EndedAt > fromInclusive))
            .ToListAsync(cancellationToken);
    }

    private static int SumDurations(IEnumerable<TimeEntry> entries)
    {
        var now = DateTimeOffset.UtcNow;
        return entries.Sum(entry =>
        {
            var endedAt = entry.EndedAt ?? now;
            return Math.Max(0, (int)(endedAt - entry.StartedAt).TotalSeconds);
        });
    }
}
