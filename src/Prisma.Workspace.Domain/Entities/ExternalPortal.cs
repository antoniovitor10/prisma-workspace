using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Domain.Interfaces;

namespace Prisma.Workspace.Domain.Entities;

/// <summary>Configuração pública de entrada de solicitações de um projeto.</summary>
public class ExternalPortal : IOrganizationOwned
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid ProjectId { get; set; }
    public Guid BoardId { get; set; }
    public string PublicSlug { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public bool RequiresAuthentication { get; set; }
    public ExternalPortalAccessMode AccessModes { get; set; } = ExternalPortalAccessMode.PublicLink;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Project Project { get; set; } = null!;
    public Board Board { get; set; } = null!;
    public ICollection<ExternalRequest> Requests { get; set; } = new List<ExternalRequest>();
    public ICollection<ExternalPortalInvitation> Invitations { get; set; } = new List<ExternalPortalInvitation>();
    public ICollection<ExternalPortalVerification> Verifications { get; set; } = new List<ExternalPortalVerification>();
    public ICollection<ExternalForm> Forms { get; set; } = new List<ExternalForm>();

    public static ExternalPortal Create(
        Guid organizationId,
        Guid projectId,
        Guid boardId,
        string publicSlug,
        bool isEnabled,
        bool requiresAuthentication,
        ExternalPortalAccessMode accessModes)
    {
        var now = DateTimeOffset.UtcNow;
        var portal = new ExternalPortal
        {
            Id = Guid.NewGuid(),
            OrganizationId = organizationId,
            ProjectId = projectId,
            CreatedAt = now
        };
        portal.Update(boardId, publicSlug, isEnabled, requiresAuthentication, accessModes);
        return portal;
    }

    public void Update(
        Guid boardId,
        string publicSlug,
        bool isEnabled,
        bool requiresAuthentication,
        ExternalPortalAccessMode accessModes)
    {
        DomainException.Garantir(boardId != Guid.Empty, "Quadro de entrada obrigatório.");
        var slug = NormalizeSlug(publicSlug);
        DomainException.Garantir(slug.Length is >= 3 and <= 80,
            "O endereço público deve ter entre 3 e 80 caracteres.");
        DomainException.Garantir(accessModes != ExternalPortalAccessMode.None,
            "Selecione ao menos uma forma de acesso ao portal.");
        var knownModes = ExternalPortalAccessMode.PublicLink | ExternalPortalAccessMode.Login
            | ExternalPortalAccessMode.Invitation | ExternalPortalAccessMode.EmailCode;
        DomainException.Garantir((accessModes & ~knownModes) == 0,
            "A configuração contém uma forma de acesso inválida.");
        if (requiresAuthentication)
        {
            var authenticatedModes = ExternalPortalAccessMode.Login
                | ExternalPortalAccessMode.Invitation
                | ExternalPortalAccessMode.EmailCode;
            DomainException.Garantir((accessModes & authenticatedModes) != 0,
                "Portal autenticado exige login, convite ou código por e-mail.");
        }
        else
        {
            DomainException.Garantir(accessModes.HasFlag(ExternalPortalAccessMode.PublicLink),
                "Portal sem autenticação precisa permitir link público.");
        }

        BoardId = boardId;
        PublicSlug = slug;
        IsEnabled = isEnabled;
        RequiresAuthentication = requiresAuthentication;
        AccessModes = accessModes;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public static string NormalizeSlug(string value)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        DomainException.Garantir(normalized.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'),
            "O endereço público aceita apenas letras, números e hífen.");
        return normalized;
    }
}

public class ExternalRequest
{
    public Guid Id { get; set; }
    public Guid ExternalPortalId { get; set; }
    public Guid WorkItemId { get; set; }
    public Guid? ExternalFormId { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public string AccessKeyHash { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public string? RequesterPhone { get; set; }
    public string? Category { get; set; }
    public string? RelatedService { get; set; }
    public string? SubmittedValuesJson { get; set; }
    public ExternalRequestTriageStatus TriageStatus { get; set; } = ExternalRequestTriageStatus.New;
    public int? Rating { get; set; }
    public string? RatingComment { get; set; }
    public DateTimeOffset? RatedAt { get; set; }
    public DateTimeOffset? CompletionConfirmedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ExternalPortal ExternalPortal { get; set; } = null!;
    public ExternalForm? ExternalForm { get; set; }
    public WorkItem WorkItem { get; set; } = null!;
    public ICollection<ExternalRequestMessage> Messages { get; set; } = new List<ExternalRequestMessage>();
    public ICollection<ExternalRequestTriageEvent> TriageEvents { get; set; } = new List<ExternalRequestTriageEvent>();

    public void Rate(int rating, string? comment)
    {
        DomainException.Garantir(rating is >= 1 and <= 5, "A avaliação deve estar entre 1 e 5.");
        Rating = rating;
        RatingComment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        RatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = RatedAt.Value;
    }

    public void ConfirmCompletion()
    {
        DomainException.Garantir(WorkItem.CompletedAt.HasValue,
            "A conclusão só pode ser confirmada quando a solicitação estiver concluída.");
        CompletionConfirmedAt ??= DateTimeOffset.UtcNow;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public class ExternalRequestTriageEvent
{
    public Guid Id { get; set; }
    public Guid ExternalRequestId { get; set; }
    public ExternalRequestTriageAction Action { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? DataJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ExternalRequest ExternalRequest { get; set; } = null!;
}

public class ExternalRequestMessage
{
    public Guid Id { get; set; }
    public Guid ExternalRequestId { get; set; }
    public ExternalRequestMessageAuthor AuthorType { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? AuthorId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public ExternalRequest ExternalRequest { get; set; } = null!;
}

public class ExternalPortalInvitation
{
    public Guid Id { get; set; }
    public Guid ExternalPortalId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string TokenHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ExternalPortal ExternalPortal { get; set; } = null!;
}

public class ExternalPortalVerification
{
    public Guid Id { get; set; }
    public Guid ExternalPortalId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public ExternalPortal ExternalPortal { get; set; } = null!;
}
