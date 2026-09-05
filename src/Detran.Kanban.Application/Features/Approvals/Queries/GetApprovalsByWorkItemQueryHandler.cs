using Detran.Kanban.Application.Features.Approvals.Dtos;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Enums;
using MediatR;

namespace Detran.Kanban.Application.Features.Approvals.Queries;

public class GetApprovalsByWorkItemQueryHandler
    : IRequestHandler<GetApprovalsByWorkItemQuery, IReadOnlyList<ApprovalDto>>
{
    private readonly IApprovalRepository _approvals;
    private readonly IUserDirectory _users;
    private readonly IWorkItemAccessService _access;

    public GetApprovalsByWorkItemQueryHandler(
        IApprovalRepository approvals,
        IUserDirectory users,
        IWorkItemAccessService access)
    {
        _approvals = approvals;
        _users = users;
        _access = access;
    }

    public async Task<IReadOnlyList<ApprovalDto>> Handle(
        GetApprovalsByWorkItemQuery request,
        CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(request.WorkItemId, request.ActorId,
            PlatformPermission.View, ProjectRole.Viewer, cancellationToken);
        var items = await _approvals.GetByWorkItemAsync(request.WorkItemId, cancellationToken);
        return await ApprovalDto.FromListAsync(items, _users, cancellationToken);
    }
}
