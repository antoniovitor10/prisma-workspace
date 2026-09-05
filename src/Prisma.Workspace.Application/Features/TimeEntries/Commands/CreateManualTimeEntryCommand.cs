using Prisma.Workspace.Application.Features.TimeEntries.Dtos;
using MediatR;

namespace Prisma.Workspace.Application.Features.TimeEntries.Commands;

/// <summary>Lançamento manual de horas (início e fim informados).</summary>
public record CreateManualTimeEntryCommand(
    Guid WorkItemId,
    string UserId,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    string? Note) : IRequest<TimeEntryDto>;
