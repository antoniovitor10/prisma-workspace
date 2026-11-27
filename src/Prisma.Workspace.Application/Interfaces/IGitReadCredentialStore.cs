using Prisma.Workspace.Application.Features.MethodGit.Contracts;

namespace Prisma.Workspace.Application.Interfaces;

/// <summary>
/// Tokens de leitura por organização GitHub no projeto (HU-0002, D2): o valor é cifrado ao gravar
/// (ASP.NET Data Protection) e só o provedor o decifra. Nenhum método devolve o valor.
/// </summary>
public interface IGitReadCredentialStore
{
    Task<IReadOnlyList<GitReadCredentialDto>> ListAsync(Guid projectId, CancellationToken cancellationToken = default);
    /// <summary>Cria ou troca o token ativo do dono GitHub; repositórios do mesmo dono passam a usar o token ativo.</summary>
    Task<GitReadCredentialDto> SaveAsync(Guid projectId, string gitHubOwner, string token, string actorUserId, CancellationToken cancellationToken = default);
    Task<GitReadCredentialDto> RevokeAsync(Guid projectId, Guid credentialId, string actorUserId, CancellationToken cancellationToken = default);
    Task<Guid?> GetActiveIdAsync(Guid projectId, string gitHubOwner, CancellationToken cancellationToken = default);
}
