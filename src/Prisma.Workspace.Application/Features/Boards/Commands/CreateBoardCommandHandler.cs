using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using MediatR;

namespace Prisma.Workspace.Application.Features.Boards.Commands;

/// <summary>
/// Handler do comando CreateBoard.
/// Cria um novo quadro, adiciona automaticamente a etapa Backlog
/// e define como quadro padrão do projeto quando não há nenhum.
/// </summary>
public class CreateBoardCommandHandler : IRequestHandler<CreateBoardCommand, Guid>
{
    private readonly IBoardRepository _boardRepository;
    private readonly IStageRepository _stageRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectAccessService _projectAccess;
    private readonly IPermissionService? _permissions;

    public CreateBoardCommandHandler(
        IBoardRepository boardRepository,
        IStageRepository stageRepository,
        IProjectRepository projectRepository,
        IProjectAccessService projectAccess,
        IPermissionService? permissions = null)
    {
        _boardRepository = boardRepository;
        _stageRepository = stageRepository;
        _projectRepository = projectRepository;
        _projectAccess = projectAccess;
        _permissions = permissions;
    }

    public async Task<Guid> Handle(CreateBoardCommand request, CancellationToken cancellationToken)
    {
        if (_permissions is not null)
            await _permissions.EnsureAsync(
                request.OwnerId,
                PlatformPermission.Create,
                request.ProjectId.HasValue ? PermissionScope.Project : PermissionScope.Organization,
                request.ProjectId,
                cancellationToken);
        if (request.ProjectId.HasValue)
            await _projectAccess.EnsureAtLeastAsync(request.ProjectId.Value, request.OwnerId,
                ProjectRole.ProjectAdmin, cancellationToken);

        var board = new Board
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            OwnerId = request.OwnerId,
            ProjectId = request.ProjectId,
            TeamId = request.TeamId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _boardRepository.AddAsync(board, cancellationToken);

        // Etapa Backlog criada automaticamente — ponto de entrada de novos itens
        var backlog = new Stage
        {
            Id = Guid.NewGuid(),
            BoardId = board.Id,
            Name = "Backlog",
            Position = 100,
            Category = StageCategory.Ready,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _stageRepository.AddAsync(backlog, cancellationToken);

        // Define quadro padrão do projeto quando ainda não há nenhum
        if (request.ProjectId.HasValue)
        {
            var project = await _projectRepository.GetByIdAsync(request.ProjectId.Value, cancellationToken);
            if (project is not null && project.DefaultBoardId is null)
            {
                project.DefaultBoardId = board.Id;
                await _projectRepository.SaveAsync(cancellationToken);
            }
        }

        return board.Id;
    }
}
