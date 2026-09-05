using Detran.Kanban.Domain.Entities;

namespace Detran.Kanban.Application.Interfaces;

/// <summary>
/// Repositório de equipes e membros.
/// </summary>
public interface ITeamRepository
{
    Task<IReadOnlyList<Team>> GetAllWithMembersAsync(CancellationToken cancellationToken = default);
    Task<Team?> GetByIdWithMembersAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Team?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<TeamMember?> GetMemberAsync(Guid teamId, string userId, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string name, Guid? exceptId = null, CancellationToken cancellationToken = default);
    Task AddAsync(Team team, CancellationToken cancellationToken = default);
    Task DeleteAsync(Team team, CancellationToken cancellationToken = default);
    Task RemoveMemberAsync(TeamMember member, CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
