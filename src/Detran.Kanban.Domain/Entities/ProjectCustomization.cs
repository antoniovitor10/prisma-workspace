using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;

namespace Detran.Kanban.Domain.Entities;

public class ProjectTag
{
    public Guid ProjectId { get; set; }
    public Guid TagId { get; set; }
    public Project Project { get; set; } = null!;
    public Tag Tag { get; set; } = null!;
}

public class ProjectCustomFieldDefinition
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public CustomFieldType Type { get; set; }
    public bool IsRequired { get; set; }
    public string? OptionsJson { get; set; }
    public double Position { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public Project Project { get; set; } = null!;
    public ICollection<WorkItemCustomFieldValue> Values { get; set; } = new List<WorkItemCustomFieldValue>();

    public static ProjectCustomFieldDefinition Create(
        Guid projectId,
        string name,
        CustomFieldType type,
        bool isRequired,
        string? optionsJson,
        double position)
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(name), "Nome do campo personalizado obrigatorio.");
        return new ProjectCustomFieldDefinition
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Name = name.Trim(),
            Type = type,
            IsRequired = isRequired,
            OptionsJson = string.IsNullOrWhiteSpace(optionsJson) ? null : optionsJson,
            Position = position,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }
}

public class ProjectEvent
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? Payload { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Project Project { get; set; } = null!;

    public static ProjectEvent Register(Guid projectId, string actorId, string kind, string? payload = null)
        => new()
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            ActorId = actorId,
            Kind = kind,
            Payload = payload,
            CreatedAt = DateTimeOffset.UtcNow
        };
}
