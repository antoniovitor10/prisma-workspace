using Prisma.Workspace.Application.Features.MeTime.Dtos;
using MediatR;

namespace Prisma.Workspace.Application.Features.MeTime.Queries;

/// <summary>Justificativas do usuário em um dia.</summary>
public record GetDayJustificationsQuery(string UserId, DateOnly Date) : IRequest<IReadOnlyList<DayJustificationDto>>;
