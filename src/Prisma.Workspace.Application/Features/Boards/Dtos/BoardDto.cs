namespace Prisma.Workspace.Application.Features.Boards.Dtos;

/// <summary>
/// DTO de retorno de um Board.
/// </summary>
public class BoardDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string OwnerId { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Guid? TeamId { get; set; }
    public string? CardSettingsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
