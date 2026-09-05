using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Prisma.Workspace.Infrastructure.Repositories;

/// <summary>
/// Implementação do repositório de equipes usando EF Core.
/// </summary>
public class TeamRepository : ITeamRepository
{
    private readonly AppDbContext _context;

    public TeamRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Team>> GetAllWithMembersAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Teams
            .AsNoTracking()
            .Include(t => t.Members)
            .Include(t => t.Projects)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Team?> GetByIdWithMembersAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Teams
            .Include(t => t.Members)
            .Include(t => t.Projects)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Teams.FindAsync(new object?[] { id }, cancellationToken);
    }

    public async Task<TeamMember?> GetMemberAsync(Guid teamId, string userId, CancellationToken cancellationToken = default)
    {
        return await _context.TeamMembers
            .FirstOrDefaultAsync(m => m.TeamId == teamId && m.UserId == userId, cancellationToken);
    }

    public Task<bool> NameExistsAsync(
        string name, Guid? exceptId = null, CancellationToken cancellationToken = default)
        => _context.Teams.AnyAsync(t => t.Name == name && (!exceptId.HasValue || t.Id != exceptId), cancellationToken);

    public async Task AddAsync(Team team, CancellationToken cancellationToken = default)
    {
        await _context.Teams.AddAsync(team, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Team team, CancellationToken cancellationToken = default)
    {
        _context.Teams.Remove(team);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveMemberAsync(TeamMember member, CancellationToken cancellationToken = default)
    {
        _context.TeamMembers.Remove(member);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
