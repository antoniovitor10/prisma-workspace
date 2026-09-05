using Detran.Kanban.Application.Interfaces;
using MediatR;

namespace Detran.Kanban.Application.Features.Me.Commands;

public class UpdateMyTaskPrioritiesCommandHandler : IRequestHandler<UpdateMyTaskPrioritiesCommand>
{
    private readonly IWorkItemRepository _workItems;

    public UpdateMyTaskPrioritiesCommandHandler(IWorkItemRepository workItems)
    {
        _workItems = workItems;
    }

    public Task Handle(UpdateMyTaskPrioritiesCommand request, CancellationToken cancellationToken)
        => _workItems.UpdatePersonalPrioritiesAsync(request.UserId, request.WorkItemIds, cancellationToken);
}
