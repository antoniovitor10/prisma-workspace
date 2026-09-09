using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using MediatR;

namespace Prisma.Workspace.Application.Features.Boards.Commands;

/// <summary>
/// Handler do comando CreateBoard.
/// Cria uma visão salva do projeto; o fluxo (colunas) fica no projeto (D83).
/// </summary>
public class CreateBoardCommandHandler : IRequestHandler<CreateBoardCommand, Guid>
{
    private readonly IBoardRepository _boardRepository;
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectAccessService _projectAccess;
    private readonly IPermissionService? _permissions;

    public CreateBoardCommandHandler(
        IBoardRepository boardRepository,
        IProjectRepository projectRepository,
        IProjectAccessService projectAccess,
        IPermissionService? permissions = null)
    {
        _boardRepository = boardRepository;
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
                PermissionScope.Project,
                request.ProjectId,
                cancellationToken);

        await _projectAccess.EnsureAtLeastAsync(
            request.ProjectId, request.OwnerId, ProjectRole.ProjectAdmin, cancellationToken);

        var project = await _projectRepository.GetByIdAsync(request.ProjectId, cancellationToken)
            ?? throw new ArgumentException("O projeto especificado não existe.");

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
