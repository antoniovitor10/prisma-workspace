using Prisma.Workspace.Application.Features.WorkItems.Dtos;

namespace Prisma.Workspace.Application.Interfaces;

/// <summary>
/// Lê, numa consulta agregada por quadro (sem N+1), o resumo Git dos cartões (HU-0002, RF1).
/// Respeita a organização atual; não chama o GitHub.
/// </summary>
public interface IWorkItemGitSummaryReader
{
    Task<IReadOnlyDictionary<Guid, WorkItemGitSummaryDto>> GetAsync(IReadOnlyCollection<Guid> workItemIds,
        CancellationToken cancellationToken = default);
}
