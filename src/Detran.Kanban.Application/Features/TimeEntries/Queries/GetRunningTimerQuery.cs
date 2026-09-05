using Detran.Kanban.Application.Features.TimeEntries.Dtos;
using MediatR;

namespace Detran.Kanban.Application.Features.TimeEntries.Queries;

/// <summary>Cronômetro aberto do usuário (nulo se não houver).</summary>
public record GetRunningTimerQuery(string UserId) : IRequest<TimeEntryDto?>;
