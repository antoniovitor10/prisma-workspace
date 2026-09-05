using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;

namespace Detran.Kanban.Application.Interfaces;

public interface IWorkflowRepository
{
    Task<IReadOnlyList<WorkflowStatus>> GetStatusesAsync(Guid projectId, bool tracking = false, CancellationToken ct = default);
    Task<WorkflowStatus?> GetStatusAsync(Guid statusId, CancellationToken ct = default);
    Task<bool> NameExistsAsync(Guid projectId, string name, Guid? excludingId = null, CancellationToken ct = default);
    Task<bool> IsStatusInUseAsync(Guid statusId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowTransition>> GetTransitionsAsync(Guid projectId, CancellationToken ct = default);
    Task<bool> IsTransitionAllowedAsync(Guid sourceStatusId, Guid targetStatusId, CancellationToken ct = default);
    Task<Board?> GetBoardAsync(Guid boardId, CancellationToken ct = default);
    Task<Stage?> GetStageAsync(Guid stageId, CancellationToken ct = default);
    Task<int> CountActiveItemsInStageAsync(Guid stageId, Guid? excludingWorkItemId = null, CancellationToken ct = default);
    Task<WorkflowInheritanceMode> GetProjectInheritanceModeAsync(Guid projectId, CancellationToken ct = default)
        => Task.FromResult(WorkflowInheritanceMode.Custom);
    void AddStatus(WorkflowStatus status);
    void DeleteStatus(WorkflowStatus status);
    void ReplaceTransitions(IReadOnlyCollection<WorkflowTransition> current, IReadOnlyCollection<WorkflowTransition> replacements);
    void DeleteStage(Stage stage);
    Task SynchronizeStageWorkItemsAsync(Guid stageId, Guid? workflowStatusId, CancellationToken ct = default);
    Task SaveAsync(CancellationToken ct = default);
}
