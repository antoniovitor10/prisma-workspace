using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using MediatR;

namespace Prisma.Workspace.Application.Features.BoardMetrics;

// DTOs do dashboard do quadro (6 métricas fixas).

public record StageCountDto(string Stage, int Count);
public record UserHoursDto(string User, double Hours);
public record BurndownPointDto(string Date, int Open);
public record TypeCountDto(string Type, string Color, int Count);

public class BoardMetricsDto
{
    public IReadOnlyList<StageCountDto> TasksByStage { get; init; } = new List<StageCountDto>();
    public int LateCount { get; init; }
    public int DeliveredThisWeek { get; init; }
    public IReadOnlyList<UserHoursDto> HoursByUser { get; init; } = new List<UserHoursDto>();
    public IReadOnlyList<BurndownPointDto> Burndown { get; init; } = new List<BurndownPointDto>();
    public IReadOnlyList<TypeCountDto> TasksByType { get; init; } = new List<TypeCountDto>();
}

/// <summary>Métricas do dashboard de um quadro.</summary>
public record GetBoardMetricsQuery(Guid BoardId, string ActorId) : IRequest<BoardMetricsDto>;

public class GetBoardMetricsQueryHandler : IRequestHandler<GetBoardMetricsQuery, BoardMetricsDto>
{
    private readonly IBoardMetricsQueries _queries;
    private readonly IBoardAccessService _access;

    public GetBoardMetricsQueryHandler(IBoardMetricsQueries queries, IBoardAccessService access)
        => (_queries, _access) = (queries, access);

    public async Task<BoardMetricsDto> Handle(GetBoardMetricsQuery request, CancellationToken ct)
    {
        await _access.EnsureAsync(request.BoardId, request.ActorId,
            PlatformPermission.View, ProjectRole.Viewer, ct);
        return await _queries.GetAsync(request.BoardId, ct);
    }
}
