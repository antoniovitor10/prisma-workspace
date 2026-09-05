using Detran.Kanban.Domain.Enums;
using Detran.Kanban.Domain.Exceptions;
using Detran.Kanban.Domain.Interfaces;

namespace Detran.Kanban.Domain.Entities;

/// <summary>Tenant que delimita dados, membros e preferências da plataforma.</summary>
public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string Locale { get; set; } = "pt-BR";
    public string TimeZone { get; set; } = "America/Sao_Paulo";
    public DayOfWeek WeekStartDay { get; set; } = DayOfWeek.Monday;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<OrganizationMember> Members { get; set; } = new List<OrganizationMember>();
    public ICollection<OrganizationInvitation> Invitations { get; set; } = new List<OrganizationInvitation>();
    public ICollection<OrganizationWorkflowTemplate> WorkflowTemplates { get; set; } = new List<OrganizationWorkflowTemplate>();

    public static Organization Create(string name, string slug, string administratorId)
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(name), "Nome da organização obrigatório.");
        DomainException.Garantir(!string.IsNullOrWhiteSpace(slug), "Identificador da organização obrigatório.");
        DomainException.Garantir(!string.IsNullOrWhiteSpace(administratorId), "Administrador obrigatório.");

        var now = DateTimeOffset.UtcNow;
        var organization = new Organization
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            CreatedAt = now,
            UpdatedAt = now
        };
        organization.Members.Add(OrganizationMember.Create(
            organization.Id, administratorId, OrganizationRole.Administrator));
        return organization;
    }

    public void Update(string name, string locale, string timeZone, DayOfWeek weekStartDay)
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(name), "Nome da organização obrigatório.");
        DomainException.Garantir(!string.IsNullOrWhiteSpace(locale), "Idioma obrigatório.");
        DomainException.Garantir(!string.IsNullOrWhiteSpace(timeZone), "Fuso horário obrigatório.");
        DomainException.Garantir(Enum.IsDefined(weekStartDay), "Primeiro dia da semana inválido.");
        Name = name.Trim();
        Locale = locale.Trim();
        TimeZone = timeZone.Trim();
        WeekStartDay = weekStartDay;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public class OrganizationMember : IOrganizationOwned
{
    public Guid OrganizationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public OrganizationRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset JoinedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public Organization Organization { get; set; } = null!;

    public static OrganizationMember Create(Guid organizationId, string userId, OrganizationRole role)
    {
        DomainException.Garantir(organizationId != Guid.Empty, "Organização obrigatória.");
        DomainException.Garantir(!string.IsNullOrWhiteSpace(userId), "Usuário obrigatório.");
        DomainException.Garantir(Enum.IsDefined(role), "Perfil inválido.");
        return new OrganizationMember
        {
            OrganizationId = organizationId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTimeOffset.UtcNow
        };
    }

    public void Configure(OrganizationRole role, bool isActive)
    {
        DomainException.Garantir(Enum.IsDefined(role), "Perfil inválido.");
        Role = role;
        IsActive = isActive;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateDisplayName(string? displayName)
    {
        if (displayName is null)
        {
            DisplayName = null;
            UpdatedAt = DateTimeOffset.UtcNow;
            return;
        }

        var normalized = displayName.Trim();
        DomainException.Garantir(normalized.Length >= 2, "Nome de exibição deve ter pelo menos 2 caracteres.");
        DomainException.Garantir(normalized.Length <= 200, "Nome de exibição deve ter no máximo 200 caracteres.");
        DisplayName = normalized;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public class OrganizationInvitation : IOrganizationOwned
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Email { get; set; } = string.Empty;
    public OrganizationRole Role { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string InvitedBy { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset? CanceledAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Organization Organization { get; set; } = null!;

    public bool CanBeAccepted(DateTimeOffset now) => AcceptedAt is null && CanceledAt is null && ExpiresAt > now;
}

public class PermissionGrant : IOrganizationOwned
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public PermissionScope Scope { get; set; }
    public Guid? ScopeId { get; set; }
    public PlatformPermission Permission { get; set; }
    public bool IsAllowed { get; set; }
    public string GrantedBy { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}
