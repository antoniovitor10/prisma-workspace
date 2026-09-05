using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;

namespace Prisma.Workspace.Domain.Entities;

/// <summary>Status de negócio reutilizado pelas visões do projeto.</summary>
public class WorkflowStatus
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public Guid? OrganizationWorkflowStatusId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#64748B";
    public double Position { get; set; }
    public StageCategory Category { get; set; } = StageCategory.InProgress;
    public bool IsInitial { get; set; }
    public bool IsFinal { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Project Project { get; set; } = null!;
    public OrganizationWorkflowStatus? OrganizationWorkflowStatus { get; set; }
    public ICollection<Stage> Stages { get; set; } = new List<Stage>();
    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();
    public ICollection<WorkflowTransition> OutgoingTransitions { get; set; } = new List<WorkflowTransition>();
    public ICollection<WorkflowTransition> IncomingTransitions { get; set; } = new List<WorkflowTransition>();

    public static WorkflowStatus Create(
        Guid projectId,
        string name,
        string color,
        double position,
        StageCategory category,
        bool isInitial,
        bool isFinal)
    {
        var status = new WorkflowStatus
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        status.Update(name, color, position, category, isInitial, isFinal);
        return status;
    }

    public void Update(
        string name,
        string color,
        double position,
        StageCategory category,
        bool isInitial,
        bool isFinal)
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(name), "Nome do status obrigatorio.");
        DomainException.Garantir(IsHexColor(color), "A cor do status precisa usar o formato hexadecimal #RRGGBB.");
        DomainException.Garantir(position >= 0, "A posicao do status nao pode ser negativa.");

        Name = name.Trim();
        Color = color.Trim().ToUpperInvariant();
        Position = position;
        Category = category;
        IsInitial = isInitial;
        IsFinal = isFinal;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static bool IsHexColor(string? value)
        => value is { Length: 7 }
            && value[0] == '#'
            && value[1..].All(Uri.IsHexDigit);
}

/// <summary>Transição direcionada permitida entre dois status do mesmo projeto.</summary>
public class WorkflowTransition
{
    public Guid SourceStatusId { get; set; }
    public Guid TargetStatusId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public WorkflowStatus SourceStatus { get; set; } = null!;
    public WorkflowStatus TargetStatus { get; set; } = null!;

    public static WorkflowTransition Create(Guid sourceStatusId, Guid targetStatusId)
    {
        DomainException.Garantir(sourceStatusId != targetStatusId,
            "A origem e o destino da transicao precisam ser diferentes.");
        return new WorkflowTransition
        {
            SourceStatusId = sourceStatusId,
            TargetStatusId = targetStatusId,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}
