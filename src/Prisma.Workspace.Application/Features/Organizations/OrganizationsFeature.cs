using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Prisma.Workspace.Application.Common.Exceptions;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Authorization;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using FluentValidation;
using MediatR;

namespace Prisma.Workspace.Application.Features.Organizations;

public record OrganizationDto(
    Guid Id,
    string Name,
    string Slug,
    bool IsActive,
    string Locale,
    string TimeZone,
    DayOfWeek WeekStartDay,
    OrganizationRole Role,
    bool IsAdministrator);

public record OrganizationMemberDto(
    string UserId,
    string Name,
    string? DisplayName,
    string? Email,
    string? UserName,
    OrganizationRole Role,
    bool IsActive,
    DateTimeOffset JoinedAt);

public record OrganizationInvitationDto(
    Guid Id,
    string Email,
    OrganizationRole Role,
    DateTimeOffset ExpiresAt,
    string Token);

public record PermissionGrantDto(
    Guid Id,
    string UserId,
    PermissionScope Scope,
    Guid? ScopeId,
    PlatformPermission Permission,
    bool IsAllowed);

public record OrganizationAccessDto(
    OrganizationRole Role,
    IReadOnlyList<PlatformPermission> AllowedPermissions);

public record GetOrganizationsQuery(string UserId) : IRequest<IReadOnlyList<OrganizationDto>>;

public class GetOrganizationsQueryHandler : IRequestHandler<GetOrganizationsQuery, IReadOnlyList<OrganizationDto>>
{
    private readonly IOrganizationRepository _organizations;
    public GetOrganizationsQueryHandler(IOrganizationRepository organizations) => _organizations = organizations;

    public async Task<IReadOnlyList<OrganizationDto>> Handle(GetOrganizationsQuery request, CancellationToken ct)
    {
        var organizations = await _organizations.GetForUserAsync(request.UserId, ct);
        return organizations.Select(organization =>
        {
            var member = organization.Members.Single(m => m.UserId == request.UserId);
            return Map(organization, member);
        }).ToList();
    }

    internal static OrganizationDto Map(Organization organization, OrganizationMember member) => new(
        organization.Id, organization.Name, organization.Slug, organization.IsActive,
        organization.Locale, organization.TimeZone, organization.WeekStartDay,
        member.Role, member.Role == OrganizationRole.Administrator);
}

public record GetCurrentOrganizationQuery(string UserId) : IRequest<OrganizationDto>;

public class GetCurrentOrganizationQueryHandler : IRequestHandler<GetCurrentOrganizationQuery, OrganizationDto>
{
    private readonly IOrganizationContext _context;
    private readonly IOrganizationRepository _organizations;

    public GetCurrentOrganizationQueryHandler(IOrganizationContext context, IOrganizationRepository organizations)
        => (_context, _organizations) = (context, organizations);

    public async Task<OrganizationDto> Handle(GetCurrentOrganizationQuery request, CancellationToken ct)
    {
        var id = _context.RequireOrganizationId();
        var organization = await _organizations.GetByIdAsync(id, ct)
            ?? throw new NaoEncontradoException("Organização");
        var member = await _organizations.GetMemberAsync(id, request.UserId, ct)
            ?? throw new AcessoNegadoException();
        return GetOrganizationsQueryHandler.Map(organization, member);
    }
}

public record CreateOrganizationCommand(string Name, string? Slug, string UserId) : IRequest<OrganizationDto>;

public class CreateOrganizationCommandValidator : AbstractValidator<CreateOrganizationCommand>
{
    public CreateOrganizationCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Slug).MaximumLength(80);
    }
}

public class CreateOrganizationCommandHandler : IRequestHandler<CreateOrganizationCommand, OrganizationDto>
{
    private readonly IOrganizationRepository _organizations;
    public CreateOrganizationCommandHandler(IOrganizationRepository organizations) => _organizations = organizations;

