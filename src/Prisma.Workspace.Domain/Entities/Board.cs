using Prisma.Workspace.Domain.Interfaces;

namespace Prisma.Workspace.Domain.Entities;

/// <summary>
/// Quadro como visão salva do projeto (D83). Não possui colunas próprias — o fluxo fica em <see cref="Stage"/>.
/// </summary>
public class Board : IOrganizationOwned
{
    /// <summary>Identificador único do quadro.</summary>
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    /// <summary>Projeto ao qual o quadro pertence (obrigatório).</summary>
    public Guid ProjectId { get; set; }

    /// <summary>Time proprietário do fluxo. Nulo em quadros legados até a configuração.</summary>
    public Guid? TeamId { get; set; }

    /// <summary>Nome do quadro.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Identificador do usuário proprietário do quadro.</summary>
    public string? OwnerId { get; set; }

    /// <summary>Descrição do quadro/projeto.</summary>
    public string? Description { get; set; }

    /// <summary>Preferências versionáveis de conteúdo e ordenação dos cartões.</summary>
    public string? CardSettingsJson { get; set; }

    /// <summary>Cliente ao qual este quadro pertence (opcional).</summary>
    public Guid? ClientId { get; set; }

    /// <summary>Cliente vinculado (pode ser nulo).</summary>
    public Client? Client { get; set; }

    public Project Project { get; set; } = null!;
    public Team? Team { get; set; }

    /// <summary>Data/hora de criação.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    // ── Navegação ──────────────────────────────────────────────

    /// <summary>Itens cujo quadro home ainda aponta para este quadro (compatibilidade).</summary>
    public ICollection<WorkItem> WorkItems { get; set; } = new List<WorkItem>();

    /// <summary>Vincula cliente e descrição ao projeto (nulos limpam o vínculo).</summary>
    public void VincularCliente(Guid? clientId, string? descricao)
    {
        ClientId = clientId;
        Description = string.IsNullOrWhiteSpace(descricao) ? null : descricao;
    }
}
