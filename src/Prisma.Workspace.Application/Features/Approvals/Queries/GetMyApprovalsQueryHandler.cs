using Prisma.Workspace.Application.Features.Approvals.Dtos;
using Prisma.Workspace.Application.Interfaces;
using MediatR;

namespace Prisma.Workspace.Application.Features.Approvals.Queries;

public class GetMyApprovalsQueryHandler
    : IRequestHandler<GetMyApprovalsQuery, IReadOnlyList<ApprovalDto>>
{
    private const int Limite = 100;

    private readonly IApprovalRepository _approvals;
    private readonly IUserDirectory _users;

    public GetMyApprovalsQueryHandler(IApprovalRepository approvals, IUserDirectory users)
    {
        _approvals = approvals;
        _users = users;
    }

    public async Task<IReadOnlyList<ApprovalDto>> Handle(
        GetMyApprovalsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await _approvals.GetByApproverAsync(request.ApproverId, Limite, cancellationToken);
        return await ApprovalDto.FromListAsync(items, _users, cancellationToken);
    }
}
