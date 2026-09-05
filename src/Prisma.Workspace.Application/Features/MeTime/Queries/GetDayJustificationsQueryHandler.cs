using Prisma.Workspace.Application.Features.MeTime.Dtos;
using Prisma.Workspace.Application.Interfaces;
using MediatR;

namespace Prisma.Workspace.Application.Features.MeTime.Queries;

public class GetDayJustificationsQueryHandler
    : IRequestHandler<GetDayJustificationsQuery, IReadOnlyList<DayJustificationDto>>
{
    private readonly IDayJustificationRepository _justifications;

    public GetDayJustificationsQueryHandler(IDayJustificationRepository justifications)
    {
        _justifications = justifications;
    }

    public async Task<IReadOnlyList<DayJustificationDto>> Handle(
        GetDayJustificationsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _justifications.GetByUserAndDateAsync(request.UserId, request.Date, cancellationToken);
        return items
            .Select(d => new DayJustificationDto { Id = d.Id, Reason = d.Reason, Hours = d.Hours })
            .ToList();
    }
}
