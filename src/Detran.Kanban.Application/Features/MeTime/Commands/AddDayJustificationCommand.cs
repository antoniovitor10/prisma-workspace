using Detran.Kanban.Application.Features.MeTime.Dtos;
using MediatR;

namespace Detran.Kanban.Application.Features.MeTime.Commands;

/// <summary>Cria uma justificativa do dia (férias, atestado, feriado...).</summary>
public record AddDayJustificationCommand(
    string UserId,
    DateOnly Date,
    string Reason,
    decimal Hours) : IRequest<DayJustificationDto>;
