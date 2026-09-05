using Detran.Kanban.Domain.Entities;

namespace Detran.Kanban.Application.Interfaces;

public interface IReportRepository
{
    Task<IReadOnlyList<WorkItem>> GetTimeReportItemsAsync(
        Guid projectId,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        Guid? teamId,
        string? userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkItem>> GetCustomFieldReportItemsAsync(
        Guid projectId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Lançamentos de tempo de toda a organização no período (isolamento por
    /// organização é aplicado automaticamente pelo filtro de consulta).
    /// </summary>
    Task<IReadOnlyList<TimeEntry>> GetOrganizationTimeEntriesAsync(
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        string? userId,
        Guid? teamId,
        CancellationToken cancellationToken = default);
}
