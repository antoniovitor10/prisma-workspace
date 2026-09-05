using Prisma.Workspace.Application.Features.Attachments;
using Prisma.Workspace.Application.Features.Attachments.Dtos;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Prisma.Workspace.Api.Controllers;

/// <summary>
/// Upload e download de anexos de WorkItems. O arquivo em si é tratado
/// pelo IFileStorage (Infrastructure); aqui só entra a tradução HTTP.
/// </summary>
[ApiController]
[Route("api/WorkItems/{workItemId:guid}/attachments")]
[Authorize]
public class WorkItemAttachmentsController : ControllerBase
{
    private readonly IMediator _mediator;

    public WorkItemAttachmentsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    private string ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AttachmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid workItemId, CancellationToken cancellationToken)
    {
        return Ok(await _mediator.Send(new GetAttachmentsQuery(workItemId, ActorId), cancellationToken));
    }

    [HttpPost]
    [RequestSizeLimit(50_000_000)]
    [ProducesResponseType(typeof(AttachmentDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Upload(
        Guid workItemId,
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { detail = "Arquivo vazio." });
        }

        await using var content = file.OpenReadStream();
        var dto = await _mediator.Send(new UploadAttachmentCommand(
            workItemId,
            file.FileName,
            file.ContentType,
            file.Length,
            content,
            ActorId), cancellationToken);

        return CreatedAtAction(nameof(Download), new { workItemId, attachmentId = dto.Id }, dto);
    }

    [HttpGet("{attachmentId:guid}/download")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Download(
        Guid workItemId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var arquivo = await _mediator.Send(
            new GetAttachmentFileQuery(workItemId, attachmentId, ActorId), cancellationToken);
        return File(arquivo.Content, arquivo.MimeType, arquivo.FileName);
    }

    [HttpDelete("{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(
        Guid workItemId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteAttachmentCommand(workItemId, attachmentId, ActorId), cancellationToken);
        return NoContent();
    }
}
