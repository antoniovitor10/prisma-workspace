using Detran.Kanban.Domain.Entities;

namespace Detran.Kanban.Application.Interfaces;

public interface IAnalyticsRepository
{
    Task<IReadOnlyList<WorkItem>> GetWorkItemsAsync(
        Guid? projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExternalRequest>> GetExternalRequestsAsync(
        Guid? projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Project>> GetProjectsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Team>> GetTeamsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Sprint>> GetSprintsAsync(
        Guid? projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimeEntry>> GetTimeEntriesAsync(
        Guid? projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Comment>> GetCommentsForUserAsync(
        string userId, string? email, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SavedReport>> GetSavedReportsAsync(
        string userId, Guid? projectId, CancellationToken cancellationToken = default);
    Task<SavedReport?> GetSavedReportAsync(
        Guid id, CancellationToken cancellationToken = default);
    void AddSavedReport(SavedReport report);
    void RemoveSavedReport(SavedReport report);
    Task SaveAsync(CancellationToken cancellationToken = default);
}
