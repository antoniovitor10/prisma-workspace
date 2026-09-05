using Detran.Kanban.Application.Common.Exceptions;
using Detran.Kanban.Application.Interfaces;
using Detran.Kanban.Domain.Entities;
using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Detran.Kanban.Application.Features.Teams;

public class TeamDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public string? LeaderId { get; init; }
    public string? LeaderName { get; init; }
    public decimal DefaultWeeklyCapacityHours { get; init; }
    public decimal TotalWeeklyCapacityHours { get; init; }
    public IReadOnlyList<Guid> ProjectIds { get; init; } = Array.Empty<Guid>();
    public IReadOnlyList<TeamMemberDto> Members { get; init; } = Array.Empty<TeamMemberDto>();
}

public class TeamMemberDto
{
    public string UserId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public decimal WeeklyCapacityHours { get; init; }
    public double WeekHours { get; init; }
    public bool IsLeader { get; init; }
}

public record GetTeamsQuery(string ActorId) : IRequest<IReadOnlyList<TeamDto>>;

public class GetTeamsQueryHandler : IRequestHandler<GetTeamsQuery, IReadOnlyList<TeamDto>>
{
    private readonly ITeamRepository _teams;
    private readonly ITimeEntryRepository _timeEntries;
    private readonly IUserDirectory _users;
    private readonly IPermissionService _permissions;

    public GetTeamsQueryHandler(
        ITeamRepository teams, ITimeEntryRepository timeEntries,
        IUserDirectory users, IPermissionService permissions)
        => (_teams, _timeEntries, _users, _permissions) = (teams, timeEntries, users, permissions);

    public async Task<IReadOnlyList<TeamDto>> Handle(GetTeamsQuery request, CancellationToken ct)
    {
        var allTeams = await _teams.GetAllWithMembersAsync(ct);
        var visible = new List<Team>();
        foreach (var team in allTeams)
            if (await _permissions.HasAsync(request.ActorId, PlatformPermission.View,
                    PermissionScope.Team, team.Id, ct))
                visible.Add(team);

        var userIds = visible.SelectMany(t => t.Members.Select(m => m.UserId)).Distinct().ToList();
        var names = await _users.GetDisplayNamesAsync(userIds, ct);
        var today = DateTimeOffset.UtcNow.Date;
        var dayOffset = ((int)today.DayOfWeek + 6) % 7;
        var weekStart = new DateTimeOffset(today.AddDays(-dayOffset), TimeSpan.Zero);
        var entries = await _timeEntries.GetByUsersInRangeAsync(
            userIds, weekStart, weekStart.AddDays(7), ct);
        var now = DateTimeOffset.UtcNow;

        double WeekSeconds(string userId) => entries
            .Where(e => e.UserId == userId)
            .Sum(e => Math.Max(0, ((e.EndedAt ?? now) - e.StartedAt).TotalSeconds));

        return visible.Select(team => new TeamDto
        {
            Id = team.Id,
            Name = team.Name,
            IsActive = team.IsActive,
            LeaderId = team.LeaderId,
            LeaderName = team.LeaderId is null ? null : names.GetValueOrDefault(team.LeaderId, team.LeaderId),
            DefaultWeeklyCapacityHours = team.DefaultWeeklyCapacityHours,
            TotalWeeklyCapacityHours = team.Members.Sum(x => x.WeeklyCapacityHours),
            ProjectIds = team.Projects.Select(x => x.ProjectId).ToList(),
            Members = team.Members.Select(member => new TeamMemberDto
            {
                UserId = member.UserId,
                Name = names.GetValueOrDefault(member.UserId, member.UserId),
                WeeklyCapacityHours = member.WeeklyCapacityHours,
                WeekHours = Math.Round(WeekSeconds(member.UserId) / 3600.0, 1),
                IsLeader = member.UserId == team.LeaderId
            }).OrderByDescending(x => x.IsLeader).ThenBy(x => x.Name).ToList()
        }).ToList();
    }
}

