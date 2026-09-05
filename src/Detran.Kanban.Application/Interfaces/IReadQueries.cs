using Detran.Kanban.Application.Features.BoardMetrics;
using Detran.Kanban.Application.Features.Company;

namespace Detran.Kanban.Application.Interfaces;

/// <summary>
/// Lado de LEITURA da área Empresa (projeções otimizadas direto no EF).
/// Escritas continuam passando por entidades de domínio.
/// </summary>
public interface ICompanyQueries
{
    Task<IReadOnlyList<ProjectSummaryDto>> GetProjectsAsync(CancellationToken cancellationToken = default);
}

/// <summary>Lado de leitura do dashboard do quadro (6 métricas fixas).</summary>
public interface IBoardMetricsQueries
{
    Task<BoardMetricsDto> GetAsync(Guid boardId, CancellationToken cancellationToken = default);
}
