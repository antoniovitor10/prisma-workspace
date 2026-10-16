using Prisma.Workspace.Application.Features.MethodGit.Contracts;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Features.MethodSecurity;

public interface IMethodTokenService
{
    Task<IssuedMethodTokenDto> IssueAsync(Guid projectId, IssueMethodTokenRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MethodTokenDto>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);
    Task RevokeAsync(Guid projectId, Guid tokenId, CancellationToken cancellationToken = default);
    Task<MethodActorIdentity?> AuthenticateAsync(string token, string correlationId, CancellationToken cancellationToken = default);
}

/// <summary>Interseção de delegação e escopo, sem trocar o ator autenticado pelo emissor.</summary>
public interface IMethodPermissionEvaluator
{
    Task<bool> HasAsync(MethodActorIdentity actor, PlatformPermission permission, PermissionScope scope,
        Guid? scopeId, CancellationToken cancellationToken = default);
}
