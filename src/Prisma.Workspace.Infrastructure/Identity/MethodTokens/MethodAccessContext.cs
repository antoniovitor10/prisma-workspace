using System.Collections.Frozen;
using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Application.Interfaces;

namespace Prisma.Workspace.Infrastructure.Identity.MethodTokens;

public sealed class MethodAccessContext : IMethodAccessContext, IMethodContextInitializer
{
    public MethodActorIdentity? Current { get; private set; }

    public void Initialize(MethodActorIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (Current is not null)
            throw new InvalidOperationException("A identidade da operação já foi estabelecida.");
        if (string.IsNullOrWhiteSpace(identity.ActorId) || string.IsNullOrWhiteSpace(identity.CorrelationId))
            throw new InvalidOperationException("Identidade ou correlação da operação ausente.");
        // O conjunto recebido não permanece mutável pelo chamador depois da inicialização.
        Current = identity with { PermissionCeiling = identity.PermissionCeiling.ToFrozenSet() };
    }
}
