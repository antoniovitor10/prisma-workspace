using Prisma.Workspace.Application.Features.MethodGit.Contracts;

namespace Prisma.Workspace.Application.Features.MethodGit.Repositories;

/// <summary>Configuração explícita por admin da UI; não concede autorização nem indexa tasks legadas.</summary>
public interface IMethodProjectProvisioner
{
    Task<MethodProjectSnapshot> ProvisionAsync(Guid projectId, ProvisionMethodProjectRequest request,
        string actorId, CancellationToken cancellationToken = default);
    Task<MethodProjectSnapshot> UpdateSettingsAsync(Guid projectId, UpdateMethodProjectRequest request,
        string actorId, CancellationToken cancellationToken = default);
}
