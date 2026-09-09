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

    /// <summary>
    /// Estado funcional da sprint, calculado a partir das datas (D84 / SPEC-S-003 v3).
    ///
    /// Encerramento e cancelamento continuam sendo atos explícitos, registrados em
    /// <see cref="CompletedAt"/> e <see cref="CancelledAt"/>; o que deixa de existir é a
    /// transição manual para <c>Ativa</c>. Uma sprint cuja data final já passou é
    /// <c>Closed</c> mesmo sem encerramento explícito — era isso que permitia planejar
    /// tarefas numa sprint de janeiro já vencida.
    ///
    /// A coluna <see cref="Status"/> permanece na tabela por compatibilidade, mas NÃO é a
    /// fonte funcional: leia sempre por aqui. Removê-la exige migration própria.
    /// </summary>
    public SprintStatus StatusEm(DateOnly hoje)
    {
        if (CancelledAt.HasValue) return SprintStatus.Cancelled;
        if (CompletedAt.HasValue) return SprintStatus.Closed;
        if (hoje < StartDate) return SprintStatus.Planned;
        if (hoje > EndDate) return SprintStatus.Closed;
        return SprintStatus.Active;
    }

    /// <summary>Sprint terminal não recebe tarefa nova nem é editada.</summary>
    public bool EstaEncerradaEm(DateOnly hoje)
        => StatusEm(hoje) is SprintStatus.Closed or SprintStatus.Cancelled;

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
        DomainException.Garantir(!CompletedAt.HasValue && !CancelledAt.HasValue,
            "Sprint concluída ou cancelada não pode ser editada.");
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

    /// <summary>
    /// Encerra ou cancela a sprint. Não existe mais transição manual para <c>Ativa</c>:
    /// isso passou a ser derivado das datas (D84). Também não existe mais a regra de uma
    /// única sprint ativa por projeto — períodos sobrepostos são permitidos.
    /// </summary>
    public void Encerrar(SprintStatus nextStatus, DateOnly hoje)
    {
        DomainException.Garantir(nextStatus is SprintStatus.Closed or SprintStatus.Cancelled,
            "Só é possível encerrar ou cancelar a sprint; o início passou a ser determinado pelas datas.");
        // Sprint vencida por data PRECISA poder ser encerrada explicitamente: é o
        // encerramento que captura o snapshot e dá destino às tarefas abertas. O que
        // bloqueia é já ter sido encerrada ou cancelada de fato.
        DomainException.Garantir(!CancelledAt.HasValue && !CompletedAt.HasValue,
            "Sprint já encerrada ou cancelada não pode mudar de estado.");

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
