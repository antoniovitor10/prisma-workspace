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
        // D89/D65: a categoria explícita da coluna governa o estado. Fluxos de projeto são legado.
        if (destination.BoardId.HasValue) return;
        if (destination.WorkflowStatusId.HasValue)
        {
            var destinationStatus = await workflow.GetStatusAsync(destination.WorkflowStatusId.Value, ct);
            DomainException.Garantir(destinationStatus?.IsActive == true,
                "O status de destino esta inativo no workflow.");
        }
        // O limite de WIP foi removido do produto pela D83. Nenhuma regra bloqueia ou
        // avisa por quantidade de cartões na coluna.

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