    public async Task<OrganizationDto> Handle(CreateOrganizationCommand request, CancellationToken ct)
    {
        var baseSlug = Slugify(string.IsNullOrWhiteSpace(request.Slug) ? request.Name : request.Slug!);
        DomainException.Garantir(baseSlug.Length >= 2, "Identificador da organização inválido.");
        var slug = baseSlug;
        var suffix = 2;
        while (await _organizations.SlugExistsAsync(slug, ct))
            slug = $"{baseSlug}-{suffix++}";

        var organization = Organization.Create(request.Name, slug, request.UserId);
        await _organizations.AddAsync(organization, ct);
        return GetOrganizationsQueryHandler.Map(organization, organization.Members.Single());
    }

    private static string Slugify(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD);
        var withoutMarks = new string(normalized
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            .ToArray());
        return Regex.Replace(withoutMarks.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
    }
}

public record UpdateOrganizationCommand(
    string Name,
    string Locale,
    string TimeZone,
    DayOfWeek WeekStartDay,
    string ActorId) : IRequest<OrganizationDto>;

public class UpdateOrganizationCommandValidator : AbstractValidator<UpdateOrganizationCommand>
{
    public UpdateOrganizationCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Locale).NotEmpty().MaximumLength(20);
        RuleFor(x => x.TimeZone).NotEmpty().MaximumLength(80);
        RuleFor(x => x.WeekStartDay).IsInEnum();
    }
}

public class UpdateOrganizationCommandHandler : IRequestHandler<UpdateOrganizationCommand, OrganizationDto>
{
    private readonly IOrganizationContext _context;
    private readonly IOrganizationRepository _organizations;
    private readonly IPermissionService _permissions;

    public UpdateOrganizationCommandHandler(
        IOrganizationContext context, IOrganizationRepository organizations, IPermissionService permissions)
        => (_context, _organizations, _permissions) = (context, organizations, permissions);

    public async Task<OrganizationDto> Handle(UpdateOrganizationCommand request, CancellationToken ct)
    {
        await _permissions.EnsureAsync(request.ActorId, PlatformPermission.AdministerOrganization,
            cancellationToken: ct);
        var id = _context.RequireOrganizationId();
        var organization = await _organizations.GetByIdAsync(id, ct)
            ?? throw new NaoEncontradoException("Organização");
        organization.Update(request.Name, request.Locale, request.TimeZone, request.WeekStartDay);
        await _organizations.SaveAsync(ct);
        var member = await _organizations.GetMemberAsync(id, request.ActorId, ct)
            ?? throw new AcessoNegadoException();
        return GetOrganizationsQueryHandler.Map(organization, member);
    }
}

public record GetOrganizationMembersQuery(string ActorId) : IRequest<IReadOnlyList<OrganizationMemberDto>>;

public class GetOrganizationMembersQueryHandler : IRequestHandler<GetOrganizationMembersQuery, IReadOnlyList<OrganizationMemberDto>>
{
    private readonly IOrganizationContext _context;
    private readonly IOrganizationRepository _organizations;
    private readonly IUserDirectory _users;
    private readonly IPermissionService _permissions;

    public GetOrganizationMembersQueryHandler(
        IOrganizationContext context, IOrganizationRepository organizations,
        IUserDirectory users, IPermissionService permissions)
        => (_context, _organizations, _users, _permissions) = (context, organizations, users, permissions);

    public async Task<IReadOnlyList<OrganizationMemberDto>> Handle(GetOrganizationMembersQuery request, CancellationToken ct)
    {
        await _permissions.EnsureAsync(request.ActorId, PlatformPermission.View, cancellationToken: ct);
        var members = await _organizations.GetMembersAsync(_context.RequireOrganizationId(), ct);
        var users = (await _users.GetByIdsAsync(
            members.Select(x => x.UserId), includeInactive: true, cancellationToken: ct))
            .ToDictionary(x => x.Id);
        return members.Select(member =>
        {
            users.TryGetValue(member.UserId, out var user);
            var name = user?.DisplayName
                ?? UserDisplayName.Resolve(member.UserId, member.DisplayName, null, null);
            return new OrganizationMemberDto(
                member.UserId,
                name,
                member.DisplayName,
                user?.Email,
                user?.UserName,
                member.Role,
                member.IsActive,
                member.JoinedAt);
        }).ToList();
    }
}

