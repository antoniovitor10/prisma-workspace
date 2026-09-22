using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Exceptions;

namespace Prisma.Workspace.Tests;

/// <summary>
/// Estado funcional da sprint derivado das datas (D84 / SPEC-S-003 v3).
///
/// O defeito de origem: o PO criou uma sprint de 1 a 30 de janeiro e conseguiu adicionar
/// tarefas nela em setembro, porque o estado era uma coluna persistida que ninguém tinha
/// mudado para encerrada.
/// </summary>
public class SprintStatusByDatesTests
{
    private static Sprint Nova(DateOnly inicio, DateOnly fim)
        => Sprint.Criar(Guid.NewGuid(), null, "Sprint", inicio, fim, null);

    [Fact]
    public void AntesDoInicio_EstaPlanejada()
    {
        var sprint = Nova(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15));
        Assert.Equal(SprintStatus.Planned, sprint.StatusEm(new DateOnly(2026, 9, 30)));
    }

    [Theory]
    [InlineData(2026, 10, 1)]   // primeiro dia, inclusive
    [InlineData(2026, 10, 8)]   // meio do período
    [InlineData(2026, 10, 15)]  // último dia, inclusive
    public void DentroDoPeriodo_EstaAtiva(int ano, int mes, int dia)
    {
        var sprint = Nova(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15));
        Assert.Equal(SprintStatus.Active, sprint.StatusEm(new DateOnly(ano, mes, dia)));
    }

    [Fact]
    public void DepoisDaDataFinal_EstaEncerradaSemAcaoManual()
    {
        // O caso relatado: sprint de janeiro consultada em setembro.
        var sprint = Nova(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 30));

        Assert.Equal(SprintStatus.Closed, sprint.StatusEm(new DateOnly(2026, 9, 9)));
        Assert.True(sprint.EstaEncerradaEm(new DateOnly(2026, 9, 9)));
        // E sem que ninguém tenha encerrado explicitamente.
        Assert.Null(sprint.CompletedAt);
    }

    [Fact]
    public void SprintDeUmDiaSo_EstaAtivaNesseDia()
    {
        var dia = new DateOnly(2026, 10, 5);
        var sprint = Nova(dia, dia);

        Assert.Equal(SprintStatus.Planned, sprint.StatusEm(dia.AddDays(-1)));
        Assert.Equal(SprintStatus.Active, sprint.StatusEm(dia));
        Assert.Equal(SprintStatus.Closed, sprint.StatusEm(dia.AddDays(1)));
    }

    [Fact]
    public void EncerramentoExplicito_PrevaleceSobreAsDatas()
    {
        var sprint = Nova(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15));
        sprint.Encerrar(SprintStatus.Closed, new DateOnly(2026, 10, 8));

        // Mesmo no meio do período, encerrada explicitamente continua encerrada.
        Assert.Equal(SprintStatus.Closed, sprint.StatusEm(new DateOnly(2026, 10, 8)));
        Assert.NotNull(sprint.CompletedAt);
    }

    [Fact]
    public void Cancelamento_PrevaleceSobreEncerramentoPorData()
    {
        var sprint = Nova(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 30));
        sprint.Encerrar(SprintStatus.Cancelled, new DateOnly(2026, 1, 10));

        Assert.Equal(SprintStatus.Cancelled, sprint.StatusEm(new DateOnly(2026, 9, 9)));
        Assert.NotNull(sprint.CancelledAt);
    }

    [Fact]
    public void VariasSprintsDoMesmoProjeto_PodemEstarAtivasAoMesmoTempo()
    {
        // A regra de "somente uma ativa por projeto" foi revogada pela D84.
        var projeto = Guid.NewGuid();
        var hoje = new DateOnly(2026, 10, 8);
        var a = Sprint.Criar(projeto, null, "A", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15), null);
        var b = Sprint.Criar(projeto, null, "B", new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 20), null);

        Assert.Equal(SprintStatus.Active, a.StatusEm(hoje));
        Assert.Equal(SprintStatus.Active, b.StatusEm(hoje));
    }

    [Fact]
    public void NaoExisteTransicaoManualParaAtiva()
    {
        var sprint = Nova(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15));

        var erro = Assert.Throws<DomainException>(
            () => sprint.Encerrar(SprintStatus.Active, new DateOnly(2026, 9, 30)));
        Assert.Contains("datas", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SprintJaEncerrada_NaoMudaDeEstado()
    {
        var sprint = Nova(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15));
        sprint.Encerrar(SprintStatus.Closed, new DateOnly(2026, 10, 8));

        Assert.Throws<DomainException>(
            () => sprint.Encerrar(SprintStatus.Cancelled, new DateOnly(2026, 10, 9)));
    }

    [Fact]
    public void SprintEncerradaExplicitamente_NaoPodeSerEditada()
    {
        var sprint = Nova(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 15));
        sprint.Encerrar(SprintStatus.Closed, new DateOnly(2026, 10, 8));

        Assert.Throws<DomainException>(
            () => sprint.Update("Novo nome", null, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 20)));
    }
}
