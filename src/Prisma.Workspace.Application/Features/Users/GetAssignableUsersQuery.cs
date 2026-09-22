using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Enums;
using MediatR;

namespace Prisma.Workspace.Application.Features.Users;

public record UserDto(string Id, string? Email, string? UserName, string DisplayName);

/// <summary>Usuários que podem ser atribuídos a tarefas (todos, por enquanto).</summary>
public record GetAssignableUsersQuery(string ActorId, Guid? ProjectId = null) : IRequest<IReadOnlyList<UserDto>>;

public class GetAssignableUsersQueryHandler : IRequestHandler<GetAssignableUsersQuery, IReadOnlyList<UserDto>>
{
    private readonly IUserDirectory _users;
    private readonly IPermissionService _permissions;
    private readonly IProjectAccessService _projectAccess;

    public GetAssignableUsersQueryHandler(
        IUserDirectory users, IPermissionService permissions, IProjectAccessService projectAccess)
        => (_users, _permissions, _projectAccess) = (users, permissions, projectAccess);

    public async Task<IReadOnlyList<UserDto>> Handle(GetAssignableUsersQuery request, CancellationToken ct)
    {
        await _permissions.EnsureAsync(request.ActorId, PlatformPermission.View,
            PermissionScope.Organization, cancellationToken: ct);
        var users = await _users.GetAllAsync(ct);
        if (request.ProjectId.HasValue)
        {
            await _permissions.EnsureAsync(request.ActorId, PlatformPermission.Assign,
                PermissionScope.Project, request.ProjectId.Value, ct);
            await _projectAccess.EnsureAtLeastAsync(
                request.ProjectId.Value, request.ActorId, ProjectRole.Viewer, ct);
            var eligible = new List<UserDto>();
            foreach (var user in users)
            {
                if (await _projectAccess.GetRoleAsync(request.ProjectId.Value, user.Id, ct) is not null)
                    eligible.Add(new UserDto(user.Id, user.Email, user.UserName, user.DisplayName));
            }
            return eligible;
        }
        return users.Select(u => new UserDto(u.Id, u.Email, u.UserName, u.DisplayName)).ToList();
    }
}
