using Prisma.Workspace.Domain.Exceptions;

namespace Prisma.Workspace.Domain.Entities;

/// <summary>Política de prazo de atendimento aplicada às solicitações externas do projeto.</summary>
public class ProjectSlaPolicy
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public bool IsEnabled { get; set; }
    public int FirstResponseMinutes { get; set; } = 240;
    public int ResolutionMinutes { get; set; } = 1440;
    public TimeOnly ServiceStart { get; set; } = new(8, 0);
    public TimeOnly ServiceEnd { get; set; } = new(18, 0);
    public int BusinessDaysMask { get; set; } = 62;
    public string TimeZoneId { get; set; } = "E. South America Standard Time";
    public bool PauseWhileWaitingRequester { get; set; } = true;
    public bool AlertsEnabled { get; set; } = true;
    public int NearDueMinutes { get; set; } = 60;
    public string HolidaysJson { get; set; } = "[]";
    public string RulesJson { get; set; } = "[]";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Project Project { get; set; } = null!;

    public static ProjectSlaPolicy Create(Guid projectId)
    {
        var now = DateTimeOffset.UtcNow;
        return new ProjectSlaPolicy
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Configure(
        bool isEnabled,
        int firstResponseMinutes,
        int resolutionMinutes,
        TimeOnly serviceStart,
        TimeOnly serviceEnd,
        int businessDaysMask,
        string timeZoneId,
        bool pauseWhileWaitingRequester,
        bool alertsEnabled,
        int nearDueMinutes,
        string holidaysJson,
        string rulesJson)
    {
        DomainException.Garantir(firstResponseMinutes is >= 1 and <= 525_600,
            "O prazo de primeira resposta deve estar entre 1 minuto e 365 dias.");
        DomainException.Garantir(resolutionMinutes is >= 1 and <= 2_102_400,
            "O prazo de resolução deve estar entre 1 minuto e 4 anos.");
        DomainException.Garantir(serviceEnd > serviceStart,
            "O fim do horário de atendimento deve ser posterior ao início.");
        DomainException.Garantir((businessDaysMask & 127) != 0 && (businessDaysMask & ~127) == 0,
            "Selecione ao menos um dia útil válido.");
        DomainException.Garantir(!string.IsNullOrWhiteSpace(timeZoneId),
            "O fuso horário do SLA é obrigatório.");
        DomainException.Garantir(nearDueMinutes is >= 1 and <= 525_600,
            "A antecedência do alerta deve estar entre 1 minuto e 365 dias.");

        IsEnabled = isEnabled;
        FirstResponseMinutes = firstResponseMinutes;
        ResolutionMinutes = resolutionMinutes;
        ServiceStart = serviceStart;
        ServiceEnd = serviceEnd;
        BusinessDaysMask = businessDaysMask;
        TimeZoneId = timeZoneId.Trim();
        PauseWhileWaitingRequester = pauseWhileWaitingRequester;
        AlertsEnabled = alertsEnabled;
        NearDueMinutes = nearDueMinutes;
        HolidaysJson = holidaysJson;
        RulesJson = rulesJson;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
