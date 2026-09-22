using Prisma.Workspace.Application.Features.Me;
using Prisma.Workspace.Application.Interfaces;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Tests;

/// <summary>
/// Blocos de projetos vigentes, sprints em curso e horas da semana do "Meu trabalho"
/// (SPEC-MY-WORK-HUB). Tudo é projeção de leitura sobre WorkItem, Project e Sprint,
/// conforme a D23: nada é duplicado numa fila própria.
/// </summary>
public class MyWorkHubTests
{
    private const string Eu = "user-eu";
    private const string Outro = "user-outro";

    private static DateOnly Hoje => DateOnly.FromDateTime(DateTime.UtcNow);

    private static Project NovoProjeto(string chave, string nome, bool ativo = true)
    {
        var projeto = Project.Criar(chave, nome, Eu, WorkNature.Project, WorkType.Development);
        if (!ativo) projeto.Status = ProjectStatus.Completed;
        return projeto;
    }

    private static WorkItem NovaTarefa(
        Guid projectId, string? responsavel = Eu,
        DateTimeOffset? concluidaEm = null, DateOnly? prazo = null)
    {
        var board = new Board { Id = Guid.NewGuid(), Name = "Quadro", ProjectId = projectId };
        return new WorkItem
        {
            Id = Guid.NewGuid(), Number = Random.Shared.Next(1, 9999), Title = "Tarefa",
            BoardId = board.Id, Board = board, ResponsibleId = responsavel,
            CompletedAt = concluidaEm, DueDate = prazo,
        };
    }

    private static MyWorkDashboardDto Executar(
        IReadOnlyList<WorkItem> tarefas,
        IReadOnlyList<Project> projetos,
        IReadOnlyList<Sprint> sprints)
    {
        var handler = new GetMyWorkDashboardQueryHandler(
            new MyWorkRepositoryFake(tarefas, projetos, sprints),
            new ApprovalRepositoryFake(),
            new UserDirectoryFake(),
            new ProjectAccessFake());
        return handler.Handle(new GetMyWorkDashboardQuery(Eu), default).GetAwaiter().GetResult();
    }

    // ── Projetos vigentes ──────────────────────────────────────────────────

    [Fact]
    public void ProjetoSemTarefaMinha_ContinuaNaLista()
    {
        // Sumir da lista esconderia que a pessoa participa do projeto.
        var projeto = NovoProjeto("VAZIO", "Projeto sem tarefa minha");

        var resultado = Executar([], [projeto], []);

        var cartao = Assert.Single(resultado.Projects);
        Assert.Equal("VAZIO", cartao.Key);
        Assert.Equal(0, cartao.OpenTasks);
        Assert.Equal(0, cartao.ProgressPercentage);
    }

    [Fact]
    public void ProjetoArquivadoOuEncerrado_NaoAparece()
    {
        // O repositório é quem filtra; o fake devolve só ativos, e o handler não
        // reintroduz nada. Aqui provamos o contrato do bloco: lista só o que veio.
        var ativo = NovoProjeto("ATIVO", "Ativo");

        var resultado = Executar([], [ativo], []);

        Assert.Single(resultado.Projects);
        Assert.Equal("ATIVO", resultado.Projects[0].Key);
    }

    [Fact]
    public void ProjetoComAtraso_VemAntesDoQueSoTemTrabalhoAberto()
    {
        var comAtraso = NovoProjeto("ATRASO", "Com atraso");
        var soAberto = NovoProjeto("ABERTO", "Só aberto");
        var tarefas = new[]
        {
            NovaTarefa(comAtraso.Id, prazo: Hoje.AddDays(-3)),
            NovaTarefa(soAberto.Id), NovaTarefa(soAberto.Id), NovaTarefa(soAberto.Id),
        };

        var resultado = Executar(tarefas, [soAberto, comAtraso], []);

        Assert.Equal("ATRASO", resultado.Projects[0].Key);
        Assert.Equal(1, resultado.Projects[0].OverdueTasks);
    }

