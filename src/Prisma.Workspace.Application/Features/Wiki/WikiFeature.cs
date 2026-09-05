using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using MediatR;

namespace Prisma.Workspace.Application.Features.Wiki;

// ─────────────────────────────────────────────────────────────────────────────
// DTOs
// ─────────────────────────────────────────────────────────────────────────────

public record WikiTreeNodeDto(
    Guid Id, Guid? ParentPageId, string Title, double Position,
    DateTimeOffset UpdatedAt, bool LockedByOther);

public record WikiLockDto(
    string? LockedByUserId, string? LockedByName, DateTimeOffset? LockedAt, bool LockedByOther);

public record WikiPageDto(
    Guid Id, Guid ProjectId, Guid? ParentPageId, string Title, string ContentHtml,
    DateTimeOffset UpdatedAt, string UpdatedByName, bool CanEdit, WikiLockDto Lock);

// ─────────────────────────────────────────────────────────────────────────────
// Helpers compartilhados
// ─────────────────────────────────────────────────────────────────────────────

internal static class WikiAccess
{
    public const ProjectRole Editor = ProjectRole.Member; // >= Member edita; Viewer só lê.

    public static async Task<ProjectRole> EnsureViewerAndGetRoleAsync(
        IProjectAccessService access, Guid projectId, string userId, CancellationToken ct)
    {
        await access.EnsureAtLeastAsync(projectId, userId, ProjectRole.Viewer, ct);
        return await access.GetRoleAsync(projectId, userId, ct) ?? ProjectRole.Viewer;
    }

    public static bool PodeEditar(ProjectRole role) => (int)role >= (int)Editor;
}

internal static class WikiMapper
{
    public static WikiLockDto Lock(WikiPage page, string actorId, IReadOnlyDictionary<string, string> names)
    {
        var lockedByOther = page.EstaTravada(DateTimeOffset.UtcNow) && page.LockedByUserId != actorId;
        return new WikiLockDto(
            page.LockedByUserId,
            page.LockedByUserId is null ? null : names.GetValueOrDefault(page.LockedByUserId, page.LockedByUserId),
            page.LockedAt,
            lockedByOther);
    }

