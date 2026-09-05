using Detran.Kanban.Application.Common.Exceptions;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Enums;
using MediatR;

namespace Detran.Kanban.Application.Features.Company;

/// <summary>Projeto (quadro) na galeria da Empresa.</summary>
public class ProjectSummaryDto
{
    public Guid Id { get; init; }
    public Guid? ProjectId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Guid? ClientId { get; init; }
    public string? ClientName { get; init; }
    public int TasksTotal { get; init; }
    public int TasksDone { get; init; }
    public int Progress { get; init; }
    public double TotalHours { get; init; }
}

/// <summary>Galeria de projetos com cliente, progresso e horas.</summary>
public record GetCompanyProjectsQuery(string ActorId) : IRequest<IReadOnlyList<ProjectSummaryDto>>;

public class GetCompanyProjectsQueryHandler
    : IRequestHandler<GetCompanyProjectsQuery, IReadOnlyList<ProjectSummaryDto>>
{
    private readonly ICompanyQueries _queries;
    private readonly IProjectAccessService _projectAccess;
    private readonly IPermissionService _permissions;

    public GetCompanyProjectsQueryHandler(
        ICompanyQueries queries,
        IProjectAccessService projectAccess,
        IPermissionService permissions)
        => (_queries, _projectAccess, _permissions) = (queries, projectAccess, permissions);

    public async Task<IReadOnlyList<ProjectSummaryDto>> Handle(GetCompanyProjectsQuery request, CancellationToken ct)
    {
        await _permissions.EnsureAsync(request.ActorId, PlatformPermission.View,
            PermissionScope.Organization, cancellationToken: ct);
        var items = await _queries.GetProjectsAsync(ct);
        var accessible = await _projectAccess.GetAccessibleProjectIdsAsync(
            items.Where(x => x.ProjectId.HasValue).Select(x => x.ProjectId!.Value), request.ActorId, ct);
        return items.Where(x => x.ProjectId.HasValue && accessible.Contains(x.ProjectId.Value)).ToList();
    }
}

/// <summary>Vincula cliente e descrição a um projeto.</summary>
public record UpdateProjectCommand(
    Guid BoardId, Guid? ClientId, string? Description, string ActorId) : IRequest;

public class UpdateProjectCommandHandler : IRequestHandler<UpdateProjectCommand>
{
    private readonly IBoardRepository _boards;
    private readonly IBoardAccessService _access;

    public UpdateProjectCommandHandler(IBoardRepository boards, IBoardAccessService access)
        => (_boards, _access) = (boards, access);

    public async Task Handle(UpdateProjectCommand request, CancellationToken ct)
    {
        await _access.EnsureAsync(request.BoardId, request.ActorId,
            PlatformPermission.Edit, ProjectRole.ProjectAdmin, ct);
        var board = await _boards.GetByIdAsync(request.BoardId, ct)
            ?? throw new NaoEncontradoException("Projeto");
        board.VincularCliente(request.ClientId, request.Description);
        await _boards.UpdateAsync(board, ct);
    }
}
