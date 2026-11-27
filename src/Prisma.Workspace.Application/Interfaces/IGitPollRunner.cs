using Prisma.Workspace.Application.Features.MethodGit.Contracts;

namespace Prisma.Workspace.Application.Interfaces;

/// <summary>
/// Executa a consulta ao GitHub de um repositório no modo consulta (HU-0002, T-0008), com a identidade de sistema
/// da integração e o canal "consulta". A consulta só lê o GitHub e enfileira reavaliações na mesma fila do webhook (D1).
/// </summary>
public interface IGitPollRunner
{
    /// <summary>"Consultar agora": no máximo uma por minuto por integração; recusa com 429.</summary>
    Task<GitPollResultDto> PollNowAsync(Guid integrationId, CancellationToken cancellationToken = default);
}
