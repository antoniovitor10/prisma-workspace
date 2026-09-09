using Prisma.Workspace.Domain.Interfaces;

namespace Prisma.Workspace.Domain.Entities;

/// <summary>
/// Quadro Kanban. Agrupa etapas e itens de trabalho.
/// </summary>
public class Board : IOrganizationOwned
{
    /// <summary>Identificador único do quadro.</summary>
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    /// <summary>Projeto ao qual o quadro pertence. Nulo apenas durante a transição de dados legados.</summary>
    public Guid? ProjectId { get; set; }

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

    public Project? Project { get; set; }
    public Team? Team { get; set; }

    /// <summary>Data/hora de criação.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    // ── Navegação ──────────────────────────────────────────────

    /// <summary>Etapas pertencentes a este quadro.</summary>
    public ICollection<Stage> Stages { get; set; }

    /// <summary>Itens cujo quadro home ainda aponta para este quadro (compatibilidade).</summary>
    public ICollection<WorkItem> WorkItems { get; set; }

    /// <summary>Projeções de tarefas neste quadro.</summary>

    public Board()
    {
        Stages = new List<Stage>();
        WorkItems = new List<WorkItem>();
    }

    /// <summary>Vincula cliente e descrição ao projeto (nulos limpam o vínculo).</summary>
    public void VincularCliente(Guid? clientId, string? descricao)
    {
        ClientId = clientId;
        Description = string.IsNullOrWhiteSpace(descricao) ? null : descricao;
    }
}
