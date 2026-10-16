using Prisma.Workspace.Application.Features.BoardMetrics;
using Prisma.Workspace.Application.Features.Company;

namespace Prisma.Workspace.Application.Interfaces;

/// <summary>
/// Lado de LEITURA da área Empresa (projeções otimizadas direto no EF).
/// Escritas continuam passando por entidades de domínio.
/// </summary>
public interface ICompanyQueries
{
    Task<IReadOnlyList<ProjectSummaryDto>> GetProjectsAsync(string actorId, CancellationToken cancellationToken = default);
}

/// <summary>Lado de leitura do dashboard do quadro (6 métricas fixas).</summary>
public interface IBoardMetricsQueries
{
    Task<BoardMetricsDto> GetAsync(Guid boardId, string actorId, CancellationToken cancellationToken = default);
}