public record UpdateOrganizationMemberCommand(
    string UserId, OrganizationRole Role, bool IsActive, string? DisplayName, string ActorId) : IRequest;

public class UpdateOrganizationMemberCommandValidator : AbstractValidator<UpdateOrganizationMemberCommand>
{
    public UpdateOrganizationMemberCommandValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.DisplayName)
            .Must(name => name is null || name.Trim().Length is >= 2 and <= 200)
            .WithMessage("Nome de exibição deve ter entre 2 e 200 caracteres.");
    }
}

public class UpdateOrganizationMemberCommandHandler : IRequestHandler<UpdateOrganizationMemberCommand>
{
    private readonly IOrganizationContext _context;
    private readonly IOrganizationRepository _organizations;
    private readonly IPermissionService _permissions;

    public UpdateOrganizationMemberCommandHandler(
        IOrganizationContext context, IOrganizationRepository organizations, IPermissionService permissions)
        => (_context, _organizations, _permissions) = (context, organizations, permissions);

    public async Task Handle(UpdateOrganizationMemberCommand request, CancellationToken ct)
    {
        await _permissions.EnsureAsync(request.ActorId, PlatformPermission.ManageMembers,
            cancellationToken: ct);
        var organizationId = _context.RequireOrganizationId();
        var member = await _organizations.GetMemberAsync(organizationId, request.UserId, ct)
            ?? throw new NaoEncontradoException("Membro");
        if (member.Role == OrganizationRole.Administrator
            && (!request.IsActive || request.Role != OrganizationRole.Administrator)
            && await _organizations.CountActiveAdministratorsAsync(organizationId, ct) <= 1)
            throw new DomainException("A organização precisa manter ao menos um administrador ativo.");

        member.Configure(request.Role, request.IsActive);
        member.UpdateDisplayName(request.DisplayName);
        await _organizations.SaveAsync(ct);
    }
}

public record InviteOrganizationMemberCommand(
    string Email, OrganizationRole Role, int ExpiresInDays, string ActorId)
    : IRequest<OrganizationInvitationDto>;

public class InviteOrganizationMemberCommandValidator : AbstractValidator<InviteOrganizationMemberCommand>
{
    public InviteOrganizationMemberCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.Role).IsInEnum();
        RuleFor(x => x.ExpiresInDays).InclusiveBetween(1, 30);
    }
}

