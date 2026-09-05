using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Repositories;

/// <summary>Repositório de tags usando EF Core.</summary>
public class TagRepository : ITagRepository
{
    private readonly AppDbContext _context;
    public TagRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken ct = default)
        => await _context.Tags.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);

    public async Task<Tag?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Tags.FindAsync(new object?[] { id }, ct);

    public async Task AddAsync(Tag tag, CancellationToken ct = default)
    {
        await _context.Tags.AddAsync(tag, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Tag tag, CancellationToken ct = default)
    {
        _context.Tags.Remove(tag);
        await _context.SaveChangesAsync(ct);
    }
}

/// <summary>Repositório de tipos de tarefa usando EF Core.</summary>
public class TaskTypeRepository : ITaskTypeRepository
{
    private readonly AppDbContext _context;
    public TaskTypeRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<TaskType>> GetAllAsync(CancellationToken ct = default)
        => await _context.TaskTypes.AsNoTracking().OrderBy(t => t.Name).ToListAsync(ct);

    public async Task<TaskType?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.TaskTypes.FindAsync(new object?[] { id }, ct);

    public async Task AddAsync(TaskType taskType, CancellationToken ct = default)
    {
        await _context.TaskTypes.AddAsync(taskType, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(TaskType taskType, CancellationToken ct = default)
    {
        _context.TaskTypes.Remove(taskType);
        await _context.SaveChangesAsync(ct);
    }
}

/// <summary>Repositório de clientes usando EF Core.</summary>
public class ClientRepository : IClientRepository
{
    private readonly AppDbContext _context;
    public ClientRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyList<(Client Client, int BoardsCount)>> GetAllWithBoardCountAsync(CancellationToken ct = default)
    {
        var rows = await _context.Clients
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new { Client = c, BoardsCount = c.Boards.Count })
            .ToListAsync(ct);
        return rows.Select(r => (r.Client, r.BoardsCount)).ToList();
    }

    public async Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.Clients.FindAsync(new object?[] { id }, ct);

    public async Task AddAsync(Client client, CancellationToken ct = default)
    {
        await _context.Clients.AddAsync(client, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Client client, CancellationToken ct = default)
    {
        _context.Clients.Remove(client);
        await _context.SaveChangesAsync(ct);
    }
}
