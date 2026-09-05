using Detran.Kanban.Application.Common.Exceptions;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using MediatR;

namespace Detran.Kanban.Application.Features.WorkItemDetails;

// ─── DTOs ───────────────────────────────────────────────────────────────────

public class ChecklistItemDto
{
    public Guid Id { get; init; }
    public string Text { get; init; } = string.Empty;
    public bool Done { get; init; }
    public double Position { get; init; }

    public static ChecklistItemDto From(ChecklistItem c) => new()
    {
        Id = c.Id, Text = c.Text, Done = c.Done, Position = c.Position
    };
}

// ─── Taxonomia e descrição ─────────────────────────────────────────────────

/// <summary>Define tipo, pontos e o conjunto exato de tags da tarefa.</summary>
public record SetTaxonomyCommand(
    Guid WorkItemId, Guid? TaskTypeId, int? Points, IReadOnlyList<Guid> TagIds, string ActorId) : IRequest;

public class SetTaxonomyCommandHandler : IRequestHandler<SetTaxonomyCommand>
{
    private readonly IWorkItemDetailsRepository _details;
    private readonly IWorkItemAccessService _access;

    public SetTaxonomyCommandHandler(IWorkItemDetailsRepository details, IWorkItemAccessService access)
        => (_details, _access) = (details, access);

    public async Task Handle(SetTaxonomyCommand request, CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(request.WorkItemId, request.ActorId,
            PlatformPermission.Edit, ProjectRole.Member, cancellationToken);
        var item = await _details.GetWithTagsAsync(request.WorkItemId, cancellationToken)
            ?? throw new NaoEncontradoException("Tarefa");
        item.DefinirTaxonomia(request.TaskTypeId, request.Points, request.TagIds);
        await _details.SaveAsync(cancellationToken);
    }
}

/// <summary>Define a descrição da tarefa.</summary>
public record SetDescriptionCommand(Guid WorkItemId, string? Description, string ActorId) : IRequest;

public class SetDescriptionCommandHandler : IRequestHandler<SetDescriptionCommand>
{
    private readonly IWorkItemDetailsRepository _details;
    private readonly IWorkItemAccessService _access;

    public SetDescriptionCommandHandler(IWorkItemDetailsRepository details, IWorkItemAccessService access)
        => (_details, _access) = (details, access);

    public async Task Handle(SetDescriptionCommand request, CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(request.WorkItemId, request.ActorId,
            PlatformPermission.Edit, ProjectRole.Member, cancellationToken);
        var item = await _details.GetAsync(request.WorkItemId, cancellationToken)
            ?? throw new NaoEncontradoException("Tarefa");
        item.DefinirDescricao(request.Description);
        await _details.SaveAsync(cancellationToken);
    }
}

// ─── Checklist ──────────────────────────────────────────────────────────────

/// <summary>Checklist da tarefa em ordem de posição.</summary>
public record GetChecklistQuery(Guid WorkItemId, string ActorId) : IRequest<IReadOnlyList<ChecklistItemDto>>;

public class GetChecklistQueryHandler : IRequestHandler<GetChecklistQuery, IReadOnlyList<ChecklistItemDto>>
{
    private readonly IWorkItemDetailsRepository _details;
    private readonly IWorkItemAccessService _access;

    public GetChecklistQueryHandler(IWorkItemDetailsRepository details, IWorkItemAccessService access)
        => (_details, _access) = (details, access);

    public async Task<IReadOnlyList<ChecklistItemDto>> Handle(GetChecklistQuery request, CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(request.WorkItemId, request.ActorId,
            PlatformPermission.View, ProjectRole.Viewer, cancellationToken);
        var items = await _details.GetChecklistAsync(request.WorkItemId, cancellationToken);
        return items.Select(ChecklistItemDto.From).ToList();
    }
}

/// <summary>Adiciona item no fim do checklist.</summary>
public record AddChecklistItemCommand(Guid WorkItemId, string Text, string ActorId) : IRequest<ChecklistItemDto>;

public class AddChecklistItemCommandHandler : IRequestHandler<AddChecklistItemCommand, ChecklistItemDto>
{
    private readonly IWorkItemDetailsRepository _details;
    private readonly IWorkItemAccessService _access;

    public AddChecklistItemCommandHandler(IWorkItemDetailsRepository details, IWorkItemAccessService access)
        => (_details, _access) = (details, access);

    public async Task<ChecklistItemDto> Handle(AddChecklistItemCommand request, CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(request.WorkItemId, request.ActorId,
            PlatformPermission.Edit, ProjectRole.Member, cancellationToken);
        if (!await _details.WorkItemExistsAsync(request.WorkItemId, cancellationToken))
            throw new NaoEncontradoException("Tarefa");

        var maxPos = await _details.GetMaxChecklistPositionAsync(request.WorkItemId, cancellationToken);
        var item = ChecklistItem.Criar(request.WorkItemId, request.Text, maxPos);
        await _details.AddChecklistItemAsync(item, cancellationToken);
        return ChecklistItemDto.From(item);
    }
}

/// <summary>Marca/desmarca um item do checklist.</summary>
public record ToggleChecklistItemCommand(Guid WorkItemId, Guid ItemId, bool Done, string ActorId) : IRequest;

public class ToggleChecklistItemCommandHandler : IRequestHandler<ToggleChecklistItemCommand>
{
    private readonly IWorkItemDetailsRepository _details;
    private readonly IWorkItemAccessService _access;

    public ToggleChecklistItemCommandHandler(IWorkItemDetailsRepository details, IWorkItemAccessService access)
        => (_details, _access) = (details, access);

    public async Task Handle(ToggleChecklistItemCommand request, CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(request.WorkItemId, request.ActorId,
            PlatformPermission.Edit, ProjectRole.Member, cancellationToken);
        var item = await _details.GetChecklistItemAsync(request.WorkItemId, request.ItemId, cancellationToken)
            ?? throw new NaoEncontradoException("Item do checklist");
        item.Marcar(request.Done);
        await _details.SaveAsync(cancellationToken);
    }
}

/// <summary>Remove um item do checklist.</summary>
public record DeleteChecklistItemCommand(Guid WorkItemId, Guid ItemId, string ActorId) : IRequest;

public class DeleteChecklistItemCommandHandler : IRequestHandler<DeleteChecklistItemCommand>
{
    private readonly IWorkItemDetailsRepository _details;
    private readonly IWorkItemAccessService _access;

    public DeleteChecklistItemCommandHandler(IWorkItemDetailsRepository details, IWorkItemAccessService access)
        => (_details, _access) = (details, access);

    public async Task Handle(DeleteChecklistItemCommand request, CancellationToken cancellationToken)
    {
        await _access.EnsureAsync(request.WorkItemId, request.ActorId,
            PlatformPermission.Edit, ProjectRole.Member, cancellationToken);
        var item = await _details.GetChecklistItemAsync(request.WorkItemId, request.ItemId, cancellationToken)
            ?? throw new NaoEncontradoException("Item do checklist");
        await _details.DeleteChecklistItemAsync(item, cancellationToken);
    }
}