public record CreateTeamCommand(
    string Name, decimal? DefaultWeeklyCapacityHours, string ActorId) : IRequest<TeamDto>;

public class CreateTeamCommandValidator : AbstractValidator<CreateTeamCommand>
{
    public CreateTeamCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DefaultWeeklyCapacityHours)
            .InclusiveBetween(1, 168).When(x => x.DefaultWeeklyCapacityHours.HasValue);
    }
}

public class CreateTeamCommandHandler : IRequestHandler<CreateTeamCommand, TeamDto>
{
    private readonly ITeamRepository _teams;
    private readonly IPermissionService _permissions;

    public CreateTeamCommandHandler(ITeamRepository teams, IPermissionService permissions)
        => (_teams, _permissions) = (teams, permissions);

    public async Task<TeamDto> Handle(CreateTeamCommand request, CancellationToken ct)
    {
        await _permissions.EnsureAsync(request.ActorId, PlatformPermission.Create,
            PermissionScope.Organization, cancellationToken: ct);
        DomainException.Garantir(!await _teams.NameExistsAsync(request.Name.Trim(), cancellationToken: ct),
            "Já existe uma equipe com este nome.");
        var team = Team.Criar(request.Name, request.DefaultWeeklyCapacityHours);
        await _teams.AddAsync(team, ct);
        return new TeamDto
        {
            Id = team.Id,
            Name = team.Name,
            IsActive = team.IsActive,
            DefaultWeeklyCapacityHours = team.DefaultWeeklyCapacityHours
        };
    }
}

public record UpdateTeamCommand(
    Guid Id, string Name, string? LeaderId,
    decimal DefaultWeeklyCapacityHours, string ActorId) : IRequest;

public class UpdateTeamCommandValidator : AbstractValidator<UpdateTeamCommand>
{
    public UpdateTeamCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(150);
        RuleFor(x => x.DefaultWeeklyCapacityHours).InclusiveBetween(1, 168);
    }
}

public class UpdateTeamCommandHandler : IRequestHandler<UpdateTeamCommand>
{
    private readonly ITeamRepository _teams;
    private readonly IPermissionService _permissions;

    public UpdateTeamCommandHandler(ITeamRepository teams, IPermissionService permissions)
        => (_teams, _permissions) = (teams, permissions);

    public async Task Handle(UpdateTeamCommand request, CancellationToken ct)
    {
        await _permissions.EnsureAsync(request.ActorId, PlatformPermission.Edit,
            PermissionScope.Team, request.Id, ct);
        var team = await _teams.GetByIdWithMembersAsync(request.Id, ct)
            ?? throw new NaoEncontradoException("Equipe");
        DomainException.Garantir(!await _teams.NameExistsAsync(request.Name.Trim(), request.Id, ct),
            "Já existe uma equipe com este nome.");
        team.Editar(request.Name, request.LeaderId, request.DefaultWeeklyCapacityHours);
        await _teams.SaveAsync(ct);
    }
}

public record SetTeamActiveCommand(Guid Id, bool IsActive, string ActorId) : IRequest;

public class SetTeamActiveCommandHandler : IRequestHandler<SetTeamActiveCommand>
{
    private readonly ITeamRepository _teams;
    private readonly IPermissionService _permissions;

    public SetTeamActiveCommandHandler(ITeamRepository teams, IPermissionService permissions)
        => (_teams, _permissions) = (teams, permissions);

    public async Task Handle(SetTeamActiveCommand request, CancellationToken ct)
    {
        await _permissions.EnsureAsync(request.ActorId, PlatformPermission.Delete,
            PermissionScope.Team, request.Id, ct);
        var team = await _teams.GetByIdAsync(request.Id, ct)
            ?? throw new NaoEncontradoException("Equipe");
        team.DefinirAtiva(request.IsActive);
        await _teams.SaveAsync(ct);
    }
}

public record AddTeamMemberCommand(
    Guid TeamId, string UserId, decimal? WeeklyCapacityHours, string ActorId) : IRequest;

