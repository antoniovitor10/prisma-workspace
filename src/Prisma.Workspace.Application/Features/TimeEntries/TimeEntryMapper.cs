using Prisma.Workspace.Application.Features.TimeEntries.Dtos;
using Prisma.Workspace.Domain.Entities;

namespace Prisma.Workspace.Application.Features.TimeEntries;

/// <summary>
/// Conversão de <see cref="TimeEntry"/> para DTO. Para cronômetro aberto,
/// a duração devolvida é o decorrido até agora (o contrato da API sempre foi esse).
/// </summary>
public static class TimeEntryMapper
{
    public static TimeEntryDto ToDto(this TimeEntry entry, string? displayName = null)
    {
        var endedAt = entry.EndedAt ?? DateTimeOffset.UtcNow;
        return new TimeEntryDto
        {
            Id = entry.Id,
            WorkItemId = entry.WorkItemId,
            UserId = entry.UserId,
            DisplayName = displayName,
            StartedAt = entry.StartedAt,
            EndedAt = entry.EndedAt,
            Note = entry.Note,
            CreatedAt = entry.CreatedAt,
            DurationSeconds = Math.Max(0, (int)(endedAt - entry.StartedAt).TotalSeconds)
        };
    }
}
