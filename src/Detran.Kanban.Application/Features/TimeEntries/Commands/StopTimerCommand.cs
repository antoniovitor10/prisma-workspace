using Detran.Kanban.Application.Features.TimeEntries.Dtos;
using MediatR;

namespace Detran.Kanban.Application.Features.TimeEntries.Commands;

/// <summary>
/// Encerra o cronômetro aberto do usuário. Se WorkItemId vier preenchido,
/// valida que o cronômetro pertence àquela tarefa.
/// </summary>
public record StopTimerCommand(Guid? WorkItemId, string UserId, string? Note) : IRequest<TimeEntryDto>;
