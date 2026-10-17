using Prisma.Workspace.Application.Features.MethodGit.Contracts;

namespace Prisma.Workspace.Application.Interfaces;

/// <summary>
/// Única unidade de movimento: coluna, autorização, StageHistory, TaskEvent, fato e entrega
/// são persistidos na mesma transação; notificação externa só ocorre após commit.
/// Implementação da Forja/F1, consumida pelo webhook da Ponte e pelos handlers existentes.
/// </summary>
public interface IMethodMoveService
{
    Task<MethodMoveResult> MoveAsync(MethodMoveRequest request, CancellationToken cancellationToken = default);
}
