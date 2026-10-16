using Prisma.Workspace.Application.Features.MethodGit.Contracts;

namespace Prisma.Workspace.Application.Interfaces;

/// <summary>Usado apenas pela autenticação/middleware; nunca vincular este modelo a uma entrada HTTP.</summary>
public interface IMethodContextInitializer
{
    void Initialize(MethodActorIdentity identity);
}

/// <summary>Carrega tenant/principal/correlação persistidos antes de o worker acessar dados filtrados.</summary>
public interface IMethodWorkerContextInitializer
{
    Task InitializeAsync(Guid integrationId, Guid? deliveryId, string correlationId, CancellationToken cancellationToken = default);
}
