using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Domain.Interfaces;

namespace Prisma.Workspace.Domain.Entities;

public class SavedReport : IOrganizationOwned
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? ProjectId { get; set; }
    public string OwnerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public ReportDataSource Source { get; set; }
    public ReportVisualization Visualization { get; set; }
    public string DefinitionJson { get; set; } = "{}";
    public bool IsShared { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Project? Project { get; set; }

    public static SavedReport Create(
        Guid organizationId,
        Guid? projectId,
        string ownerId,
        string name,
        ReportDataSource source,
        ReportVisualization visualization,
        string definitionJson,
        bool isShared)
    {
        DomainException.Garantir(organizationId != Guid.Empty, "Organização obrigatória.");
        DomainException.Garantir(!string.IsNullOrWhiteSpace(ownerId), "Proprietário obrigatório.");
        var report = new SavedReport
        {
            Id = Guid.NewGuid(), OrganizationId = organizationId, ProjectId = projectId,
            OwnerId = ownerId, CreatedAt = DateTimeOffset.UtcNow
        };
        report.Update(name, source, visualization, definitionJson, isShared);
        return report;
    }

    public void Update(
        string name,
        ReportDataSource source,
        ReportVisualization visualization,
        string definitionJson,
        bool isShared)
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(name), "Nome do relatório obrigatório.");
        DomainException.Garantir(name.Trim().Length <= 200, "Nome do relatório muito longo.");
        Name = name.Trim();
        Source = source;
        Visualization = visualization;
        DefinitionJson = definitionJson;
        IsShared = isShared;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
