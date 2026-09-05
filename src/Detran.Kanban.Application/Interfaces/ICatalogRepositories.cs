using Detran.Kanban.Domain.Entities;

namespace Detran.Kanban.Application.Interfaces;

/// <summary>Repositório de tags.</summary>
public interface ITagRepository
{
    Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Tag?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Tag tag, CancellationToken cancellationToken = default);
    Task DeleteAsync(Tag tag, CancellationToken cancellationToken = default);
}

/// <summary>Repositório de tipos de tarefa.</summary>
public interface ITaskTypeRepository
{
    Task<IReadOnlyList<TaskType>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TaskType?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(TaskType taskType, CancellationToken cancellationToken = default);
    Task DeleteAsync(TaskType taskType, CancellationToken cancellationToken = default);
}

/// <summary>Repositório de clientes.</summary>
public interface IClientRepository
{
    /// <summary>Clientes com a contagem de quadros vinculados.</summary>
    Task<IReadOnlyList<(Client Client, int BoardsCount)>> GetAllWithBoardCountAsync(CancellationToken cancellationToken = default);
    Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Client client, CancellationToken cancellationToken = default);
    Task DeleteAsync(Client client, CancellationToken cancellationToken = default);
}
