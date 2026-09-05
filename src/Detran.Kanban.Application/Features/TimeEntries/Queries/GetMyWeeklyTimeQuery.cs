using Detran.Kanban.Application.Features.TimeEntries.Dtos;
using MediatR;

namespace Detran.Kanban.Application.Features.TimeEntries.Queries;

/// <summary>
/// Visão semanal (seg→dom) do tempo do usuário. WeekStart opcional
/// (yyyy-MM-dd); sem ele, usa a semana corrente no fuso de São Paulo.
/// </summary>
public record GetMyWeeklyTimeQuery(string UserId, DateOnly? WeekStart) : IRequest<WeeklyTimeDto>;
