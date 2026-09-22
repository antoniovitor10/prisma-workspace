using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Prisma.Workspace.Application.Features.Boards;

namespace Prisma.Workspace.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workitems/{workItemId:guid}/transfer")]
public sealed class BoardTransfersController(IMediator mediator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Transfer(Guid workItemId, TransferBoardRequest request, CancellationToken ct)
    {
        await mediator.Send(new TransferBoardWorkItemCommand(workItemId, request.DestinationBoardId,
            request.DestinationStageId, User.FindFirstValue(ClaimTypes.NameIdentifier)!), ct);
        return NoContent();
    }
}
public record TransferBoardRequest(Guid DestinationBoardId, Guid DestinationStageId);
