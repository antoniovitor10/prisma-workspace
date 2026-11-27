using System.Linq.Expressions;
using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Domain.Services;

/// <summary>
/// Regra única de quais itens viram card de quadro (HU-0008, "Quadros sem itens de planejamento").
///
/// Épico, Funcionalidade e História são itens de planejamento: vivem no backlog, na hierarquia
/// e no detalhe, nunca como card. Um item é card quando não é de planejamento e está no topo
/// ou logo abaixo de um item de planejamento. Assim a Tarefa filha da História vira card, e a
/// Subtarefa continua dentro do card da Tarefa, como sempre foi.
///
/// Vale para todos os quadros, de qualquer organização: Kanban, itens do projeto, itens
/// atribuídos, métricas do quadro e quadro da sprint. As expressões são traduzidas pelo EF
/// para SQL (o pai entra como junção); as funções servem para objetos já carregados.
/// </summary>
public static class WorkItemBoardVisibility
{
    /// <summary>Épico, Funcionalidade e História.</summary>
    public static bool IsPlanningKind(WorkItemKind kind)
        => kind is WorkItemKind.Epic or WorkItemKind.Feature or WorkItemKind.UserStory;

    /// <summary>
    /// O item é card de quadro? <paramref name="parentKind"/> é nulo quando o item não tem pai.
    /// </summary>
    public static bool IsBoardCard(WorkItemKind kind, WorkItemKind? parentKind)
        => !IsPlanningKind(kind) && (parentKind is null || IsPlanningKind(parentKind.Value));

    /// <summary>
    /// O item aparece em Minhas tarefas? É lista, não quadro: os cards entram e os itens de
    /// planejamento também continuam aparecendo para quem está ligado a eles.
    /// </summary>
    public static bool IsPersonalListItem(WorkItemKind kind, WorkItemKind? parentKind)
        => IsPlanningKind(kind) || IsBoardCard(kind, parentKind);

    /// <summary>Versão traduzível para SQL de <see cref="IsBoardCard(WorkItemKind, WorkItemKind?)"/>.</summary>
    public static readonly Expression<Func<WorkItem, bool>> IsBoardCardExpression = item =>
        item.Kind != WorkItemKind.Epic
        && item.Kind != WorkItemKind.Feature
        && item.Kind != WorkItemKind.UserStory
        && (item.ParentId == null
            || item.Parent!.Kind == WorkItemKind.Epic
            || item.Parent!.Kind == WorkItemKind.Feature
            || item.Parent!.Kind == WorkItemKind.UserStory);

    /// <summary>Versão traduzível para SQL de <see cref="IsPersonalListItem(WorkItemKind, WorkItemKind?)"/>.</summary>
    public static readonly Expression<Func<WorkItem, bool>> IsPersonalListItemExpression = item =>
        item.Kind == WorkItemKind.Epic
        || item.Kind == WorkItemKind.Feature
        || item.Kind == WorkItemKind.UserStory
        || item.ParentId == null
        || item.Parent!.Kind == WorkItemKind.Epic
        || item.Parent!.Kind == WorkItemKind.Feature
        || item.Parent!.Kind == WorkItemKind.UserStory;
}
