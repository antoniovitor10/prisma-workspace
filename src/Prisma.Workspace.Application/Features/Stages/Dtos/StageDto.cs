using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Features.Stages.Dtos;

/// <summary>
/// DTO de retorno para uma etapa (Stage) do fluxo do projeto.
/// </summary>
public class StageDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double Position { get; set; }
    public Guid? WorkflowStatusId { get; set; }
    /// <summary>
    /// Classificação funcional da coluna. Sem ela a interface não consegue mostrar nem
    /// escolher se a coluna representa trabalho aberto, em andamento ou concluído.
    /// </summary>
    public StageCategory Category { get; set; }
    public string? StatusName { get; set; }
    public string? StatusColor { get; set; }
    public bool IsInitial { get; set; }
    public bool IsFinal { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
