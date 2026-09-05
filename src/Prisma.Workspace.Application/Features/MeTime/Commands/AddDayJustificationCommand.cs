using Prisma.Workspace.Application.Features.MeTime.Dtos;
using MediatR;

namespace Prisma.Workspace.Application.Features.MeTime.Commands;

/// <summary>Cria uma justificativa do dia (férias, atestado, feriado...).</summary>
public record AddDayJustificationCommand(
    string UserId,
    DateOnly Date,
    string Reason,
    decimal Hours) : IRequest<DayJustificationDto>;
