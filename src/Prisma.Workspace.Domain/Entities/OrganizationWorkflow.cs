using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Domain.Interfaces;

namespace Prisma.Workspace.Domain.Entities;

public sealed class OrganizationWorkflowTemplate : IOrganizationOwned
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public long Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public Organization Organization { get; set; } = null!;
    public ICollection<OrganizationWorkflowStatus> Statuses { get; set; } = new List<OrganizationWorkflowStatus>();
    public ICollection<OrganizationWorkflowTransition> Transitions { get; set; } = new List<OrganizationWorkflowTransition>();
    public ICollection<Project> Projects { get; set; } = new List<Project>();

    public static OrganizationWorkflowTemplate Create(Guid organizationId, string name, bool isDefault)
    {
        DomainException.Garantir(organizationId != Guid.Empty, "Organizacao obrigatoria.");
        DomainException.Garantir(!string.IsNullOrWhiteSpace(name), "Nome do template obrigatorio.");
        var now = DateTimeOffset.UtcNow;
        return new OrganizationWorkflowTemplate
        {
            Id = Guid.NewGuid(), OrganizationId = organizationId, Name = name.Trim(),
            IsDefault = isDefault, CreatedAt = now, UpdatedAt = now
        };
    }

    public void Update(string name, bool isDefault)
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(name), "Nome do template obrigatorio.");
        Name = name.Trim();
        IsDefault = isDefault;
        Version++;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public sealed class OrganizationWorkflowStatus
{
    public Guid Id { get; set; }
    public Guid TemplateId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#64748B";
    public double Position { get; set; }
    public StageCategory Category { get; set; } = StageCategory.InProgress;
    public bool IsInitial { get; set; }
    public bool IsFinal { get; set; }
    public bool IsActive { get; set; } = true;
    public byte[] RowVersion { get; set; } = [];

    public OrganizationWorkflowTemplate Template { get; set; } = null!;
    public ICollection<WorkflowStatus> ProjectStatuses { get; set; } = new List<WorkflowStatus>();

    public static OrganizationWorkflowStatus Create(
        Guid templateId, string key, string name, string color, double position,
        StageCategory category, bool isInitial, bool isFinal, bool isActive = true)
    {
        DomainException.Garantir(templateId != Guid.Empty, "Template obrigatorio.");
        var normalizedKey = key.Trim().ToUpperInvariant();
        DomainException.Garantir(normalizedKey.Length > 0, "Chave do status obrigatoria.");
        DomainException.Garantir(normalizedKey.All(c => char.IsLetterOrDigit(c) || c is '_' or '-'),
            "Chave do status invalida.");
        var local = WorkflowStatus.Create(Guid.NewGuid(), name, color, position, category, isInitial, isFinal);
        return new OrganizationWorkflowStatus
        {
            Id = Guid.NewGuid(), TemplateId = templateId, Key = normalizedKey,
            Name = local.Name, Color = local.Color, Position = local.Position, Category = category,
            IsInitial = isInitial, IsFinal = isFinal, IsActive = isActive
        };
    }
}

public sealed class OrganizationWorkflowTransition
{
    public Guid TemplateId { get; set; }
    public Guid SourceStatusId { get; set; }
    public Guid TargetStatusId { get; set; }
    public OrganizationWorkflowTemplate Template { get; set; } = null!;
    public OrganizationWorkflowStatus SourceStatus { get; set; } = null!;
    public OrganizationWorkflowStatus TargetStatus { get; set; } = null!;

    public static OrganizationWorkflowTransition Create(Guid templateId, Guid sourceId, Guid targetId)
    {
        DomainException.Garantir(templateId != Guid.Empty, "Template obrigatorio.");
        DomainException.Garantir(sourceId != targetId, "A origem e o destino precisam ser diferentes.");
        return new OrganizationWorkflowTransition
        {
            TemplateId = templateId, SourceStatusId = sourceId, TargetStatusId = targetId
        };
    }
}
