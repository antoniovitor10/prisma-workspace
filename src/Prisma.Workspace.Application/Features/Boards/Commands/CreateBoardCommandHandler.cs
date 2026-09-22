using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using MediatR;

namespace Prisma.Workspace.Application.Features.Boards.Commands;

/// <summary>
/// Handler do comando CreateBoard.
/// Cria um quadro do projeto com colunas próprias (D89).
/// </summary>
public class CreateBoardCommandHandler : IRequestHandler<CreateBoardCommand, Guid>
{
    private readonly IBoardRepository _boardRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectAccessService _projectAccess;
    private readonly IPermissionService? _permissions;
    private readonly IStageRepository? _stages;

    public CreateBoardCommandHandler(
        IBoardRepository boardRepository,
        IProjectRepository projectRepository,
        IProjectAccessService projectAccess,
        IPermissionService? permissions = null, IStageRepository? stages = null)
    {
        _boardRepository = boardRepository;
        _projectRepository = projectRepository;
        _projectAccess = projectAccess;
        _permissions = permissions;
        _stages = stages;
    }

    public async Task<Guid> Handle(CreateBoardCommand request, CancellationToken cancellationToken)
    {
        if (_permissions is not null)
            await _permissions.EnsureAsync(
                request.OwnerId,
                PlatformPermission.Create,
                PermissionScope.Project,
                request.ProjectId,
                cancellationToken);

        await _projectAccess.EnsureAtLeastAsync(
            request.ProjectId, request.OwnerId, ProjectRole.ProjectAdmin, cancellationToken);

        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new ArgumentException("O projeto especificado não existe.");

        var template = request.CopyStagesFromBoardId.HasValue
            ? await _boardRepository.GetByIdAsync(request.CopyStagesFromBoardId.Value, cancellationToken)
            : null;
        if (request.CopyStagesFromBoardId.HasValue && template?.ProjectId != request.ProjectId)
            throw new ArgumentException("O quadro de origem deve pertencer ao mesmo projeto.");

        var board = new Board
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            OwnerId = request.OwnerId,
            OrganizationId = project.OrganizationId,
            ProjectId = request.ProjectId,
            TeamId = request.TeamId,
            CreatedAt = DateTimeOffset.UtcNow
        };

        if (_stages is not null)
        {
            var source = template is null
                ? Array.Empty<Stage>()
                : await _stages.GetByBoardIdAsync(template.Id, cancellationToken);
            var columns = template is not null ? source.Select(x => new Stage
            {
                Id = Guid.NewGuid(), ProjectId = request.ProjectId, BoardId = board.Id,
                Name = x.Name, Position = x.Position, Category = x.Category,
                WorkflowStatusId = x.WorkflowStatusId, CreatedAt = DateTimeOffset.UtcNow
            }) : new[]
            {
                new Stage { Id=Guid.NewGuid(), ProjectId=request.ProjectId, BoardId=board.Id, Name="A fazer", Position=100, Category=StageCategory.Backlog, CreatedAt=DateTimeOffset.UtcNow },
                new Stage { Id=Guid.NewGuid(), ProjectId=request.ProjectId, BoardId=board.Id, Name="Em andamento", Position=200, Category=StageCategory.InProgress, CreatedAt=DateTimeOffset.UtcNow },
                new Stage { Id=Guid.NewGuid(), ProjectId=request.ProjectId, BoardId=board.Id, Name="Concluído", Position=300, Category=StageCategory.Done, CreatedAt=DateTimeOffset.UtcNow }
            };
            foreach (var stage in columns) board.Stages.Add(stage);
        }

        await _boardRepository.AddAsync(board, cancellationToken);

        // Define quadro padrão do projeto quando ainda não há nenhum
        if (project.DefaultBoardId is null)
        {
            project.DefaultBoardId = board.Id;
            await _projectRepository.SaveAsync(cancellationToken);
        }

        return board.Id;
    }
}
