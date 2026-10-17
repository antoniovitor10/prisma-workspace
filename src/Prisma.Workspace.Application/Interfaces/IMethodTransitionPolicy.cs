using Prisma.Workspace.Application.Features.MethodGit.Contracts;

namespace Prisma.Workspace.Application.Interfaces;

public interface IMethodTransitionPolicy
{
    // O contexto de identidade vem do servidor; estado e permissões são revalidados dentro da transação.
    Task<MethodTransitionDecision> EvaluateAsync(MethodMutation mutation, CancellationToken cancellationToken = default);
    Task EnsureAllowedAsync(MethodMutation mutation, CancellationToken cancellationToken = default);
}
