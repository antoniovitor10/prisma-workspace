namespace Prisma.Workspace.Domain.Entities;

/// <summary>
/// Projeção de um WorkItem em um quadro do mesmo projeto.
/// A tarefa permanece canônica; cada linha representa posição/coluna em um quadro.
/// </summary>
public class WorkItemBoardPlacement
{
    public Guid Id { get; set; }
    public Guid WorkItemId { get; set; }
    public Guid BoardId { get; set; }
    public Guid? StageId { get; set; }
    public double Position { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public WorkItem WorkItem { get; set; } = null!;
    public Board Board { get; set; } = null!;
    public Stage? Stage { get; set; }
}
