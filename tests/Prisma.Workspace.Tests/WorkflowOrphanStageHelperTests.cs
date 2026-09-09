using Prisma.Workspace.Application.Features.Workflow;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Tests;

/// <summary>
/// Casos obrigatórios de RemapOrphanedStageStatuses / DeactivateUnlessBackingColumns
/// (herança de template sem derrubar colunas do quadro).
/// </summary>
public sealed class WorkflowOrphanStageHelperTests
{
    [Fact]
    public void DeactivateUnlessBackingColumns_ObsoleteWithoutColumns_IsDeactivated()
    {
        var status = WorkflowStatus.Create(Guid.NewGuid(), "Revisão", "#F59E0B", 2,
            StageCategory.InProgress, false, false);
        status.IsActive = true;

        WorkflowTemplateProjection.DeactivateUnlessBackingColumns(status);

        Assert.False(status.IsActive);
        Assert.Empty(status.Stages);
        Assert.Empty(status.WorkItems);
    }

    [Fact]
    public void RemapOrphanedStageStatuses_ObsoleteWithColumns_RemapsToActiveHomonym()
    {
        var project = Project.Criar("FLOW", "Fluxo", "owner", WorkNature.Project, WorkType.Development);
        var obsolete = WorkflowStatus.Create(project.Id, "Concluído", "#22C55E", 3,
            StageCategory.Done, false, true);
        obsolete.IsActive = false;
        var activeHomonym = WorkflowStatus.Create(project.Id, "Concluído", "#16A34A", 4,
            StageCategory.Done, false, true);
        activeHomonym.IsActive = true;

        var column = new Stage
        {
            Id = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            Name = "Concluído",
            WorkflowStatusId = obsolete.Id,
            Category = StageCategory.Done,
            CreatedAt = DateTimeOffset.UtcNow
        };
        obsolete.Stages.Add(column);
        project.WorkflowStatuses.Add(obsolete);
        project.WorkflowStatuses.Add(activeHomonym);

        WorkflowTemplateProjection.RemapOrphanedStageStatuses(project);

        Assert.Equal(activeHomonym.Id, column.WorkflowStatusId);
        Assert.False(obsolete.IsActive);
        Assert.True(activeHomonym.IsActive);
    }

    [Fact]
    public void RemapOrphanedStageStatuses_ObsoleteWithColumnsWithoutSubstitute_ReactivatesIntentionally()
    {
        // Reativação intencional: sem status ativo homônimo, o legado em uso
        // precisa voltar a IsActive=true — senão colunas/tarefas ficam apontando
        // para destino que o WorkflowMoveGuard rejeita.
        var project = Project.Criar("FLOW", "Fluxo", "owner", WorkNature.Project, WorkType.Development);
        var obsolete = WorkflowStatus.Create(project.Id, "Revisão", "#F59E0B", 2,
            StageCategory.InProgress, false, false);
        obsolete.IsActive = false;
        var otherActive = WorkflowStatus.Create(project.Id, "Em andamento", "#3B82F6", 1,
            StageCategory.InProgress, false, false);
        otherActive.IsActive = true;

        obsolete.Stages.Add(new Stage
        {
            Id = Guid.NewGuid(),
            BoardId = Guid.NewGuid(),
            Name = "Revisão",
            WorkflowStatusId = obsolete.Id,
            Category = StageCategory.InProgress,
            CreatedAt = DateTimeOffset.UtcNow
        });
        project.WorkflowStatuses.Add(obsolete);
        project.WorkflowStatuses.Add(otherActive);

        WorkflowTemplateProjection.RemapOrphanedStageStatuses(project);

        Assert.True(obsolete.IsActive,
            "Reativação intencional: status obsoleto COM colunas e SEM substituto homônimo deve ser reativado.");
        Assert.Equal(obsolete.Id, obsolete.Stages.Single().WorkflowStatusId);
    }
}
