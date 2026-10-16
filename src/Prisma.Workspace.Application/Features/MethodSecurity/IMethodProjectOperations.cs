using Prisma.Workspace.Application.Features.MethodGit.Contracts;

namespace Prisma.Workspace.Application.Features.MethodSecurity;

public interface IMethodProjectOperations
{
    Task<MethodIdentityDto> IdentityAsync(Guid? projectId, CancellationToken cancellationToken = default);
    Task<MethodProjectSnapshot?> SettingsAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task<MethodProjectSnapshot> ProvisionAsync(Guid projectId, ProvisionMethodProjectRequest request, CancellationToken cancellationToken = default);
    Task<MethodProjectSnapshot> UpdateAsync(Guid projectId, UpdateMethodProjectRequest request, CancellationToken cancellationToken = default);
    Task<MethodRulesDto> RulesAsync(Guid projectId, CancellationToken cancellationToken = default);
}
