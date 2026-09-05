using Detran.Kanban.Application.Common.Exceptions;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using MediatR;

namespace Detran.Kanban.Application.Features.Catalogo;

// Catálogos simples da Empresa: Tags, Tipos de tarefa e Clientes.

public record CatalogItemDto(Guid Id, string Name, string Color);
public record ClientDto(Guid Id, string Name, int BoardsCount);

// ─── Tags ───────────────────────────────────────────────────────────────────

public record GetTagsQuery(string ActorId) : IRequest<IReadOnlyList<CatalogItemDto>>;
public record CreateTagCommand(string Name, string? Color, string ActorId) : IRequest<CatalogItemDto>;
public record DeleteTagCommand(Guid Id, string ActorId) : IRequest;

public class GetTagsQueryHandler : IRequestHandler<GetTagsQuery, IReadOnlyList<CatalogItemDto>>
{
    private readonly ITagRepository _tags;
    private readonly IPermissionService _permissions;
    public GetTagsQueryHandler(ITagRepository tags, IPermissionService permissions)
        => (_tags, _permissions) = (tags, permissions);

    public async Task<IReadOnlyList<CatalogItemDto>> Handle(GetTagsQuery request, CancellationToken ct)
    {
        await CatalogAccess.EnsureAsync(_permissions, request.ActorId, PlatformPermission.View, ct);
        return (await _tags.GetAllAsync(ct)).Select(t => new CatalogItemDto(t.Id, t.Name, t.Color)).ToList();
    }
}

public class CreateTagCommandHandler : IRequestHandler<CreateTagCommand, CatalogItemDto>
{
    private readonly ITagRepository _tags;
    private readonly IPermissionService _permissions;
    public CreateTagCommandHandler(ITagRepository tags, IPermissionService permissions)
        => (_tags, _permissions) = (tags, permissions);

    public async Task<CatalogItemDto> Handle(CreateTagCommand request, CancellationToken ct)
    {
        await CatalogAccess.EnsureAsync(_permissions, request.ActorId, PlatformPermission.Create, ct);
        var tag = Tag.Criar(request.Name, request.Color);
        await _tags.AddAsync(tag, ct);
        return new CatalogItemDto(tag.Id, tag.Name, tag.Color);
    }
}

public class DeleteTagCommandHandler : IRequestHandler<DeleteTagCommand>
{
    private readonly ITagRepository _tags;
    private readonly IPermissionService _permissions;
    public DeleteTagCommandHandler(ITagRepository tags, IPermissionService permissions)
        => (_tags, _permissions) = (tags, permissions);

    public async Task Handle(DeleteTagCommand request, CancellationToken ct)
    {
        await CatalogAccess.EnsureAsync(_permissions, request.ActorId, PlatformPermission.Delete, ct);
        var tag = await _tags.GetByIdAsync(request.Id, ct) ?? throw new NaoEncontradoException("Tag");
        await _tags.DeleteAsync(tag, ct);
    }
}

// ─── Tipos de tarefa ────────────────────────────────────────────────────────

public record GetTaskTypesQuery(string ActorId) : IRequest<IReadOnlyList<CatalogItemDto>>;
public record CreateTaskTypeCommand(string Name, string? Color, string ActorId) : IRequest<CatalogItemDto>;
public record DeleteTaskTypeCommand(Guid Id, string ActorId) : IRequest;

public class GetTaskTypesQueryHandler : IRequestHandler<GetTaskTypesQuery, IReadOnlyList<CatalogItemDto>>
{
    private readonly ITaskTypeRepository _types;
    private readonly IPermissionService _permissions;
    public GetTaskTypesQueryHandler(ITaskTypeRepository types, IPermissionService permissions)
        => (_types, _permissions) = (types, permissions);

    public async Task<IReadOnlyList<CatalogItemDto>> Handle(GetTaskTypesQuery request, CancellationToken ct)
    {
        await CatalogAccess.EnsureAsync(_permissions, request.ActorId, PlatformPermission.View, ct);
        return (await _types.GetAllAsync(ct)).Select(t => new CatalogItemDto(t.Id, t.Name, t.Color)).ToList();
    }
}