public class InviteOrganizationMemberCommandHandler
    : IRequestHandler<InviteOrganizationMemberCommand, OrganizationInvitationDto>
{
    private readonly IOrganizationContext _context;
    private readonly IOrganizationRepository _organizations;
    private readonly IPermissionService _permissions;

    public InviteOrganizationMemberCommandHandler(
        IOrganizationContext context, IOrganizationRepository organizations, IPermissionService permissions)
        => (_context, _organizations, _permissions) = (context, organizations, permissions);

    public async Task<OrganizationInvitationDto> Handle(InviteOrganizationMemberCommand request, CancellationToken ct)
    {
        await _permissions.EnsureAsync(request.ActorId, PlatformPermission.ManageMembers,
            cancellationToken: ct);
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var invitation = new OrganizationInvitation
        {
            Id = Guid.NewGuid(),
            OrganizationId = _context.RequireOrganizationId(),
            Email = request.Email.Trim().ToLowerInvariant(),
            Role = request.Role,
            TokenHash = Hash(token),
            InvitedBy = request.ActorId,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(request.ExpiresInDays),
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _organizations.AddInvitationAsync(invitation, ct);
        return new OrganizationInvitationDto(
            invitation.Id, invitation.Email, invitation.Role, invitation.ExpiresAt, token);
    }

    internal static string Hash(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}

public record AcceptOrganizationInvitationCommand(string Token, string UserId) : IRequest<Guid>;

public class AcceptOrganizationInvitationCommandHandler : IRequestHandler<AcceptOrganizationInvitationCommand, Guid>
{
    private readonly IOrganizationRepository _organizations;
    private readonly IUserDirectory _users;

    public AcceptOrganizationInvitationCommandHandler(IOrganizationRepository organizations, IUserDirectory users)
        => (_organizations, _users) = (organizations, users);

    public async Task<Guid> Handle(AcceptOrganizationInvitationCommand request, CancellationToken ct)
    {
        var invitation = await _organizations.GetInvitationByTokenHashAsync(
            InviteOrganizationMemberCommandHandler.Hash(request.Token), ct)
            ?? throw new NaoEncontradoException("Convite");
        DomainException.Garantir(invitation.CanBeAccepted(DateTimeOffset.UtcNow), "Convite expirado ou indisponível.");
        // Sem escopo de organização: quem aceita um convite ainda não pertence à org.
        var user = await _users.GetByIdUnscopedAsync(request.UserId, ct)
            ?? throw new NaoEncontradoException("Usuário");
        DomainException.Garantir(string.Equals(user.Email, invitation.Email, StringComparison.OrdinalIgnoreCase),
            "Este convite pertence a outro endereço de e-mail.");

        var member = await _organizations.GetMemberAsync(invitation.OrganizationId, request.UserId, ct);
        if (member is null)
            invitation.Organization.Members.Add(OrganizationMember.Create(
                invitation.OrganizationId, request.UserId, invitation.Role));
        else
            member.Configure(invitation.Role, true);
        invitation.AcceptedAt = DateTimeOffset.UtcNow;
        await _organizations.SaveAsync(ct);
        return invitation.OrganizationId;
    }
}

public record GetPermissionGrantsQuery(string ActorId) : IRequest<IReadOnlyList<PermissionGrantDto>>;

public class GetPermissionGrantsQueryHandler : IRequestHandler<GetPermissionGrantsQuery, IReadOnlyList<PermissionGrantDto>>
{
    private readonly IOrganizationContext _context;
    private readonly IOrganizationRepository _organizations;
    private readonly IPermissionService _permissions;

    public GetPermissionGrantsQueryHandler(
        IOrganizationContext context, IOrganizationRepository organizations, IPermissionService permissions)
        => (_context, _organizations, _permissions) = (context, organizations, permissions);

    public async Task<IReadOnlyList<PermissionGrantDto>> Handle(GetPermissionGrantsQuery request, CancellationToken ct)
    {
        await _permissions.EnsureAsync(request.ActorId, PlatformPermission.ManagePermissions,
            cancellationToken: ct);
        return (await _organizations.GetGrantsAsync(_context.RequireOrganizationId(), ct))
            .Select(Map).ToList();
    }

    internal static PermissionGrantDto Map(PermissionGrant grant) => new(
        grant.Id, grant.UserId, grant.Scope, grant.ScopeId, grant.Permission, grant.IsAllowed);
}

public record SetPermissionGrantCommand(
    string UserId,
    PermissionScope Scope,
    Guid? ScopeId,
    PlatformPermission Permission,
    bool IsAllowed,
    string ActorId) : IRequest<PermissionGrantDto>;

public class SetPermissionGrantCommandValidator : AbstractValidator<SetPermissionGrantCommand>
{
    public SetPermissionGrantCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Scope).IsInEnum();
        RuleFor(x => x.Permission).IsInEnum();
    }
}

public class SetPermissionGrantCommandHandler : IRequestHandler<SetPermissionGrantCommand, PermissionGrantDto>
{
    private readonly IOrganizationContext _context;
    private readonly IOrganizationRepository _organizations;
    private readonly IPermissionService _permissions;