    [Fact]
    public void ProgressoDoProjeto_ContaSomenteAsTarefasDaPessoa()
    {
        var projeto = NovoProjeto("PROG", "Progresso");
        var tarefas = new[]
        {
            NovaTarefa(projeto.Id, concluidaEm: DateTimeOffset.UtcNow),
            NovaTarefa(projeto.Id),
            // Tarefa de outra pessoa não entra no recorte: o repositório não a traz.
        };

        var resultado = Executar(tarefas, [projeto], []);

        var cartao = Assert.Single(resultado.Projects);
        Assert.Equal(1, cartao.OpenTasks);
        Assert.Equal(1, cartao.CompletedTasks);
        Assert.Equal(50, cartao.ProgressPercentage);
    }

    // ── Sprints em curso ───────────────────────────────────────────────────

    [Fact]
    public void SprintEncerradaPorData_NaoApareceEmCurso()
    {
        var projeto = NovoProjeto("SPR", "Com sprints");
        var vencida = Sprint.Criar(projeto.Id, null, "Vencida", Hoje.AddDays(-60), Hoje.AddDays(-30), null);
        var corrente = Sprint.Criar(projeto.Id, null, "Corrente", Hoje.AddDays(-2), Hoje.AddDays(10), null);
        var futura = Sprint.Criar(projeto.Id, null, "Futura", Hoje.AddDays(20), Hoje.AddDays(34), null);
        foreach (var s in new[] { vencida, corrente, futura }) s.Project = projeto;

        var resultado = Executar([], [projeto], [vencida, corrente, futura]);

        var emCurso = Assert.Single(resultado.Sprints);
        Assert.Equal("Corrente", emCurso.Name);
    }

    [Fact]
    public void SprintEmCurso_SeparaProgressoGeralDoProgressoPessoal()
    {
        // É a diferença entre "a sprint vai bem" e "eu estou em dia".
        var projeto = NovoProjeto("SPR", "Com sprint");
        var sprint = Sprint.Criar(projeto.Id, null, "Sprint", Hoje.AddDays(-2), Hoje.AddDays(10), null);
        sprint.Project = projeto;
        // Time entregou 3 de 4; eu não entreguei nenhuma das minhas 2.
        sprint.WorkItems.Add(NovaTarefa(projeto.Id, Outro, DateTimeOffset.UtcNow));
        sprint.WorkItems.Add(NovaTarefa(projeto.Id, Outro, DateTimeOffset.UtcNow));
        sprint.WorkItems.Add(NovaTarefa(projeto.Id, Eu, DateTimeOffset.UtcNow));
        sprint.WorkItems.Add(NovaTarefa(projeto.Id, Eu));

        var resultado = Executar([], [projeto], [sprint]);

        var emCurso = Assert.Single(resultado.Sprints);
        Assert.Equal(4, emCurso.TotalItems);
        Assert.Equal(3, emCurso.TotalCompleted);
        Assert.Equal(75, emCurso.TotalProgressPercentage);
        Assert.Equal(2, emCurso.MyItems);
        Assert.Equal(1, emCurso.MyCompleted);
        Assert.Equal(50, emCurso.MyProgressPercentage);
    }

    [Fact]
    public void SprintQueEncerraEmDoisDias_RecebeDestaque()
    {
        var projeto = NovoProjeto("SPR", "Com sprint");
        var apertada = Sprint.Criar(projeto.Id, null, "Apertada", Hoje.AddDays(-10), Hoje.AddDays(1), null);
        var folgada = Sprint.Criar(projeto.Id, null, "Folgada", Hoje.AddDays(-1), Hoje.AddDays(20), null);
        foreach (var s in new[] { apertada, folgada }) s.Project = projeto;

        var resultado = Executar([], [projeto], [apertada, folgada]);

        // Ordenadas pelo que encerra antes.
        Assert.Equal("Apertada", resultado.Sprints[0].Name);
        Assert.True(resultado.Sprints[0].EndingSoon);
        Assert.False(resultado.Sprints[1].EndingSoon);
    }

    [Fact]
    public void ProjetoMostraSprintEmCurso_ComDiasRestantes()
    {
        var projeto = NovoProjeto("SPR", "Com sprint");
        var sprint = Sprint.Criar(projeto.Id, null, "Sprint atual", Hoje.AddDays(-3), Hoje.AddDays(5), null);
        sprint.Project = projeto;

        var resultado = Executar([], [projeto], [sprint]);

        var cartao = Assert.Single(resultado.Projects);
        Assert.Equal(sprint.Id, cartao.CurrentSprintId);
        Assert.Equal("Sprint atual", cartao.CurrentSprintName);
        Assert.Equal(5, cartao.CurrentSprintDaysLeft);
    }

