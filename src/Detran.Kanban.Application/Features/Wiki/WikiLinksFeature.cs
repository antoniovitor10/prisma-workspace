using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;
using MediatR;

namespace Detran.Kanban.Application.Features.Wiki;

public record WikiTaskLinkDto(Guid WorkItemId, long Number, string Title, string Reference);
public record WikiPageLinkDto(Guid PageId, string Title);

internal static class WikiLinkMap
{
    public static WikiTaskLinkDto Task(WorkItem item)
    {
        var key = item.Board?.Project?.Key ?? item.Board?.Name ?? "#";
        return new WikiTaskLinkDto(item.Id, item.Number, item.Title, $"{key}-{item.Number}");
    }
}

// ── Vincular ─────────────────────────────────────────────────────────────────

public record LinkWikiTaskCommand(Guid ProjectId, Guid PageId, long WorkItemNumber, string ActorId)
    : IRequest<WikiTaskLinkDto>;

public class LinkWikiTaskCommandHandler : IRequestHandler<LinkWikiTaskCommand, WikiTaskLinkDto>
{
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    public LinkWikiTaskCommandHandler(IWikiRepository wiki, IProjectAccessService access)
        => (_wiki, _access) = (wiki, access);

    public async Task<WikiTaskLinkDto> Handle(LinkWikiTaskCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, WikiAccess.Editor, ct);
        await GetWikiPageQueryHandler.LoadAsync(_wiki, request.ProjectId, request.PageId, ct);
        var item = await _wiki.GetProjectWorkItemByNumberAsync(request.WorkItemNumber, request.ProjectId, ct)
            ?? throw new DomainException("Tarefa não encontrada neste projeto.");
        if (await _wiki.GetLinkAsync(request.PageId, item.Id, ct) is null)
        {
            _wiki.AddLink(WikiPageWorkItemLink.Create(request.PageId, item.Id, request.ActorId));
            await _wiki.SaveAsync(ct);
        }
        return WikiLinkMap.Task(item);
    }
}

// ── Desvincular ──────────────────────────────────────────────────────────────

public record UnlinkWikiTaskCommand(Guid ProjectId, Guid PageId, Guid WorkItemId, string ActorId) : IRequest<Unit>;

public class UnlinkWikiTaskCommandHandler : IRequestHandler<UnlinkWikiTaskCommand, Unit>
{
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    public UnlinkWikiTaskCommandHandler(IWikiRepository wiki, IProjectAccessService access)
        => (_wiki, _access) = (wiki, access);

    public async Task<Unit> Handle(UnlinkWikiTaskCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, WikiAccess.Editor, ct);
        await GetWikiPageQueryHandler.LoadAsync(_wiki, request.ProjectId, request.PageId, ct);
        var link = await _wiki.GetLinkAsync(request.PageId, request.WorkItemId, ct);
        if (link is not null)
        {
            _wiki.RemoveLink(link);
            await _wiki.SaveAsync(ct);
        }
        return Unit.Value;
    }
}

// ── Tarefas de uma página ────────────────────────────────────────────────────

public record GetWikiPageLinksQuery(Guid ProjectId, Guid PageId, string ActorId) : IRequest<IReadOnlyList<WikiTaskLinkDto>>;

public class GetWikiPageLinksQueryHandler : IRequestHandler<GetWikiPageLinksQuery, IReadOnlyList<WikiTaskLinkDto>>
{
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    public GetWikiPageLinksQueryHandler(IWikiRepository wiki, IProjectAccessService access)
        => (_wiki, _access) = (wiki, access);

    public async Task<IReadOnlyList<WikiTaskLinkDto>> Handle(GetWikiPageLinksQuery request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.Viewer, ct);
        await GetWikiPageQueryHandler.LoadAsync(_wiki, request.ProjectId, request.PageId, ct);
        var links = await _wiki.GetLinksByPageAsync(request.PageId, ct);
        return links.Select(link => WikiLinkMap.Task(link.WorkItem)).ToList();
    }
}

// ── Páginas de uma tarefa (para a tela da tarefa) ────────────────────────────

public record GetTaskWikiPagesQuery(Guid ProjectId, Guid WorkItemId, string ActorId) : IRequest<IReadOnlyList<WikiPageLinkDto>>;

public class GetTaskWikiPagesQueryHandler : IRequestHandler<GetTaskWikiPagesQuery, IReadOnlyList<WikiPageLinkDto>>
{
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    public GetTaskWikiPagesQueryHandler(IWikiRepository wiki, IProjectAccessService access)
        => (_wiki, _access) = (wiki, access);

    public async Task<IReadOnlyList<WikiPageLinkDto>> Handle(GetTaskWikiPagesQuery request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, ProjectRole.Viewer, ct);
        var links = await _wiki.GetLinksByWorkItemAsync(request.WorkItemId, ct);
        return links.Select(link => new WikiPageLinkDto(link.WikiPageId, link.Page.Title)).ToList();
    }
}
