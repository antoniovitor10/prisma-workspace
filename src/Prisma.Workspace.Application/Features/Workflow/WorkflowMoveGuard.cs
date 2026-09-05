using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Exceptions;

namespace Prisma.Workspace.Application.Features.Workflow;

public static class WorkflowMoveGuard
{
    public static async Task EnsureAllowedAsync(
        WorkItem item,
        Stage destination,
        IWorkflowRepository workflow,
        CancellationToken ct)
    {
        if (destination.WorkflowStatusId.HasValue)
        {
            var destinationStatus = await workflow.GetStatusAsync(destination.WorkflowStatusId.Value, ct);
            DomainException.Garantir(destinationStatus?.IsActive == true,
                "O status de destino esta inativo no workflow.");
        }
        if (destination.WipLimit.HasValue)
        {
            var currentCount = await workflow.CountActiveItemsInStageAsync(destination.Id, item.Id, ct);
            DomainException.Garantir(currentCount < destination.WipLimit.Value,
                $"A coluna '{destination.Name}' atingiu o limite de WIP ({destination.WipLimit.Value}).");
        }

        if (item.WorkflowStatusId.HasValue
            && destination.WorkflowStatusId.HasValue
            && item.WorkflowStatusId != destination.WorkflowStatusId)
        {
            DomainException.Garantir(await workflow.IsTransitionAllowedAsync(
                    item.WorkflowStatusId.Value, destination.WorkflowStatusId.Value, ct),
                "Esta transicao de status nao e permitida pelo fluxo do projeto.");
        }
    }
}