public class AddTeamMemberCommandValidator : AbstractValidator<AddTeamMemberCommand>
{
    public AddTeamMemberCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.WeeklyCapacityHours).InclusiveBetween(1, 168)
            .When(x => x.WeeklyCapacityHours.HasValue);
    }
}

public class AddTeamMemberCommandHandler : IRequestHandler<AddTeamMemberCommand>
{
    private readonly ITeamRepository _teams;
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationContext _context;
    private readonly IPermissionService _permissions;

    public AddTeamMemberCommandHandler(
        ITeamRepository teams, IOrganizationRepository organizations,
        IOrganizationContext context, IPermissionService permissions)
        => (_teams, _organizations, _context, _permissions) = (teams, organizations, context, permissions);

    public async Task Handle(AddTeamMemberCommand request, CancellationToken ct)
    {
        await _permissions.EnsureAsync(request.ActorId, PlatformPermission.ManageMembers,
            PermissionScope.Team, request.TeamId, ct);
        var organizationMember = await _organizations.GetMemberAsync(
            _context.RequireOrganizationId(), request.UserId, ct);
        DomainException.Garantir(organizationMember?.IsActive == true,
            "Somente membros ativos da organização podem entrar na equipe.");
        var team = await _teams.GetByIdWithMembersAsync(request.TeamId, ct)
            ?? throw new NaoEncontradoException("Equipe");
        DomainException.Garantir(team.IsActive, "A equipe está desativada.");
        team.AdicionarMembro(request.UserId, request.WeeklyCapacityHours);
        await _teams.SaveAsync(ct);
    }
}

public record RemoveTeamMemberCommand(Guid TeamId, string UserId, string ActorId) : IRequest;

public class RemoveTeamMemberCommandHandler : IRequestHandler<RemoveTeamMemberCommand>
{
    private readonly ITeamRepository _teams;
    private readonly IPermissionService _permissions;

    public RemoveTeamMemberCommandHandler(ITeamRepository teams, IPermissionService permissions)
        => (_teams, _permissions) = (teams, permissions);

    public async Task Handle(RemoveTeamMemberCommand request, CancellationToken ct)
    {
        await _permissions.EnsureAsync(request.ActorId, PlatformPermission.ManageMembers,
            PermissionScope.Team, request.TeamId, ct);
        var team = await _teams.GetByIdWithMembersAsync(request.TeamId, ct)
            ?? throw new NaoEncontradoException("Equipe");
        var member = team.Members.FirstOrDefault(x => x.UserId == request.UserId)
            ?? throw new NaoEncontradoException("Membro");
        if (team.LeaderId == request.UserId)
            team.LeaderId = null;
        team.Members.Remove(member);
        await _teams.SaveAsync(ct);
    }
}

public record UpdateTeamMemberCapacityCommand(
    Guid TeamId, string UserId, decimal WeeklyCapacityHours, string ActorId) : IRequest;

public class UpdateTeamMemberCapacityCommandValidator : AbstractValidator<UpdateTeamMemberCapacityCommand>
{
    public UpdateTeamMemberCapacityCommandValidator()
        => RuleFor(x => x.WeeklyCapacityHours).InclusiveBetween(1, 168);
}

public class UpdateTeamMemberCapacityCommandHandler : IRequestHandler<UpdateTeamMemberCapacityCommand>
{
    private readonly ITeamRepository _teams;
    private readonly IPermissionService _permissions;

    public UpdateTeamMemberCapacityCommandHandler(ITeamRepository teams, IPermissionService permissions)
        => (_teams, _permissions) = (teams, permissions);

    public async Task Handle(UpdateTeamMemberCapacityCommand request, CancellationToken ct)
    {
        await _permissions.EnsureAsync(request.ActorId, PlatformPermission.Edit,
            PermissionScope.Team, request.TeamId, ct);
        var team = await _teams.GetByIdWithMembersAsync(request.TeamId, ct)
            ?? throw new NaoEncontradoException("Equipe");
        team.AtualizarCapacidade(request.UserId, request.WeeklyCapacityHours);
        await _teams.SaveAsync(ct);
    }
}
