using Prisma.Workspace.Domain.Exceptions;
using Prisma.Workspace.Domain.Interfaces;

namespace Prisma.Workspace.Domain.Entities;

/// <summary>
/// Equipe da empresa (agrupamento de pessoas com capacidade semanal).
/// </summary>
public class Team : IOrganizationOwned
{
    public const decimal CapacidadePadraoHoras = 40;

    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }

    /// <summary>Nome da equipe.</summary>
    public string Name { get; set; } = string.Empty;
    public string? LeaderId { get; set; }
    public bool IsActive { get; set; } = true;
    public decimal DefaultWeeklyCapacityHours { get; set; } = CapacidadePadraoHoras;

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Membros da equipe.</summary>
    public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
    public ICollection<ProjectTeam> Projects { get; set; } = new List<ProjectTeam>();

    /// <summary>Cria uma equipe com nome obrigatório.</summary>
    public static Team Criar(string nome, decimal? capacidadePadrao = null)
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(nome), "Nome obrigatório.");
        var capacity = capacidadePadrao is > 0 ? capacidadePadrao.Value : CapacidadePadraoHoras;
        return new Team
        {
            Id = Guid.NewGuid(),
            Name = nome.Trim(),
            DefaultWeeklyCapacityHours = capacity,
            CreatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>Adiciona um membro; recusa duplicado e aplica capacidade padrão.</summary>
    public TeamMember AdicionarMembro(string userId, decimal? capacidadeSemanalHoras)
    {
        DomainException.Garantir(Members.All(m => m.UserId != userId), "Usuário já é membro.");
        var membro = new TeamMember
        {
            TeamId = Id,
            UserId = userId,
            WeeklyCapacityHours = capacidadeSemanalHoras is > 0
                ? capacidadeSemanalHoras.Value
                : DefaultWeeklyCapacityHours,
            AddedAt = DateTimeOffset.UtcNow
        };
        Members.Add(membro);
        return membro;
    }

    public void Editar(string nome, string? leaderId, decimal capacidadePadrao)
    {
        DomainException.Garantir(!string.IsNullOrWhiteSpace(nome), "Nome obrigatório.");
        DomainException.Garantir(capacidadePadrao > 0 && capacidadePadrao <= 168, "Capacidade semanal inválida.");
        DomainException.Garantir(leaderId is null || Members.Any(m => m.UserId == leaderId),
            "O líder precisa ser membro da equipe.");
        Name = nome.Trim();
        LeaderId = leaderId;
        DefaultWeeklyCapacityHours = capacidadePadrao;
    }

    public void DefinirAtiva(bool ativa) => IsActive = ativa;

    public void AtualizarCapacidade(string userId, decimal capacidadeSemanalHoras)
    {
        DomainException.Garantir(capacidadeSemanalHoras > 0 && capacidadeSemanalHoras <= 168,
            "Capacidade semanal inválida.");
        var membro = Members.FirstOrDefault(m => m.UserId == userId);
        DomainException.Garantir(membro is not null, "Membro não encontrado.");
        membro!.WeeklyCapacityHours = capacidadeSemanalHoras;
    }
}

/// <summary>
/// Membro de uma equipe, com capacidade semanal em horas.
/// </summary>
public class TeamMember
{
    public Guid TeamId { get; set; }
    public Team Team { get; set; } = null!;

    public string UserId { get; set; } = string.Empty;

    /// <summary>Capacidade semanal em horas (padrão 40).</summary>
    public decimal WeeklyCapacityHours { get; set; } = Team.CapacidadePadraoHoras;

    public DateTimeOffset AddedAt { get; set; }
}
