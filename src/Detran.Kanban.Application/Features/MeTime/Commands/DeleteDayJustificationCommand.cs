using MediatR;

namespace Detran.Kanban.Application.Features.MeTime.Commands;

/// <summary>Remove uma justificativa do próprio usuário.</summary>
public record DeleteDayJustificationCommand(Guid Id, string UserId) : IRequest;