    public SetPermissionGrantCommandHandler(
        IOrganizationContext context, IOrganizationRepository organizations, IPermissionService permissions)
        => (_context, _organizations, _permissions) = (context, organizations, permissions);

    public async Task<PermissionGrantDto> Handle(SetPermissionGrantCommand request, CancellationToken ct)
    {
        await _permissions.EnsureAsync(request.ActorId, PlatformPermission.ManagePermissions,
            cancellationToken: ct);
        var organizationId = _context.RequireOrganizationId();
        var member = await _organizations.GetMemberAsync(organizationId, request.UserId, ct);
        DomainException.Garantir(member?.IsActive == true, "A permissão só pode ser atribuída a um membro ativo.");
        DomainException.Garantir(
            request.Scope == PermissionScope.Organization ? request.ScopeId is null : request.ScopeId is not null,
            "O identificador do escopo é inválido.");

        var grant = await _organizations.GetGrantAsync(
            organizationId, request.UserId, request.Scope, request.ScopeId, request.Permission, ct);
        if (grant is null)
        {
            grant = new PermissionGrant
            {
                Id = Guid.NewGuid(),
                OrganizationId = organizationId,
                UserId = request.UserId,
                Scope = request.Scope,
                ScopeId = request.ScopeId,
                Permission = request.Permission,
                IsAllowed = request.IsAllowed,
                GrantedBy = request.ActorId,
                CreatedAt = DateTimeOffset.UtcNow
            };
            await _organizations.AddGrantAsync(grant, ct);
        }
        else
        {
            grant.IsAllowed = request.IsAllowed;
            grant.GrantedBy = request.ActorId;
            await _organizations.SaveAsync(ct);
        }
        return GetPermissionGrantsQueryHandler.Map(grant);
    }
}

public record DeletePermissionGrantCommand(Guid GrantId, string ActorId) : IRequest;

public class DeletePermissionGrantCommandHandler : IRequestHandler<DeletePermissionGrantCommand>
{
    private readonly IOrganizationContext _context;
    private readonly IOrganizationRepository _organizations;
    private readonly IPermissionService _permissions;

    public DeletePermissionGrantCommandHandler(
        IOrganizationContext context, IOrganizationRepository organizations, IPermissionService permissions)
        => (_context, _organizations, _permissions) = (context, organizations, permissions);

    public async Task Handle(DeletePermissionGrantCommand request, CancellationToken ct)
    {
        await _permissions.EnsureAsync(request.ActorId, PlatformPermission.ManagePermissions,
            cancellationToken: ct);
        var grant = (await _organizations.GetGrantsAsync(_context.RequireOrganizationId(), ct))
            .FirstOrDefault(x => x.Id == request.GrantId)
            ?? throw new NaoEncontradoException("Permissão");
        await _organizations.RemoveGrantAsync(grant, ct);
    }
}

public record GetOrganizationAccessQuery(string UserId) : IRequest<OrganizationAccessDto>;

public class GetOrganizationAccessQueryHandler : IRequestHandler<GetOrganizationAccessQuery, OrganizationAccessDto>
{
    private readonly IOrganizationContext _context;
    private readonly IOrganizationRepository _organizations;
    private readonly IPermissionService _permissions;

    public GetOrganizationAccessQueryHandler(
        IOrganizationContext context, IOrganizationRepository organizations, IPermissionService permissions)
        => (_context, _organizations, _permissions) = (context, organizations, permissions);

    public async Task<OrganizationAccessDto> Handle(GetOrganizationAccessQuery request, CancellationToken ct)
    {
        var member = await _organizations.GetMemberAsync(_context.RequireOrganizationId(), request.UserId, ct)
            ?? throw new AcessoNegadoException();
        var allowed = new List<PlatformPermission>();
        foreach (var permission in Enum.GetValues<PlatformPermission>())
            if (await _permissions.HasAsync(request.UserId, permission, cancellationToken: ct))
                allowed.Add(permission);
        return new OrganizationAccessDto(member.Role, allowed);
    }
}