    public static WikiPageDto Page(
        WikiPage page, string actorId, bool canEditRole, IReadOnlyDictionary<string, string> names)
    {
        var canEdit = canEditRole && page.PodeEditar(actorId, DateTimeOffset.UtcNow);
        return new WikiPageDto(
            page.Id, page.ProjectId, page.ParentPageId, page.Title, page.ContentHtml,
            page.UpdatedAt, names.GetValueOrDefault(page.UpdatedByUserId, page.UpdatedByUserId),
            canEdit, Lock(page, actorId, names));
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Árvore (com criação automática da página "Início")
// ─────────────────────────────────────────────────────────────────────────────

public record GetWikiTreeQuery(Guid ProjectId, string ActorId, bool IncludeTrash = false)
    : IRequest<IReadOnlyList<WikiTreeNodeDto>>;

public class GetWikiTreeQueryHandler : IRequestHandler<GetWikiTreeQuery, IReadOnlyList<WikiTreeNodeDto>>
{
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    public GetWikiTreeQueryHandler(IWikiRepository wiki, IProjectAccessService access)
        => (_wiki, _access) = (wiki, access);

    public async Task<IReadOnlyList<WikiTreeNodeDto>> Handle(GetWikiTreeQuery request, CancellationToken ct)
    {
        var role = await WikiAccess.EnsureViewerAndGetRoleAsync(_access, request.ProjectId, request.ActorId, ct);

        if (!request.IncludeTrash && !await _wiki.HasAnyAsync(request.ProjectId, ct) && WikiAccess.PodeEditar(role))
        {
            var inicio = WikiPage.Create(request.ProjectId, null, "Início", request.ActorId, 10);
            await _wiki.AddAsync(inicio, ct);
        }

        var pages = await _wiki.GetTreeAsync(request.ProjectId, request.IncludeTrash, ct);
        var now = DateTimeOffset.UtcNow;
        return pages
            .Where(page => request.IncludeTrash ? page.IsDeleted : !page.IsDeleted)
            .Select(page => new WikiTreeNodeDto(
                page.Id, page.ParentPageId, page.Title, page.Position, page.UpdatedAt,
                page.EstaTravada(now) && page.LockedByUserId != request.ActorId))
            .ToList();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Página individual
// ─────────────────────────────────────────────────────────────────────────────

public record GetWikiPageQuery(Guid ProjectId, Guid PageId, string ActorId) : IRequest<WikiPageDto>;

public class GetWikiPageQueryHandler : IRequestHandler<GetWikiPageQuery, WikiPageDto>
{
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    private readonly IUserDirectory _users;
    public GetWikiPageQueryHandler(IWikiRepository wiki, IProjectAccessService access, IUserDirectory users)
        => (_wiki, _access, _users) = (wiki, access, users);

    public async Task<WikiPageDto> Handle(GetWikiPageQuery request, CancellationToken ct)
    {
        var role = await WikiAccess.EnsureViewerAndGetRoleAsync(_access, request.ProjectId, request.ActorId, ct);
        var page = await LoadAsync(_wiki, request.ProjectId, request.PageId, ct);
        var names = await _users.GetDisplayNamesAsync(
            new[] { page.UpdatedByUserId, page.LockedByUserId ?? string.Empty }
                .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList(), ct);
        return WikiMapper.Page(page, request.ActorId, WikiAccess.PodeEditar(role), names);
    }

    internal static async Task<WikiPage> LoadAsync(IWikiRepository wiki, Guid projectId, Guid pageId, CancellationToken ct)
    {
        var page = await wiki.GetByIdAsync(pageId, ct);
        DomainException.Garantir(page is not null && page.ProjectId == projectId && !page.IsDeleted,
            "Página do wiki não encontrada.");
        return page!;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Criar
// ─────────────────────────────────────────────────────────────────────────────

public record CreateWikiPageCommand(Guid ProjectId, Guid? ParentPageId, string Title, string ActorId)
    : IRequest<WikiPageDto>;

public class CreateWikiPageCommandHandler : IRequestHandler<CreateWikiPageCommand, WikiPageDto>
{
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    private readonly IUserDirectory _users;
    public CreateWikiPageCommandHandler(IWikiRepository wiki, IProjectAccessService access, IUserDirectory users)
        => (_wiki, _access, _users) = (wiki, access, users);

    public async Task<WikiPageDto> Handle(CreateWikiPageCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, WikiAccess.Editor, ct);

        if (request.ParentPageId is not null)
        {
            var parent = await _wiki.GetByIdAsync(request.ParentPageId.Value, ct);
            DomainException.Garantir(parent is not null && parent.ProjectId == request.ProjectId && !parent.IsDeleted,
                "Página-pai inválida.");
        }

        var position = await _wiki.GetNextPositionAsync(request.ProjectId, request.ParentPageId, ct);
        var page = WikiPage.Create(request.ProjectId, request.ParentPageId,
            string.IsNullOrWhiteSpace(request.Title) ? "Nova página" : request.Title, request.ActorId, position);
        await _wiki.AddAsync(page, ct);

        var names = await _users.GetDisplayNamesAsync(new[] { page.UpdatedByUserId }, ct);
        return WikiMapper.Page(page, request.ActorId, true, names);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Renomear
// ─────────────────────────────────────────────────────────────────────────────

public record RenameWikiPageCommand(Guid ProjectId, Guid PageId, string Title, string ActorId)
    : IRequest<WikiPageDto>;

public class RenameWikiPageCommandHandler : IRequestHandler<RenameWikiPageCommand, WikiPageDto>
{
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    private readonly IUserDirectory _users;
    public RenameWikiPageCommandHandler(IWikiRepository wiki, IProjectAccessService access, IUserDirectory users)
        => (_wiki, _access, _users) = (wiki, access, users);

    public async Task<WikiPageDto> Handle(RenameWikiPageCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, WikiAccess.Editor, ct);
        var page = await GetWikiPageQueryHandler.LoadAsync(_wiki, request.ProjectId, request.PageId, ct);
        page.Rename(request.Title, request.ActorId);
        await _wiki.SaveAsync(ct);
        var names = await _users.GetDisplayNamesAsync(new[] { page.UpdatedByUserId }, ct);
        return WikiMapper.Page(page, request.ActorId, true, names);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Salvar conteúdo (autosave) — exige/renova a trava e sanitiza o HTML
// ─────────────────────────────────────────────────────────────────────────────

public record SaveWikiContentCommand(Guid ProjectId, Guid PageId, string ContentHtml, string ActorId)
    : IRequest<WikiPageDto>;

public class SaveWikiContentCommandHandler : IRequestHandler<SaveWikiContentCommand, WikiPageDto>
{
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    private readonly IUserDirectory _users;
    private readonly IHtmlSanitizer _sanitizer;
    public SaveWikiContentCommandHandler(
        IWikiRepository wiki, IProjectAccessService access, IUserDirectory users, IHtmlSanitizer sanitizer)
        => (_wiki, _access, _users, _sanitizer) = (wiki, access, users, sanitizer);

    public async Task<WikiPageDto> Handle(SaveWikiContentCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, WikiAccess.Editor, ct);
        var page = await GetWikiPageQueryHandler.LoadAsync(_wiki, request.ProjectId, request.PageId, ct);
        var now = DateTimeOffset.UtcNow;
        // Adquire/renova a trava para o autor (falha se travada por outra pessoa).
        page.AdquirirTrava(request.ActorId, now);
        page.SetContent(_sanitizer.Sanitize(request.ContentHtml), request.ActorId);

        // Histórico por sessão: mesma pessoa dentro da janela atualiza a revisão
        // corrente; autor diferente (ou pausa) sela a anterior e abre uma nova.
        var latest = await _wiki.GetLatestRevisionAsync(page.Id, ct);
        if (latest is not null && latest.MesmaSessao(request.ActorId, now))
            latest.Amend(page.Title, page.ContentHtml);
        else
            _wiki.AddRevision(WikiPageRevision.Create(page.Id, page.Title, page.ContentHtml, request.ActorId));

        await _wiki.SaveAsync(ct);
        var names = await _users.GetDisplayNamesAsync(new[] { page.UpdatedByUserId }, ct);
        return WikiMapper.Page(page, request.ActorId, true, names);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Mover na árvore
// ─────────────────────────────────────────────────────────────────────────────

public record MoveWikiPageCommand(Guid ProjectId, Guid PageId, Guid? ParentPageId, double Position, string ActorId)
    : IRequest<WikiPageDto>;

public class MoveWikiPageCommandHandler : IRequestHandler<MoveWikiPageCommand, WikiPageDto>
{
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    private readonly IUserDirectory _users;
    public MoveWikiPageCommandHandler(IWikiRepository wiki, IProjectAccessService access, IUserDirectory users)
        => (_wiki, _access, _users) = (wiki, access, users);

    public async Task<WikiPageDto> Handle(MoveWikiPageCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, WikiAccess.Editor, ct);
        var pages = await _wiki.GetTreeAsync(request.ProjectId, true, ct);
        var page = pages.FirstOrDefault(x => x.Id == request.PageId && !x.IsDeleted)
            ?? throw new DomainException("Página do wiki não encontrada.");

        if (request.ParentPageId is not null)
        {
            DomainException.Garantir(pages.Any(x => x.Id == request.ParentPageId && !x.IsDeleted),
                "Página-pai inválida.");
            // Impede mover uma página para dentro de um descendente dela.
            var descendants = Descendants(pages, request.PageId);
            DomainException.Garantir(!descendants.Contains(request.ParentPageId.Value),
                "Não é possível mover uma página para dentro de uma subpágina dela.");
        }

        page.MoveTo(request.ParentPageId, request.Position, request.ActorId);
        await _wiki.SaveAsync(ct);
        var names = await _users.GetDisplayNamesAsync(new[] { page.UpdatedByUserId }, ct);
        return WikiMapper.Page(page, request.ActorId, true, names);
    }

    internal static HashSet<Guid> Descendants(IReadOnlyList<WikiPage> pages, Guid rootId)
    {
        var byParent = pages.GroupBy(x => x.ParentPageId).ToDictionary(g => g.Key ?? Guid.Empty, g => g.ToList());
        var result = new HashSet<Guid>();
        var stack = new Stack<Guid>();
        stack.Push(rootId);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!byParent.TryGetValue(current, out var children)) continue;
            foreach (var child in children)
                if (result.Add(child.Id)) stack.Push(child.Id);
        }
        return result;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Trava: adquirir/renovar e liberar
// ─────────────────────────────────────────────────────────────────────────────

public record AcquireWikiLockCommand(Guid ProjectId, Guid PageId, string ActorId) : IRequest<WikiLockDto>;

public class AcquireWikiLockCommandHandler : IRequestHandler<AcquireWikiLockCommand, WikiLockDto>
{
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    private readonly IUserDirectory _users;
    public AcquireWikiLockCommandHandler(IWikiRepository wiki, IProjectAccessService access, IUserDirectory users)
        => (_wiki, _access, _users) = (wiki, access, users);

    public async Task<WikiLockDto> Handle(AcquireWikiLockCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, WikiAccess.Editor, ct);
        var page = await GetWikiPageQueryHandler.LoadAsync(_wiki, request.ProjectId, request.PageId, ct);
        page.AdquirirTrava(request.ActorId, DateTimeOffset.UtcNow);
        await _wiki.SaveAsync(ct);
        var names = await _users.GetDisplayNamesAsync(new[] { request.ActorId }, ct);
        return WikiMapper.Lock(page, request.ActorId, names);
    }
}

public record ReleaseWikiLockCommand(Guid ProjectId, Guid PageId, string ActorId, bool Force = false)
    : IRequest<Unit>;

public class ReleaseWikiLockCommandHandler : IRequestHandler<ReleaseWikiLockCommand, Unit>
{
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    public ReleaseWikiLockCommandHandler(IWikiRepository wiki, IProjectAccessService access)
        => (_wiki, _access) = (wiki, access);

    public async Task<Unit> Handle(ReleaseWikiLockCommand request, CancellationToken ct)
    {
        var role = await WikiAccess.EnsureViewerAndGetRoleAsync(_access, request.ProjectId, request.ActorId, ct);
        var page = await GetWikiPageQueryHandler.LoadAsync(_wiki, request.ProjectId, request.PageId, ct);
        if (request.Force && (int)role >= (int)ProjectRole.ProductOwner)
            page.ForcarLiberarTrava();
        else
            page.LiberarTrava(request.ActorId);
        await _wiki.SaveAsync(ct);
        return Unit.Value;
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Lixeira: mover para lixeira (subárvore) e restaurar
// ─────────────────────────────────────────────────────────────────────────────

public record DeleteWikiPageCommand(Guid ProjectId, Guid PageId, string ActorId) : IRequest<Unit>;

public class DeleteWikiPageCommandHandler : IRequestHandler<DeleteWikiPageCommand, Unit>
{
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    public DeleteWikiPageCommandHandler(IWikiRepository wiki, IProjectAccessService access)
        => (_wiki, _access) = (wiki, access);

    public async Task<Unit> Handle(DeleteWikiPageCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, WikiAccess.Editor, ct);
        var pages = await _wiki.GetTreeAsync(request.ProjectId, true, ct);
        var page = pages.FirstOrDefault(x => x.Id == request.PageId && !x.IsDeleted)
            ?? throw new DomainException("Página do wiki não encontrada.");

        var subtree = MoveWikiPageCommandHandler.Descendants(pages, request.PageId);
        foreach (var target in pages.Where(x => (x.Id == request.PageId || subtree.Contains(x.Id)) && !x.IsDeleted))
            target.MoverParaLixeira(request.ActorId);
        await _wiki.SaveAsync(ct);
        return Unit.Value;
    }
}

public record RestoreWikiPageCommand(Guid ProjectId, Guid PageId, string ActorId) : IRequest<Unit>;

public class RestoreWikiPageCommandHandler : IRequestHandler<RestoreWikiPageCommand, Unit>
{
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    public RestoreWikiPageCommandHandler(IWikiRepository wiki, IProjectAccessService access)
        => (_wiki, _access) = (wiki, access);

    public async Task<Unit> Handle(RestoreWikiPageCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, WikiAccess.Editor, ct);
        var pages = await _wiki.GetTreeAsync(request.ProjectId, true, ct);
        var page = pages.FirstOrDefault(x => x.Id == request.PageId && x.IsDeleted)
            ?? throw new DomainException("Página não está na lixeira.");

        // Se o pai continuar na lixeira, a página volta como página de topo.
        var parentDeleted = page.ParentPageId is not null
            && pages.Any(x => x.Id == page.ParentPageId && x.IsDeleted);

        var subtree = MoveWikiPageCommandHandler.Descendants(pages, request.PageId);
        foreach (var target in pages.Where(x => (x.Id == request.PageId || subtree.Contains(x.Id)) && x.IsDeleted))
            target.Restaurar(request.ActorId);
        if (parentDeleted) page.MoveTo(null, page.Position, request.ActorId);
        await _wiki.SaveAsync(ct);
        return Unit.Value;
    }
}
