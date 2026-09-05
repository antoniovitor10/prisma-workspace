using Prisma.Workspace.Application.Features.TimeEntries.Dtos;
using MediatR;

namespace Prisma.Workspace.Application.Features.TimeEntries.Queries;

/// <summary>Cronômetro aberto do usuário (nulo se não houver).</summary>
public record GetRunningTimerQuery(string UserId) : IRequest<TimeEntryDto?>;
