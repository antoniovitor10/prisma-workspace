using Detran.Kanban.Application.Features.TimeEntries.Dtos;
using MediatR;

namespace Detran.Kanban.Application.Features.TimeEntries.Commands;

/// <summary>Inicia o cronômetro do usuário em uma tarefa.</summary>
public record StartTimerCommand(Guid WorkItemId, string UserId, string? Note) : IRequest<TimeEntryDto>;
