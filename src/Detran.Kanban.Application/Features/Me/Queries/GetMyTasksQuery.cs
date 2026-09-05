using Detran.Kanban.Application.Features.Me.Dtos;
using MediatR;

namespace Detran.Kanban.Application.Features.Me.Queries;

/// <summary>
/// Fila "Tarefas para mim": tarefas onde o usuário é responsável,
/// ordenadas pela prioridade pessoal (sem prioridade vai para o fim).
/// </summary>
public record GetMyTasksQuery(string UserId) : IRequest<IReadOnlyList<MeTaskDto>>;