    // ── Horas da semana ────────────────────────────────────────────────────

    [Fact]
    public void Horas_SomamSomenteLancamentosEncerradosDaPessoaNaSemana()
    {
        var projeto = NovoProjeto("HORAS", "Horas");
        var tarefa = NovaTarefa(projeto.Id);
        var agora = DateTimeOffset.UtcNow;
        // Duas horas minhas, hoje.
        tarefa.TimeEntries.Add(TimeEntry.Manual(tarefa.Id, Eu, agora.AddHours(-2), agora, null));
        // Cronômetro aberto não conta: só lançamento encerrado entra no total.
        tarefa.TimeEntries.Add(TimeEntry.IniciarAgora(tarefa.Id, Eu, null));
        // Apontamento de outra pessoa não conta.
        tarefa.TimeEntries.Add(TimeEntry.Manual(tarefa.Id, Outro, agora.AddHours(-5), agora, null));

        var resultado = Executar([tarefa], [projeto], []);

        Assert.Equal(2m, Math.Round(resultado.Hours.WeekHours));
    }

    [Fact]
    public void Horas_NaoExpoemNenhumaMetricaFinanceira()
    {
        // D23 e catálogo de relatórios: nunca custo, valor-hora ou faturamento.
        var proibidos = new[] { "Cost", "Salary", "Billing", "Profit", "Rate", "Value" };
        var propriedades = typeof(MyWorkHoursDto).GetProperties().Select(x => x.Name);

        Assert.DoesNotContain(propriedades,
            nome => proibidos.Any(t => nome.Contains(t, StringComparison.OrdinalIgnoreCase)));
    }

    // ── fakes ──────────────────────────────────────────────────────────────

    private sealed class MyWorkRepositoryFake(
        IReadOnlyList<WorkItem> tarefas,
        IReadOnlyList<Project> projetos,
        IReadOnlyList<Sprint> sprints) : IMyWorkRepository
    {
        public Task<IReadOnlyList<WorkItem>> GetTasksAsync(string userId, CancellationToken ct = default)
            => Task.FromResult(tarefas);
        public Task<IReadOnlyList<Comment>> GetRecentCommentsAsync(
            string userId, string? userEmail, int limit, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Comment>>([]);
        public Task<IReadOnlyList<Project>> GetActiveProjectsAsync(string userId, CancellationToken ct = default)
            => Task.FromResult(projetos);
        public Task<IReadOnlyList<Sprint>> GetSprintsAsync(string userId, CancellationToken ct = default)
            => Task.FromResult(sprints);
    }

    private sealed class ApprovalRepositoryFake : IApprovalRepository
    {
        public Task<Approval?> GetByIdForApproverAsync(Guid id, string approverId, CancellationToken cancellationToken = default)
            => Task.FromResult<Approval?>(null);
        public Task<IReadOnlyList<Approval>> GetByWorkItemAsync(Guid workItemId, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Approval>>([]);
        public Task<IReadOnlyList<Approval>> GetByApproverAsync(string approverId, int limite, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<Approval>>([]);
        public Task<bool> HasPendingAsync(Guid workItemId, CancellationToken cancellationToken = default)
            => Task.FromResult(false);
        public Task AddAsync(Approval approval, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class UserDirectoryFake : IUserDirectory
    {
        public Task<IReadOnlyDictionary<string, string>> GetDisplayNamesAsync(
            IEnumerable<string> userIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());
        public Task<IReadOnlyList<UserSummary>> GetAllAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserSummary>>([]);
        public Task<IReadOnlyList<UserSummary>> GetByIdsAsync(
            IEnumerable<string> userIds, bool includeInactive = false, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<UserSummary>>([]);
        public Task<UserSummary?> GetByIdAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<UserSummary?>(null);
        public Task<UserSummary?> GetByIdUnscopedAsync(string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<UserSummary?>(null);
        public Task<UserSummary?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
            => Task.FromResult<UserSummary?>(null);
    }

    private sealed class ProjectAccessFake : IProjectAccessService
    {
        public Task<ProjectRole?> GetRoleAsync(Guid projectId, string userId, CancellationToken cancellationToken = default)
            => Task.FromResult<ProjectRole?>(ProjectRole.Member);

        public Task EnsureAtLeastAsync(Guid projectId, string userId, ProjectRole minimumRole, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
