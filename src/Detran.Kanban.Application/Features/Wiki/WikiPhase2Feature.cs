using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Exceptions;
using MediatR;

namespace Detran.Kanban.Application.Features.Wiki;

// ─────────────────────────────────────────────────────────────────────────────
// Histórico de versões
// ─────────────────────────────────────────────────────────────────────────────

public record WikiRevisionDto(Guid Id, string AuthorName, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, bool IsCurrent);
public record WikiRevisionContentDto(Guid Id, string Title, string ContentHtml, string AuthorName, DateTimeOffset UpdatedAt);

public record GetWikiHistoryQuery(Guid ProjectId, Guid PageId, string ActorId) : IRequest<IReadOnlyList<WikiRevisionDto>>;

public class GetWikiHistoryQueryHandler : IRequestHandler<GetWikiHistoryQuery, IReadOnlyList<WikiRevisionDto>>
{
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    private readonly IUserDirectory _users;
    public GetWikiHistoryQueryHandler(IWikiRepository wiki, IProjectAccessService access, IUserDirectory users)
        => (_wiki, _access, _users) = (wiki, access, users);

    public async Task<IReadOnlyList<WikiRevisionDto>> Handle(GetWikiHistoryQuery request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, Domain.Enums.ProjectRole.Viewer, ct);
        await GetWikiPageQueryHandler.LoadAsync(_wiki, request.ProjectId, request.PageId, ct);
        var revisions = await _wiki.GetRevisionsAsync(request.PageId, ct);
        var names = await _users.GetDisplayNamesAsync(revisions.Select(x => x.AuthorUserId).Distinct().ToList(), ct);
        return revisions.Select((revision, index) => new WikiRevisionDto(
            revision.Id, names.GetValueOrDefault(revision.AuthorUserId, revision.AuthorUserId),
            revision.CreatedAt, revision.UpdatedAt, index == 0)).ToList();
    }
}

public record GetWikiRevisionQuery(Guid ProjectId, Guid PageId, Guid RevisionId, string ActorId) : IRequest<WikiRevisionContentDto>;

public class GetWikiRevisionQueryHandler : IRequestHandler<GetWikiRevisionQuery, WikiRevisionContentDto>
{
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    private readonly IUserDirectory _users;
    public GetWikiRevisionQueryHandler(IWikiRepository wiki, IProjectAccessService access, IUserDirectory users)
        => (_wiki, _access, _users) = (wiki, access, users);

    public async Task<WikiRevisionContentDto> Handle(GetWikiRevisionQuery request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, Domain.Enums.ProjectRole.Viewer, ct);
        await GetWikiPageQueryHandler.LoadAsync(_wiki, request.ProjectId, request.PageId, ct);
        var revision = await _wiki.GetRevisionAsync(request.RevisionId, request.PageId, ct)
            ?? throw new DomainException("Versão não encontrada.");
        var names = await _users.GetDisplayNamesAsync(new[] { revision.AuthorUserId }, ct);
        return new WikiRevisionContentDto(revision.Id, revision.Title, revision.ContentHtml,
            names.GetValueOrDefault(revision.AuthorUserId, revision.AuthorUserId), revision.UpdatedAt);
    }
}

public record RevertWikiPageCommand(Guid ProjectId, Guid PageId, Guid RevisionId, string ActorId) : IRequest<WikiPageDto>;

public class RevertWikiPageCommandHandler : IRequestHandler<RevertWikiPageCommand, WikiPageDto>
{
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    private readonly IUserDirectory _users;
    public RevertWikiPageCommandHandler(IWikiRepository wiki, IProjectAccessService access, IUserDirectory users)
        => (_wiki, _access, _users) = (wiki, access, users);

    public async Task<WikiPageDto> Handle(RevertWikiPageCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, WikiAccess.Editor, ct);
        var page = await GetWikiPageQueryHandler.LoadAsync(_wiki, request.ProjectId, request.PageId, ct);
        var revision = await _wiki.GetRevisionAsync(request.RevisionId, request.PageId, ct)
            ?? throw new DomainException("Versão não encontrada.");
        var now = DateTimeOffset.UtcNow;
        page.AdquirirTrava(request.ActorId, now);
        page.SetContent(revision.ContentHtml, request.ActorId);
        // A reversão é um evento próprio: sempre sela uma nova revisão.
        _wiki.AddRevision(WikiPageRevision.Create(page.Id, page.Title, page.ContentHtml, request.ActorId));
        await _wiki.SaveAsync(ct);
        var names = await _users.GetDisplayNamesAsync(new[] { page.UpdatedByUserId }, ct);
        return WikiMapper.Page(page, request.ActorId, true, names);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Anexos (arquivos) — imagens embutidas ficam no próprio conteúdo (base64)
// ─────────────────────────────────────────────────────────────────────────────

public record WikiAttachmentDto(Guid Id, string FileName, long FileSize, string? MimeType, string UploadedByName, DateTimeOffset CreatedAt);
public record WikiAttachmentFileDto(Stream Content, string FileName, string? MimeType);

public record GetWikiAttachmentsQuery(Guid ProjectId, Guid PageId, string ActorId) : IRequest<IReadOnlyList<WikiAttachmentDto>>;

public class GetWikiAttachmentsQueryHandler : IRequestHandler<GetWikiAttachmentsQuery, IReadOnlyList<WikiAttachmentDto>>
{
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    private readonly IUserDirectory _users;
    public GetWikiAttachmentsQueryHandler(IWikiRepository wiki, IProjectAccessService access, IUserDirectory users)
        => (_wiki, _access, _users) = (wiki, access, users);

