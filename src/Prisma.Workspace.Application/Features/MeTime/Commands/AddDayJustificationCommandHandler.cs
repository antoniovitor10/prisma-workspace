using Prisma.Workspace.Application.Features.MeTime.Dtos;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using MediatR;

namespace Prisma.Workspace.Application.Features.MeTime.Commands;

public class AddDayJustificationCommandHandler
    : IRequestHandler<AddDayJustificationCommand, DayJustificationDto>
{
    private readonly IDayJustificationRepository _justifications;

    public AddDayJustificationCommandHandler(IDayJustificationRepository justifications)
    {
        _justifications = justifications;
    }

    public async Task<DayJustificationDto> Handle(
        AddDayJustificationCommand request,
        CancellationToken cancellationToken)
    {
        var entity = new DayJustification
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Date = request.Date,
            Reason = request.Reason.Trim(),
            Hours = request.Hours,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _justifications.AddAsync(entity, cancellationToken);
        return new DayJustificationDto { Id = entity.Id, Reason = entity.Reason, Hours = entity.Hours };
    }
}
