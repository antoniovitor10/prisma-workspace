using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Domain.Interfaces;

namespace Prisma.Workspace.Domain.Entities;

/// <summary>Projeto de TI que agrega times, quadros, backlog e sprints.</summary>
public class Project : IOrganizationOwned
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid? ClientId { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public DateOnly? StartDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public ProjectStatus Status { get; set; } = ProjectStatus.Active;
    public ProjectMethodology Methodology { get; set; } = ProjectMethodology.Kanban;
    public WorkNature Nature { get; set; } = WorkNature.Unclassified;
    public WorkType WorkType { get; set; } = WorkType.Unclassified;
    public WorkflowInheritanceMode WorkflowInheritanceMode { get; set; } = WorkflowInheritanceMode.Custom;
    public Guid? WorkflowTemplateId { get; set; }
    public long? WorkflowTemplateVersion { get; set; }
    public bool IsArchived { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public string? SettingsJson { get; set; }
    /// <summary>Quadro padrão do projeto para criação sem seleção explícita.</summary>
    public Guid? DefaultBoardId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Client? Client { get; set; }
    public Board? DefaultBoard { get; set; }
    public OrganizationWorkflowTemplate? WorkflowTemplate { get; set; }
    public ICollection<Board> Boards { get; set; } = new List<Board>();
    /// <summary>Colunas do fluxo do projeto (Kanban sem quadro obrigatório).</summary>
    public ICollection<Stage> Stages { get; set; } = new List<Stage>();
    public ICollection<ProjectMember> Members { get; set; } = new List<ProjectMember>();
    public ICollection<ProjectTeam> Teams { get; set; } = new List<ProjectTeam>();
    public ICollection<Sprint> Sprints { get; set; } = new List<Sprint>();
    public ICollection<ProjectTag> Tags { get; set; } = new List<ProjectTag>();
    public ICollection<ProjectCustomFieldDefinition> CustomFields { get; set; } = new List<ProjectCustomFieldDefinition>();
    public ICollection<ProjectEvent> Events { get; set; } = new List<ProjectEvent>();
    public ICollection<WorkflowStatus> WorkflowStatuses { get; set; } = new List<WorkflowStatus>();

    public static Project Criar(
        string chave,
        string nome,
        string ownerId,
        WorkNature nature,
        WorkType workType,
        string? descricao = null,
        ProjectMethodology methodology = ProjectMethodology.Kanban,
        DateOnly? startDate = null,
        DateOnly? dueDate = null)
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(chave), "Chave do projeto obrigatória.");
        DomainException.Garantir(!string.IsNullOrWhiteSpace(nome), "Nome do projeto obrigatório.");
        DomainException.Garantir(!string.IsNullOrWhiteSpace(ownerId), "Responsável do projeto obrigatório.");
        ValidateClassification(nature, workType);

        DomainException.Garantir(!startDate.HasValue || !dueDate.HasValue || dueDate >= startDate,
            "O prazo do projeto nao pode ser anterior a data de inicio.");

        var now = DateTimeOffset.UtcNow;
        var project = new Project
        {
            Id = Guid.NewGuid(),
            Key = chave.Trim().ToUpperInvariant(),
            Name = nome.Trim(),
            Description = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim(),
            OwnerId = ownerId,
            StartDate = startDate,
            DueDate = dueDate,
            Methodology = methodology,
            Nature = nature,
            WorkType = workType,
            Status = ProjectStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        project.Members.Add(new ProjectMember
        {
            ProjectId = project.Id,
            UserId = ownerId,
            Role = ProjectRole.ProjectAdmin,
            JoinedAt = project.CreatedAt
        });
        return project;
    }

    public void Update(
        string name,
        string? description,
        string ownerId,
        DateOnly? startDate,
        DateOnly? dueDate,
        ProjectStatus status,
        ProjectMethodology methodology,
        WorkNature nature,
        WorkType workType,
        string? settingsJson)
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(name), "Nome do projeto obrigatorio.");
        DomainException.Garantir(!string.IsNullOrWhiteSpace(ownerId), "Responsavel do projeto obrigatorio.");
        DomainException.Garantir(!startDate.HasValue || !dueDate.HasValue || dueDate >= startDate,
            "O prazo do projeto nao pode ser anterior a data de inicio.");
        ValidateClassification(nature, workType);

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        OwnerId = ownerId;
        StartDate = startDate;
        DueDate = dueDate;
        Status = status;
        Methodology = methodology;
        Nature = nature;
        WorkType = workType;
        SettingsJson = string.IsNullOrWhiteSpace(settingsJson) ? null : settingsJson;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static void ValidateClassification(WorkNature nature, WorkType workType)
    {
        DomainException.Garantir(Enum.IsDefined(nature) && nature != WorkNature.Unclassified,
            "Natureza do projeto obrigatoria.");
        DomainException.Garantir(Enum.IsDefined(workType) && workType != WorkType.Unclassified,
            "Tipo de trabalho obrigatorio.");
    }

    public void Archive()
    {
        IsArchived = true;
        ArchivedAt = DateTimeOffset.UtcNow;
        UpdatedAt = ArchivedAt.Value;
    }

    public void Reactivate()
    {
        IsArchived = false;
        ArchivedAt = null;
        if (Status is ProjectStatus.Completed or ProjectStatus.Cancelled)
            Status = ProjectStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public class ProjectMember
{
    public Guid ProjectId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public ProjectRole Role { get; set; }
    public DateTimeOffset JoinedAt { get; set; }
    public Project Project { get; set; } = null!;
}

public class ProjectTeam
{
    public Guid ProjectId { get; set; }
    public Guid TeamId { get; set; }
    public DateTimeOffset AddedAt { get; set; }
    public Project Project { get; set; } = null!;
    public Team Team { get; set; } = null!;
}