    public async Task<IReadOnlyList<WikiAttachmentDto>> Handle(GetWikiAttachmentsQuery request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, Domain.Enums.ProjectRole.Viewer, ct);
        await GetWikiPageQueryHandler.LoadAsync(_wiki, request.ProjectId, request.PageId, ct);
        var attachments = await _wiki.GetAttachmentsAsync(request.PageId, ct);
        var names = await _users.GetDisplayNamesAsync(attachments.Select(x => x.UploadedByUserId).Distinct().ToList(), ct);
        return attachments.Select(x => new WikiAttachmentDto(
            x.Id, x.FileName, x.FileSize, x.MimeType,
            names.GetValueOrDefault(x.UploadedByUserId, x.UploadedByUserId), x.CreatedAt)).ToList();
    }
}

public record UploadWikiAttachmentCommand(
    Guid ProjectId, Guid PageId, string FileName, string? ContentType, long Size, Stream Content, string ActorId)
    : IRequest<WikiAttachmentDto>;

public class UploadWikiAttachmentCommandHandler : IRequestHandler<UploadWikiAttachmentCommand, WikiAttachmentDto>
{
    private const long MaxBytes = 25L * 1024 * 1024;
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    private readonly IUserDirectory _users;
    private readonly IFileStorage _storage;
    public UploadWikiAttachmentCommandHandler(
        IWikiRepository wiki, IProjectAccessService access, IUserDirectory users, IFileStorage storage)
        => (_wiki, _access, _users, _storage) = (wiki, access, users, storage);

    public async Task<WikiAttachmentDto> Handle(UploadWikiAttachmentCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, WikiAccess.Editor, ct);
        DomainException.Garantir(!string.IsNullOrWhiteSpace(request.FileName), "Arquivo inválido.");
        DomainException.Garantir(request.Size > 0 && request.Size <= MaxBytes, "O arquivo deve ter até 25 MB.");
        var page = await GetWikiPageQueryHandler.LoadAsync(_wiki, request.ProjectId, request.PageId, ct);

        var extension = Path.GetExtension(request.FileName);
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var path = await _storage.SaveAsync($"wiki/{page.Id}", storedName, request.Content, ct);

        var attachment = WikiAttachment.Create(page.Id, path, request.FileName, request.Size, request.ContentType, request.ActorId);
        _wiki.AddAttachment(attachment);
        await _wiki.SaveAsync(ct);

        var names = await _users.GetDisplayNamesAsync(new[] { request.ActorId }, ct);
        return new WikiAttachmentDto(attachment.Id, attachment.FileName, attachment.FileSize, attachment.MimeType,
            names.GetValueOrDefault(request.ActorId, request.ActorId), attachment.CreatedAt);
    }
}

public record DownloadWikiAttachmentQuery(Guid ProjectId, Guid PageId, Guid AttachmentId, string ActorId) : IRequest<WikiAttachmentFileDto>;

public class DownloadWikiAttachmentQueryHandler : IRequestHandler<DownloadWikiAttachmentQuery, WikiAttachmentFileDto>
{
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    private readonly IFileStorage _storage;
    public DownloadWikiAttachmentQueryHandler(IWikiRepository wiki, IProjectAccessService access, IFileStorage storage)
        => (_wiki, _access, _storage) = (wiki, access, storage);

    public async Task<WikiAttachmentFileDto> Handle(DownloadWikiAttachmentQuery request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, Domain.Enums.ProjectRole.Viewer, ct);
        await GetWikiPageQueryHandler.LoadAsync(_wiki, request.ProjectId, request.PageId, ct);
        var attachment = await _wiki.GetAttachmentAsync(request.AttachmentId, request.PageId, ct)
            ?? throw new DomainException("Anexo não encontrado.");
        var stream = _storage.OpenRead(attachment.StoragePath)
            ?? throw new DomainException("Arquivo do anexo indisponível.");
        return new WikiAttachmentFileDto(stream, attachment.FileName, attachment.MimeType);
    }
}

public record DeleteWikiAttachmentCommand(Guid ProjectId, Guid PageId, Guid AttachmentId, string ActorId) : IRequest<Unit>;

public class DeleteWikiAttachmentCommandHandler : IRequestHandler<DeleteWikiAttachmentCommand, Unit>
{
    private readonly IWikiRepository _wiki;
    private readonly IProjectAccessService _access;
    private readonly IFileStorage _storage;
    public DeleteWikiAttachmentCommandHandler(IWikiRepository wiki, IProjectAccessService access, IFileStorage storage)
        => (_wiki, _access, _storage) = (wiki, access, storage);

    public async Task<Unit> Handle(DeleteWikiAttachmentCommand request, CancellationToken ct)
    {
        await _access.EnsureAtLeastAsync(request.ProjectId, request.ActorId, WikiAccess.Editor, ct);
        await GetWikiPageQueryHandler.LoadAsync(_wiki, request.ProjectId, request.PageId, ct);
        var attachment = await _wiki.GetAttachmentAsync(request.AttachmentId, request.PageId, ct)
            ?? throw new DomainException("Anexo não encontrado.");
        _wiki.RemoveAttachment(attachment);
        await _wiki.SaveAsync(ct);
        await _storage.DeleteAsync(attachment.StoragePath, ct);
        return Unit.Value;
    }
}
