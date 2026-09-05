using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Domain.Entities;

/// <summary>
/// Item de trabalho (tarefa/cartão) do quadro Kanban.
/// Usa "WorkItem" em vez de "Task" para não colidir com <see cref="System.Threading.Tasks.Task"/>.
/// </summary>
public class WorkItem
{
    /// <summary>Identificador único do item.</summary>
    public Guid Id { get; set; }

    /// <summary>Numero sequencial estavel usado na referencia humana do item.</summary>
    public long Number { get; set; }

    /// <summary>Identificador do quadro ao qual o item pertence.</summary>
    public Guid BoardId { get; set; }

    /// <summary>Sprint atual. Nulo significa product backlog.</summary>
    public Guid? SprintId { get; set; }

    /// <summary>Identificador da etapa atual. Nulo = sem etapa (backlog).</summary>
    public Guid? StageId { get; set; }

    /// <summary>Status de negócio atual, independente da visão em que a tarefa aparece.</summary>
    public Guid? WorkflowStatusId { get; set; }

    /// <summary>Identificador do item pai (auto-referência para subtarefas).</summary>
    public Guid? ParentId { get; set; }

    /// <summary>Equipe responsavel pelo item; quando nula, herda a equipe do quadro.</summary>
    public Guid? TeamId { get; set; }

    /// <summary>Título do item.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Subtítulo opcional.</summary>
    public string? Subtitle { get; set; }

    /// <summary>Descrição detalhada do item.</summary>
    public string? Description { get; set; }

    /// <summary>Nível de prioridade.</summary>
    public Priority Priority { get; set; } = Priority.Low;

    public WorkItemKind Kind { get; set; } = WorkItemKind.Task;

    /// <summary>Canal pelo qual a demanda entrou na plataforma.</summary>
    public WorkItemOrigin Origin { get; set; } = WorkItemOrigin.Internal;

    /// <summary>Responsavel principal. Participantes continuam em Assignees.</summary>
    public string? ResponsibleId { get; set; }

    public string? RequesterId { get; set; }
    public string? RequesterName { get; set; }
    public string? RequesterEmail { get; set; }

    /// <summary>Horas estimadas para conclusão.</summary>
    public decimal? EstimatedHours { get; set; }

    /// <summary>Trabalho restante informado pelo time para capacidade e burndown.</summary>
    public decimal? RemainingHours { get; set; }

    /// <summary>Data de vencimento.</summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>Data planejada ou efetiva de inicio.</summary>
    public DateOnly? StartDate { get; set; }

    /// <summary>Criterios que precisam ser atendidos para considerar o item concluido.</summary>
    public string? AcceptanceCriteria { get; set; }

    /// <summary>Posição ordinal dentro da etapa.</summary>
    public double Position { get; set; }

    /// <summary>Ordem global no backlog do projeto.</summary>
    public decimal BacklogRank { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public bool IsArchived { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }

    /// <summary>Token de concorrência otimista gerado pelo SQL Server.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    /// <summary>Tipo de tarefa (chip colorido). Nulo = sem tipo.</summary>
    public Guid? TaskTypeId { get; set; }

    /// <summary>Pontos (story points), opcional.</summary>
    public int? Points { get; set; }

    /// <summary>Identificador do usuário que criou o item.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Data/hora de criação.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Data/hora da última atualização.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    // ── Navegação ──────────────────────────────────────────────

    /// <summary>
    /// Quadro "home" legado/compatível. A projeção canônica multi-quadro vive em
    /// <see cref="BoardPlacements"/>; este campo espelha a colocação principal.
    /// </summary>
    public Board Board { get; set; } = null!;

    /// <summary>Etapa atual do item (pode ser nula).</summary>
    public Stage? Stage { get; set; }

    /// <summary>Projeções do item em um ou mais quadros do projeto.</summary>
    public ICollection<WorkItemBoardPlacement> BoardPlacements { get; set; } = new List<WorkItemBoardPlacement>();

    public WorkflowStatus? WorkflowStatus { get; set; }

    public Sprint? Sprint { get; set; }

    public Team? Team { get; set; }

    /// <summary>Tipo de tarefa (pode ser nulo).</summary>
    public TaskType? TaskType { get; set; }

    /// <summary>Tags vinculadas a este item.</summary>
    public ICollection<WorkItemTag> WorkItemTags { get; set; }

    /// <summary>Itens de checklist deste item.</summary>
    public ICollection<ChecklistItem> ChecklistItems { get; set; }

    /// <summary>Item pai (para subtarefas).</summary>
    public WorkItem? Parent { get; set; }

    /// <summary>Subtarefas deste item.</summary>
    public ICollection<WorkItem> SubItems { get; set; }

    /// <summary>Responsáveis pelo item.</summary>
    public ICollection<WorkItemAssignee> Assignees { get; set; }

    /// <summary>Anexos vinculados ao item.</summary>
    public ICollection<Attachment> Attachments { get; set; }

    /// <summary>Lançamentos de horas do item.</summary>
    public ICollection<TimeEntry> TimeEntries { get; set; }

    /// <summary>Histórico de movimentações entre etapas.</summary>
    public ICollection<StageHistory> StageHistories { get; set; }

    public ICollection<Comment> Comments { get; set; }

    public ICollection<WorkItemFollower> Followers { get; set; }

    public ICollection<WorkItemLink> OutgoingLinks { get; set; }

    public ICollection<WorkItemLink> IncomingLinks { get; set; }

    public ICollection<WorkItemCustomFieldValue> CustomFieldValues { get; set; }

    public ExternalRequest? ExternalRequest { get; set; }

    public WorkItem()
    {
        SubItems = new List<WorkItem>();
        Assignees = new List<WorkItemAssignee>();
        Attachments = new List<Attachment>();
        TimeEntries = new List<TimeEntry>();
        StageHistories = new List<StageHistory>();
        WorkItemTags = new List<WorkItemTag>();
        ChecklistItems = new List<ChecklistItem>();
        Comments = new List<Comment>();
        Followers = new List<WorkItemFollower>();
        OutgoingLinks = new List<WorkItemLink>();
        IncomingLinks = new List<WorkItemLink>();
        CustomFieldValues = new List<WorkItemCustomFieldValue>();
    }

    // ── Comportamento ──────────────────────────────────────────────────

    /// <summary>
    /// Define tipo, pontos e o conjunto EXATO de tags (substitui as atuais).
    /// Requer WorkItemTags carregado.
    /// </summary>
    public void DefinirTaxonomia(Guid? taskTypeId, int? points, IEnumerable<Guid> tagIds)
    {
        TaskTypeId = taskTypeId;
        Points = points;
        WorkItemTags.Clear();
        foreach (var tagId in tagIds.Distinct())
        {
            WorkItemTags.Add(new WorkItemTag { WorkItemId = Id, TagId = tagId });
        }
        Tocar();
    }

    /// <summary>Define a descrição (texto em branco vira nulo).</summary>
    public void DefinirDescricao(string? descricao)
    {
        Description = string.IsNullOrWhiteSpace(descricao) ? null : descricao;
        Tocar();
    }

    public void Archive()
    {
        IsArchived = true;
        ArchivedAt = DateTimeOffset.UtcNow;
        Tocar();
    }

    public void Reactivate()
    {
        IsArchived = false;
        ArchivedAt = null;
        Tocar();
    }

    /// <summary>Atualiza o carimbo de última modificação.</summary>
    public void Tocar() => UpdatedAt = DateTimeOffset.UtcNow;
}
