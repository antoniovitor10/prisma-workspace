using Detran.Kanban.Application.Features.MeTime.Dtos;
using MediatR;

namespace Detran.Kanban.Application.Features.MeTime.Queries;

/// <summary>Justificativas do usuário em um dia.</summary>
public record GetDayJustificationsQuery(string UserId, DateOnly Date) : IRequest<IReadOnlyList<DayJustificationDto>>;