public class CreateTaskTypeCommandHandler : IRequestHandler<CreateTaskTypeCommand, CatalogItemDto>
{
    private readonly ITaskTypeRepository _types;
    private readonly IPermissionService _permissions;
    public CreateTaskTypeCommandHandler(ITaskTypeRepository types, IPermissionService permissions)
        => (_types, _permissions) = (types, permissions);

    public async Task<CatalogItemDto> Handle(CreateTaskTypeCommand request, CancellationToken ct)
    {
        await CatalogAccess.EnsureAsync(_permissions, request.ActorId, PlatformPermission.Create, ct);
        var tipo = TaskType.Criar(request.Name, request.Color);
        await _types.AddAsync(tipo, ct);
        return new CatalogItemDto(tipo.Id, tipo.Name, tipo.Color);
    }
}

public class DeleteTaskTypeCommandHandler : IRequestHandler<DeleteTaskTypeCommand>
{
    private readonly ITaskTypeRepository _types;
    private readonly IPermissionService _permissions;
    public DeleteTaskTypeCommandHandler(ITaskTypeRepository types, IPermissionService permissions)
        => (_types, _permissions) = (types, permissions);

    public async Task Handle(DeleteTaskTypeCommand request, CancellationToken ct)
    {
        await CatalogAccess.EnsureAsync(_permissions, request.ActorId, PlatformPermission.Delete, ct);
        var tipo = await _types.GetByIdAsync(request.Id, ct) ?? throw new NaoEncontradoException("Tipo de tarefa");
        await _types.DeleteAsync(tipo, ct);
    }
}

// ─── Clientes ───────────────────────────────────────────────────────────────

public record GetClientsQuery(string ActorId) : IRequest<IReadOnlyList<ClientDto>>;
public record CreateClientCommand(string Name, string ActorId) : IRequest<ClientDto>;
public record DeleteClientCommand(Guid Id, string ActorId) : IRequest;

public class GetClientsQueryHandler : IRequestHandler<GetClientsQuery, IReadOnlyList<ClientDto>>
{
    private readonly IClientRepository _clients;
    private readonly IPermissionService _permissions;
    public GetClientsQueryHandler(IClientRepository clients, IPermissionService permissions)
        => (_clients, _permissions) = (clients, permissions);

    public async Task<IReadOnlyList<ClientDto>> Handle(GetClientsQuery request, CancellationToken ct)
    {
        await CatalogAccess.EnsureAsync(_permissions, request.ActorId, PlatformPermission.View, ct);
        return (await _clients.GetAllWithBoardCountAsync(ct))
            .Select(x => new ClientDto(x.Client.Id, x.Client.Name, x.BoardsCount)).ToList();
    }
}

public class CreateClientCommandHandler : IRequestHandler<CreateClientCommand, ClientDto>
{
    private readonly IClientRepository _clients;
    private readonly IPermissionService _permissions;
    public CreateClientCommandHandler(IClientRepository clients, IPermissionService permissions)
        => (_clients, _permissions) = (clients, permissions);

    public async Task<ClientDto> Handle(CreateClientCommand request, CancellationToken ct)
    {
        await CatalogAccess.EnsureAsync(_permissions, request.ActorId, PlatformPermission.Create, ct);
        var cliente = Client.Criar(request.Name);
        await _clients.AddAsync(cliente, ct);
        return new ClientDto(cliente.Id, cliente.Name, 0);
    }
}

public class DeleteClientCommandHandler : IRequestHandler<DeleteClientCommand>
{
    private readonly IClientRepository _clients;
    private readonly IPermissionService _permissions;
    public DeleteClientCommandHandler(IClientRepository clients, IPermissionService permissions)
        => (_clients, _permissions) = (clients, permissions);

    public async Task Handle(DeleteClientCommand request, CancellationToken ct)
    {
        await CatalogAccess.EnsureAsync(_permissions, request.ActorId, PlatformPermission.Delete, ct);
        var cliente = await _clients.GetByIdAsync(request.Id, ct) ?? throw new NaoEncontradoException("Cliente");
        await _clients.DeleteAsync(cliente, ct);
    }
}

internal static class CatalogAccess
{
    public static Task EnsureAsync(
        IPermissionService permissions,
        string actorId,
        PlatformPermission permission,
        CancellationToken ct)
        => permissions.EnsureAsync(
            actorId, permission, PermissionScope.Organization,
            cancellationToken: ct);
}
