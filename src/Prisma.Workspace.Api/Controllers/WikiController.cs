using System.Security.Claims;
using Prisma.Workspace.Application.Features.Wiki;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Prisma.Workspace.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/wiki")]
public class WikiController : ControllerBase
{
    private readonly IMediator _mediator;
    public WikiController(IMediator mediator) => _mediator = mediator;
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("tree")]
    public async Task<IActionResult> GetTree(Guid projectId, [FromQuery] bool includeTrash, CancellationToken ct)
        => Ok(await _mediator.Send(new GetWikiTreeQuery(projectId, UserId, includeTrash), ct));

    [HttpGet("pages/{pageId:guid}")]
    public async Task<IActionResult> GetPage(Guid projectId, Guid pageId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetWikiPageQuery(projectId, pageId, UserId), ct));

    [HttpPost("pages")]
    public async Task<IActionResult> Create(Guid projectId, [FromBody] CreateWikiPageRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new CreateWikiPageCommand(projectId, request.ParentPageId, request.Title, UserId), ct));

    [HttpPut("pages/{pageId:guid}/title")]
    public async Task<IActionResult> Rename(Guid projectId, Guid pageId, [FromBody] RenameWikiPageRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new RenameWikiPageCommand(projectId, pageId, request.Title, UserId), ct));

    [HttpPut("pages/{pageId:guid}/content")]
    public async Task<IActionResult> SaveContent(Guid projectId, Guid pageId, [FromBody] SaveWikiContentRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new SaveWikiContentCommand(projectId, pageId, request.ContentHtml, UserId), ct));

    [HttpPut("pages/{pageId:guid}/move")]
    public async Task<IActionResult> Move(Guid projectId, Guid pageId, [FromBody] MoveWikiPageRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new MoveWikiPageCommand(projectId, pageId, request.ParentPageId, request.Position, UserId), ct));

    [HttpPost("pages/{pageId:guid}/lock")]
    public async Task<IActionResult> AcquireLock(Guid projectId, Guid pageId, CancellationToken ct)
        => Ok(await _mediator.Send(new AcquireWikiLockCommand(projectId, pageId, UserId), ct));

    [HttpDelete("pages/{pageId:guid}/lock")]
    public async Task<IActionResult> ReleaseLock(Guid projectId, Guid pageId, [FromQuery] bool force, CancellationToken ct)
    {
        await _mediator.Send(new ReleaseWikiLockCommand(projectId, pageId, UserId, force), ct);
        return NoContent();
    }

    [HttpDelete("pages/{pageId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, Guid pageId, CancellationToken ct)
    {
        await _mediator.Send(new DeleteWikiPageCommand(projectId, pageId, UserId), ct);
        return NoContent();
    }

    [HttpPost("pages/{pageId:guid}/restore")]
    public async Task<IActionResult> Restore(Guid projectId, Guid pageId, CancellationToken ct)
    {
        await _mediator.Send(new RestoreWikiPageCommand(projectId, pageId, UserId), ct);
        return NoContent();
    }

    // ── Histórico ───────────────────────────────────────────────────────

    [HttpGet("pages/{pageId:guid}/history")]
    public async Task<IActionResult> History(Guid projectId, Guid pageId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetWikiHistoryQuery(projectId, pageId, UserId), ct));

    [HttpGet("pages/{pageId:guid}/revisions/{revisionId:guid}")]
    public async Task<IActionResult> Revision(Guid projectId, Guid pageId, Guid revisionId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetWikiRevisionQuery(projectId, pageId, revisionId, UserId), ct));

    [HttpPost("pages/{pageId:guid}/revisions/{revisionId:guid}/revert")]
    public async Task<IActionResult> Revert(Guid projectId, Guid pageId, Guid revisionId, CancellationToken ct)
        => Ok(await _mediator.Send(new RevertWikiPageCommand(projectId, pageId, revisionId, UserId), ct));

    // ── Anexos ──────────────────────────────────────────────────────────

    [HttpGet("pages/{pageId:guid}/attachments")]
    public async Task<IActionResult> Attachments(Guid projectId, Guid pageId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetWikiAttachmentsQuery(projectId, pageId, UserId), ct));

    [HttpPost("pages/{pageId:guid}/attachments")]
    [RequestSizeLimit(30_000_000)]
    public async Task<IActionResult> Upload(Guid projectId, Guid pageId, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await _mediator.Send(new UploadWikiAttachmentCommand(
            projectId, pageId, file.FileName, file.ContentType, file.Length, stream, UserId), ct);
        return Ok(result);
    }

    [HttpGet("pages/{pageId:guid}/attachments/{attachmentId:guid}/download")]
    public async Task<IActionResult> Download(Guid projectId, Guid pageId, Guid attachmentId, CancellationToken ct)
    {
        var file = await _mediator.Send(new DownloadWikiAttachmentQuery(projectId, pageId, attachmentId, UserId), ct);
        return File(file.Content, file.MimeType ?? "application/octet-stream", file.FileName);
    }

    [HttpDelete("pages/{pageId:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> DeleteAttachment(Guid projectId, Guid pageId, Guid attachmentId, CancellationToken ct)
    {
        await _mediator.Send(new DeleteWikiAttachmentCommand(projectId, pageId, attachmentId, UserId), ct);
        return NoContent();
    }

    // ── Vínculos página↔tarefa ──────────────────────────────────────────

    [HttpGet("pages/{pageId:guid}/links")]
    public async Task<IActionResult> Links(Guid projectId, Guid pageId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetWikiPageLinksQuery(projectId, pageId, UserId), ct));

    [HttpPost("pages/{pageId:guid}/links")]
    public async Task<IActionResult> Link(Guid projectId, Guid pageId, [FromBody] LinkWikiTaskRequest request, CancellationToken ct)
        => Ok(await _mediator.Send(new LinkWikiTaskCommand(projectId, pageId, request.WorkItemNumber, UserId), ct));

    [HttpDelete("pages/{pageId:guid}/links/{workItemId:guid}")]
    public async Task<IActionResult> Unlink(Guid projectId, Guid pageId, Guid workItemId, CancellationToken ct)
    {
        await _mediator.Send(new UnlinkWikiTaskCommand(projectId, pageId, workItemId, UserId), ct);
        return NoContent();
    }

    [HttpGet("task-links/{workItemId:guid}")]
    public async Task<IActionResult> TaskLinks(Guid projectId, Guid workItemId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetTaskWikiPagesQuery(projectId, workItemId, UserId), ct));
}

public record LinkWikiTaskRequest(long WorkItemNumber);

public record CreateWikiPageRequest(Guid? ParentPageId, string Title);
public record RenameWikiPageRequest(string Title);
public record SaveWikiContentRequest(string ContentHtml);
public record MoveWikiPageRequest(Guid? ParentPageId, double Position);
