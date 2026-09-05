using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Enums;
using MediatR;

namespace Detran.Kanban.Application.Features.Users;

public record UserDto(string Id, string? Email, string? UserName, string DisplayName);

/// <summary>Usuários que podem ser atribuídos a tarefas (todos, por enquanto).</summary>
public record GetAssignableUsersQuery(string ActorId) : IRequest<IReadOnlyList<UserDto>>;

public class GetAssignableUsersQueryHandler : IRequestHandler<GetAssignableUsersQuery, IReadOnlyList<UserDto>>
{
    private readonly IUserDirectory _users;
    private readonly IPermissionService _permissions;

    public GetAssignableUsersQueryHandler(IUserDirectory users, IPermissionService permissions)
        => (_users, _permissions) = (users, permissions);

    public async Task<IReadOnlyList<UserDto>> Handle(GetAssignableUsersQuery request, CancellationToken ct)
    {
        await _permissions.EnsureAsync(request.ActorId, PlatformPermission.View,
            PermissionScope.Organization, cancellationToken: ct);
        var users = await _users.GetAllAsync(ct);
        return users.Select(u => new UserDto(u.Id, u.Email, u.UserName, u.DisplayName)).ToList();
    }
}
