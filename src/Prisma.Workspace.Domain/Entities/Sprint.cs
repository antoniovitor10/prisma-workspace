using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;

namespace Prisma.Workspace.Domain.Entities;

/// <summary>Iteração Scrum planejada para um time do projeto.</summary>
public class Sprint
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? TeamId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Goal { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public SprintStatus Status { get; set; } = SprintStatus.Planned;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }

    public Project Project { get; set; } = null!;
    public Team? Team { get; set; }
    public ICollection<SprintCapacity> Capacities { get; set; } = new List<SprintCapacity>();
    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
    public ICollection<SprintItemSnapshot> ItemSnapshots { get; set; } = new List<SprintItemSnapshot>();

    public bool IsTerminal => Status is SprintStatus.Closed or SprintStatus.Cancelled;

    public static Sprint Criar(Guid projectId, Guid? teamId, string nome, DateOnly inicio, DateOnly fim, string? meta)
    {
        DomainException.Garantir(projectId != Guid.Empty, "Projeto obrigatório.");
        DomainException.Garantir(!string.IsNullOrWhiteSpace(nome), "Nome da sprint obrigatório.");
        DomainException.Garantir(fim >= inicio, "Fim da sprint deve ser igual ou posterior ao início.");
        return new Sprint
        {
            Id = Guid.NewGuid(), ProjectId = projectId, TeamId = teamId,
            Name = nome.Trim(), Goal = string.IsNullOrWhiteSpace(meta) ? null : meta.Trim(),
            StartDate = inicio, EndDate = fim, CreatedAt = DateTimeOffset.UtcNow
        };
    }

    public void Update(string name, string? goal, DateOnly startDate, DateOnly endDate)
    {
        DomainException.Garantir(!IsTerminal, "Sprint concluída ou cancelada não pode ser editada.");
        DomainException.Garantir(!string.IsNullOrWhiteSpace(name), "Nome da sprint obrigatório.");
        DomainException.Garantir(endDate >= startDate, "Fim da sprint deve ser igual ou posterior ao início.");

        Name = name.Trim();
        Goal = string.IsNullOrWhiteSpace(goal) ? null : goal.Trim();
        StartDate = startDate;
        EndDate = endDate;
    }

    public void CaptureHistory(
        IEnumerable<WorkItem> workItems,
        SprintIncompleteItemsAction incompleteItemsAction,
        Guid? destinationSprintId)
    {
        if (ItemSnapshots.Count > 0) return;

        foreach (var item in workItems)
        {
            var outcome = item.CompletedAt.HasValue
                ? SprintItemOutcome.Completed
                : incompleteItemsAction == SprintIncompleteItemsAction.MoveToSprint
                    ? SprintItemOutcome.MovedToSprint
                    : SprintItemOutcome.ReturnedToBacklog;
            ItemSnapshots.Add(SprintItemSnapshot.Capture(
                Id, item, outcome,
                outcome == SprintItemOutcome.MovedToSprint ? destinationSprintId : null));
        }
    }

    public void ChangeStatus(SprintStatus nextStatus, bool projectHasAnotherActiveSprint = false)
    {
        if (Status == nextStatus) return;

        var isAllowedTransition =
            Status == SprintStatus.Planned && nextStatus == SprintStatus.Active
            || Status == SprintStatus.Active && nextStatus == SprintStatus.Closed
            || Status is SprintStatus.Planned or SprintStatus.Active && nextStatus == SprintStatus.Cancelled;

        DomainException.Garantir(isAllowedTransition,
            "A sprint deve seguir o ciclo Planejada → Ativa → Concluída, podendo ser cancelada antes da conclusão.");
        DomainException.Garantir(nextStatus != SprintStatus.Active || !projectHasAnotherActiveSprint,
            "Já existe uma sprint ativa para o projeto.");

        Status = nextStatus;
        if (nextStatus == SprintStatus.Closed) CompletedAt = DateTimeOffset.UtcNow;
        if (nextStatus == SprintStatus.Cancelled) CancelledAt = DateTimeOffset.UtcNow;
    }
}

public class SprintCapacity
{
    public Guid SprintId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public decimal AvailableHours { get; set; }
    public decimal DaysOffHours { get; set; }
    public Sprint Sprint { get; set; } = null!;
}

/// <summary>Fotografia imutável do escopo no encerramento ou cancelamento da sprint.</summary>
public class SprintItemSnapshot
{
    public Guid Id { get; set; }
    public Guid SprintId { get; set; }
    public Guid WorkItemId { get; set; }
    public long WorkItemNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public WorkItemKind Kind { get; set; }
    public int? Points { get; set; }
    public decimal? EstimatedHours { get; set; }
    public bool WasCompleted { get; set; }
    public DateTimeOffset? WorkItemCompletedAt { get; set; }
    public SprintItemOutcome Outcome { get; set; }
    public Guid? DestinationSprintId { get; set; }
    public DateTimeOffset CapturedAt { get; set; }

    public Sprint Sprint { get; set; } = null!;

    public static SprintItemSnapshot Capture(
        Guid sprintId,
        WorkItem item,
        SprintItemOutcome outcome,
        Guid? destinationSprintId) => new()
    {
        Id = Guid.NewGuid(),
        SprintId = sprintId,
        WorkItemId = item.Id,
        WorkItemNumber = item.Number,
        Title = item.Title,
        Kind = item.Kind,
        Points = item.Points,
        EstimatedHours = item.EstimatedHours,
        WasCompleted = item.CompletedAt.HasValue,
        WorkItemCompletedAt = item.CompletedAt,
        Outcome = outcome,
        DestinationSprintId = destinationSprintId,
        CapturedAt = DateTimeOffset.UtcNow
    };
}
