using Detran.Kanban.Domain.Entities;

namespace Detran.Kanban.Application.Interfaces;

public interface IProjectRepository
{
    Task<IReadOnlyList<Project>> GetForUserAsync(
        string userId,
        bool includeArchived = false,
        CancellationToken cancellationToken = default);
    Task<Project?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Project?> GetByIdWithMembersAsync(Guid id, CancellationToken cancellationToken = default);
    Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default);
    Task AddAsync(Project project, CancellationToken cancellationToken = default);
    void AddCustomField(ProjectCustomFieldDefinition field);
    void AddEvent(ProjectEvent projectEvent);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
