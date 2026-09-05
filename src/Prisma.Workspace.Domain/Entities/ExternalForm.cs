using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;

namespace Prisma.Workspace.Domain.Entities;

/// <summary>Definição publicável de um formulário do Portal Externo.</summary>
public class ExternalForm
{
    public Guid Id { get; set; }
    public Guid ExternalPortalId { get; set; }
    public string PublicSlug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Category { get; set; }
    public string? ConfirmationMessage { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool IsDefault { get; set; }
    public Priority DefaultPriority { get; set; } = Priority.Medium;
    public Guid? InitialStageId { get; set; }
    public Guid? DefaultTeamId { get; set; }
    public string? DefaultResponsibleId { get; set; }
    public int MaxFiles { get; set; } = 5;
    public long MaxFileSizeBytes { get; set; } = 10_000_000;
    public string AllowedExtensions { get; set; } = ".pdf,.png,.jpg,.jpeg,.doc,.docx,.xls,.xlsx";
    public string AllowedMimeTypes { get; set; } = "application/pdf,image/png,image/jpeg,application/msword,application/vnd.openxmlformats-officedocument.wordprocessingml.document,application/vnd.ms-excel,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    public int MinimumCompletionSeconds { get; set; } = 2;
    public string FieldsJson { get; set; } = "[]";
    public string AssignmentRulesJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ExternalPortal ExternalPortal { get; set; } = null!;
    public Stage? InitialStage { get; set; }
    public Team? DefaultTeam { get; set; }
    public ICollection<ExternalRequest> Requests { get; set; } = new List<ExternalRequest>();

    public static ExternalForm CreateDefault(Guid portalId, string projectName)
    {
        var now = DateTimeOffset.UtcNow;
        return new ExternalForm
        {
            Id = Guid.NewGuid(),
            ExternalPortalId = portalId,
            PublicSlug = "solicitacao",
            Title = $"Solicitação — {projectName.Trim()}",
            Description = "Descreva sua necessidade para que nossa equipe possa iniciar o atendimento.",
            ConfirmationMessage = "Recebemos sua solicitação. Guarde o protocolo e a chave de acompanhamento.",
            IsEnabled = true,
            IsDefault = true,
            FieldsJson = DefaultFieldsJson,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Configure(
        string publicSlug,
        string title,
        string? description,
        string? category,
        string? confirmationMessage,
        bool isEnabled,
        bool isDefault,
        Priority defaultPriority,
        Guid? initialStageId,
        Guid? defaultTeamId,
        string? defaultResponsibleId,
        int maxFiles,
        long maxFileSizeBytes,
        string allowedExtensions,
        string allowedMimeTypes,
        int minimumCompletionSeconds,
        string fieldsJson,
        string assignmentRulesJson)
    {
        var slug = ExternalPortal.NormalizeSlug(publicSlug);
        DomainException.Garantir(slug.Length is >= 3 and <= 80,
            "O endereço do formulário deve ter entre 3 e 80 caracteres.");
        DomainException.Garantir(!string.IsNullOrWhiteSpace(title), "Título do formulário obrigatório.");
        DomainException.Garantir(title.Trim().Length <= 200, "O título deve ter no máximo 200 caracteres.");
        DomainException.Garantir(maxFiles is >= 0 and <= 20, "O limite de anexos deve estar entre 0 e 20.");
        DomainException.Garantir(maxFileSizeBytes is >= 100_000 and <= 50_000_000,
            "O limite por arquivo deve estar entre 100 KB e 50 MB.");
        DomainException.Garantir(minimumCompletionSeconds is >= 0 and <= 60,
            "O tempo mínimo de preenchimento deve estar entre 0 e 60 segundos.");

        PublicSlug = slug;
        Title = title.Trim();
        Description = Clean(description);
        Category = Clean(category);
        ConfirmationMessage = Clean(confirmationMessage);
        IsEnabled = isEnabled;
        IsDefault = isDefault;
        DefaultPriority = defaultPriority;
        InitialStageId = initialStageId;
        DefaultTeamId = defaultTeamId;
        DefaultResponsibleId = Clean(defaultResponsibleId);
        MaxFiles = maxFiles;
        MaxFileSizeBytes = maxFileSizeBytes;
        AllowedExtensions = allowedExtensions.Trim().ToLowerInvariant();
        AllowedMimeTypes = allowedMimeTypes.Trim().ToLowerInvariant();
        MinimumCompletionSeconds = minimumCompletionSeconds;
        FieldsJson = fieldsJson;
        AssignmentRulesJson = assignmentRulesJson;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public const string DefaultFieldsJson = """
        [{"key":"requesterName","label":"Nome","type":1,"kind":5,"isRequired":true,"position":0,"maxLength":200},{"key":"requesterEmail","label":"E-mail","type":3,"kind":6,"isRequired":true,"position":1,"maxLength":320},{"key":"requesterPhone","label":"Telefone","type":4,"kind":7,"isRequired":false,"position":2,"maxLength":30},{"key":"subject","label":"Assunto","type":1,"kind":1,"isRequired":true,"position":3,"maxLength":500},{"key":"description","label":"Descrição detalhada","type":2,"kind":2,"isRequired":true,"position":4,"maxLength":10000},{"key":"category","label":"Categoria","type":1,"kind":3,"isRequired":false,"position":5,"maxLength":120},{"key":"priority","label":"Prioridade informada","type":5,"kind":4,"isRequired":false,"position":6,"options":["Baixa","Média","Alta","Crítica"]},{"key":"relatedService","label":"Serviço relacionado","type":1,"kind":8,"isRequired":false,"position":7,"maxLength":200},{"key":"attachments","label":"Anexos","type":9,"kind":9,"isRequired":false,"position":8}]
        """;
}
