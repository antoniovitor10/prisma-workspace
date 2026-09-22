using MediatR;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Application.Features.Boards;

public record UpdateBoardStageCommand(Guid StageId, string Name, StageCategory? Category, string? Color, bool ConfirmCategoryChange, string ActorId) : IRequest;
public record ReorderBoardStagesCommand(Guid BoardId, IReadOnlyList<Guid> StageIds, string ActorId) : IRequest;
public record RemoveBoardStageCommand(Guid StageId, Guid? DestinationStageId, string ActorId) : IRequest;
public record RemoveBoardCommand(Guid BoardId, Guid? DestinationBoardId, Guid? DestinationStageId, string ActorId) : IRequest;
public record TransferBoardWorkItemCommand(Guid WorkItemId, Guid DestinationBoardId, Guid DestinationStageId, string ActorId) : IRequest;

public sealed class BoardStructureHandler :
    IRequestHandler<UpdateBoardStageCommand>, IRequestHandler<ReorderBoardStagesCommand>, IRequestHandler<RemoveBoardStageCommand>,
    IRequestHandler<RemoveBoardCommand>, IRequestHandler<TransferBoardWorkItemCommand>
{
    private readonly IBoardStructureRepository _structure;
    private readonly IBoardAccessService _access;
    private readonly IStageRepository _stages;
    private readonly IWorkItemRepository _items;

    public BoardStructureHandler(IBoardStructureRepository structure, IBoardAccessService access,
        IStageRepository stages, IWorkItemRepository items)
        => (_structure, _access, _stages, _items) = (structure, access, stages, items);

    public async Task Handle(UpdateBoardStageCommand request, CancellationToken ct)
    {
        var stage = await _stages.GetByIdAsync(request.StageId, ct) ?? throw new ArgumentException("Coluna não encontrada.");
        if (!stage.BoardId.HasValue) throw new ArgumentException("Colunas históricas não podem ser alteradas.");
        await _access.EnsureAsync(stage.BoardId.Value, request.ActorId, PlatformPermission.Edit, ProjectRole.ProjectAdmin, ct);
        await _structure.UpdateStageAsync(request.StageId, request.Name, request.Category, request.Color, request.ConfirmCategoryChange, request.ActorId, ct);
    }

    public async Task Handle(ReorderBoardStagesCommand request, CancellationToken ct)
    {
        await _access.EnsureAsync(request.BoardId, request.ActorId, PlatformPermission.Edit, ProjectRole.ProjectAdmin, ct);
        await _structure.ReorderAsync(request.BoardId, request.StageIds, ct);
    }

    public async Task Handle(RemoveBoardStageCommand request, CancellationToken ct)
    {
        var stage = await _stages.GetByIdAsync(request.StageId, ct)
            ?? throw new ArgumentException("Coluna não encontrada.");
        if (!stage.BoardId.HasValue) throw new ArgumentException("Colunas históricas não podem ser excluídas.");
        await _access.EnsureAsync(stage.BoardId.Value, request.ActorId, PlatformPermission.Delete, ProjectRole.ProjectAdmin, ct);
        await _structure.DeleteStageAsync(request.StageId, request.DestinationStageId, request.ActorId, ct);
    }

    public async Task Handle(RemoveBoardCommand request, CancellationToken ct)
    {
        await _access.EnsureAsync(request.BoardId, request.ActorId, PlatformPermission.Delete, ProjectRole.ProjectAdmin, ct);
        if (request.DestinationBoardId.HasValue)
            await _access.EnsureAsync(request.DestinationBoardId.Value, request.ActorId, PlatformPermission.ChangeStatus, ProjectRole.Member, ct);
        await _structure.DeleteBoardAsync(request.BoardId, request.DestinationBoardId, request.DestinationStageId, request.ActorId, ct);
    }

    public async Task Handle(TransferBoardWorkItemCommand request, CancellationToken ct)
    {
        var item = await _items.GetByIdAsync(request.WorkItemId, ct)
            ?? throw new ArgumentException("Tarefa não encontrada.");
        await _access.EnsureAsync(item.BoardId, request.ActorId, PlatformPermission.ChangeStatus, ProjectRole.Member, ct);
        await _access.EnsureAsync(request.DestinationBoardId, request.ActorId, PlatformPermission.ChangeStatus, ProjectRole.Member, ct);
        await _structure.TransferAsync(request.WorkItemId, request.DestinationBoardId, request.DestinationStageId, request.ActorId, ct);
    }
}
