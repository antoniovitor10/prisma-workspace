using Detran.Kanban.Application.Common.Exceptions;
using Detran.Kanban.Application.Features.Attachments.Dtos;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;
using MediatR;

namespace Detran.Kanban.Application.Features.Attachments;

public static class AttachmentMapper
{
    public static AttachmentDto ToDto(this Attachment a) => new()
    {
        Id = a.Id,
        WorkItemId = a.WorkItemId,
        FileName = a.FileName,
        FileSize = a.FileSize,
        MimeType = a.MimeType,
        UploadedBy = a.UploadedBy,
        CreatedAt = a.CreatedAt
    };
}

/// <summary>Anexos de uma tarefa.</summary>
public record GetAttachmentsQuery(Guid WorkItemId, string ActorId) : IRequest<IReadOnlyList<AttachmentDto>>;

public class GetAttachmentsQueryHandler : IRequestHandler<GetAttachmentsQuery, IReadOnlyList<AttachmentDto>>
{
    private readonly IAttachmentRepository _attachments;
    private readonly IWorkItemRepository _workItems;
    private readonly IWorkItemAccessService _access;

    public GetAttachmentsQueryHandler(
        IAttachmentRepository attachments,
        IWorkItemRepository workItems,
        IWorkItemAccessService access)
    {
        _attachments = attachments;
        _workItems = workItems;
        _access = access;
    }

    public async Task<IReadOnlyList<AttachmentDto>> Handle(GetAttachmentsQuery request, CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(
            request.WorkItemId, request.ActorId, PlatformPermission.View,
            ProjectRole.Viewer, cancellationToken);
        _ = await _workItems.GetByIdAsync(request.WorkItemId, cancellationToken)
            ?? throw new NaoEncontradoException("Tarefa");
        var attachments = await _attachments.GetByWorkItemIdAsync(request.WorkItemId, cancellationToken);
        return attachments.Select(a => a.ToDto()).ToList();
    }
}

/// <summary>Sobe um anexo: grava o arquivo no storage e o registro no banco.</summary>
public record UploadAttachmentCommand(
    Guid WorkItemId,
    string FileName,
    string? MimeType,
    long Length,
    Stream Content,
    string? UploadedBy) : IRequest<AttachmentDto>;

public class UploadAttachmentCommandHandler : IRequestHandler<UploadAttachmentCommand, AttachmentDto>
{
    private readonly IAttachmentRepository _attachments;
    private readonly IWorkItemRepository _workItems;
    private readonly IFileStorage _storage;
    private readonly IWorkItemAccessService _access;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".gif", ".webp", ".txt", ".csv",
        ".doc", ".docx", ".xls", ".xlsx", ".zip"
    };

    public UploadAttachmentCommandHandler(
        IAttachmentRepository attachments,
        IWorkItemRepository workItems,
        IFileStorage storage,
        IWorkItemAccessService access)
    {
        _attachments = attachments;
        _workItems = workItems;
        _storage = storage;
        _access = access;
    }

    public async Task<AttachmentDto> Handle(UploadAttachmentCommand request, CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(
            request.WorkItemId, request.UploadedBy ?? string.Empty,
            PlatformPermission.Edit, ProjectRole.Member,
            cancellationToken);
        DomainException.Garantir(request.Length is > 0 and <= 50_000_000,
            "O arquivo deve possuir até 50 MB.");

        var originalFileName = Path.GetFileName(request.FileName);
        DomainException.Garantir(!string.IsNullOrWhiteSpace(originalFileName)
            && originalFileName.Length <= 255, "Nome de arquivo inválido.");
        var extension = Path.GetExtension(originalFileName);
        DomainException.Garantir(AllowedExtensions.Contains(extension),
            "Tipo de arquivo não permitido.");

        _ = await _workItems.GetByIdAsync(request.WorkItemId, cancellationToken)
            ?? throw new NaoEncontradoException("Tarefa");

        var attachmentId = Guid.NewGuid();
        var storedFileName = $"{attachmentId:N}{Path.GetExtension(originalFileName)}";
        var relativePath = await _storage.SaveAsync(
            request.WorkItemId.ToString("N"), storedFileName, request.Content, cancellationToken);

        var attachment = new Attachment
        {
            Id = attachmentId,
            WorkItemId = request.WorkItemId,
            StoragePath = relativePath,
            FileName = originalFileName,
            FileSize = request.Length,
            MimeType = string.IsNullOrWhiteSpace(request.MimeType)
                ? "application/octet-stream"
                : request.MimeType,
            UploadedBy = request.UploadedBy,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _attachments.AddAsync(attachment, cancellationToken);
        return attachment.ToDto();
    }
}

/// <summary>Conteúdo de um anexo para download.</summary>
public record AttachmentFileDto(Stream Content, string MimeType, string FileName);

/// <summary>Abre um anexo para download.</summary>
public record GetAttachmentFileQuery(Guid WorkItemId, Guid AttachmentId, string ActorId)
    : IRequest<AttachmentFileDto>;

public class GetAttachmentFileQueryHandler : IRequestHandler<GetAttachmentFileQuery, AttachmentFileDto>
{
    private readonly IAttachmentRepository _attachments;
    private readonly IFileStorage _storage;
    private readonly IWorkItemAccessService _access;

    public GetAttachmentFileQueryHandler(
        IAttachmentRepository attachments,
        IFileStorage storage,
        IWorkItemAccessService access)
    {
        _attachments = attachments;
        _storage = storage;
        _access = access;
    }

    public async Task<AttachmentFileDto> Handle(GetAttachmentFileQuery request, CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(
            request.WorkItemId, request.ActorId, PlatformPermission.View,
            ProjectRole.Viewer, cancellationToken);
        var attachment = await _attachments.GetByIdAsync(request.AttachmentId, cancellationToken);
        if (attachment is null || attachment.WorkItemId != request.WorkItemId)
            throw new NaoEncontradoException("Anexo");

        var stream = _storage.OpenRead(attachment.StoragePath)
            ?? throw new NaoEncontradoException("Arquivo do anexo");

        return new AttachmentFileDto(stream, attachment.MimeType ?? "application/octet-stream", attachment.FileName);
    }
}

/// <summary>Remove um anexo da tarefa (confirmação na UI). Lixeira de 7 dias aguarda G-MIGRATION.</summary>
public record DeleteAttachmentCommand(Guid WorkItemId, Guid AttachmentId, string ActorId) : IRequest<Unit>;

public class DeleteAttachmentCommandHandler : IRequestHandler<DeleteAttachmentCommand, Unit>
{
    private readonly IAttachmentRepository _attachments;
    private readonly IFileStorage _storage;
    private readonly IWorkItemAccessService _access;

    public DeleteAttachmentCommandHandler(
        IAttachmentRepository attachments,
        IFileStorage storage,
        IWorkItemAccessService access)
    {
        _attachments = attachments;
        _storage = storage;
        _access = access;
    }

    public async Task<Unit> Handle(DeleteAttachmentCommand request, CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(
            request.WorkItemId, request.ActorId, PlatformPermission.Edit,
            ProjectRole.Member, cancellationToken);

        var attachment = await _attachments.GetByIdAsync(request.AttachmentId, cancellationToken);
        if (attachment is null || attachment.WorkItemId != request.WorkItemId)
            throw new NaoEncontradoException("Anexo");

        await _attachments.DeleteAsync(attachment, cancellationToken);
        await _storage.DeleteAsync(attachment.StoragePath, cancellationToken);
        return Unit.Value;
    }
}
