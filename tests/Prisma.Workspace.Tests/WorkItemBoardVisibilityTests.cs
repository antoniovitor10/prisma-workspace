using Prisma.Workspace.Domain.Entities;
using Prisma.Workspace.Domain.Enums;
using Prisma.Workspace.Domain.Services;

namespace Prisma.Workspace.Tests;

/// <summary>
/// T-0022 (CA-4): regra única de quais itens viram card de quadro. Cada tipo de item contra cada
/// situação de pai (topo, sob Épico, Funcionalidade, História, Tarefa, Bug e Subtarefa).
/// </summary>
public class WorkItemBoardVisibilityTests
{
    private static readonly WorkItemKind[] PlanningKinds = [WorkItemKind.Epic, WorkItemKind.Feature, WorkItemKind.UserStory];

    public static TheoryData<WorkItemKind, WorkItemKind?, bool> Cases()
    {
        var data = new TheoryData<WorkItemKind, WorkItemKind?, bool>();
        WorkItemKind?[] parents = [null, .. Enum.GetValues<WorkItemKind>().Select(kind => (WorkItemKind?)kind)];
        foreach (var kind in Enum.GetValues<WorkItemKind>())
            foreach (var parent in parents)
            {
                var expected = !PlanningKinds.Contains(kind)
                    && (parent is null || PlanningKinds.Contains(parent.Value));
                data.Add(kind, parent, expected);
            }
        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void IsBoardCard_follows_the_single_rule_for_every_kind_and_parent(
        WorkItemKind kind, WorkItemKind? parentKind, bool expected)
    {
        Assert.Equal(expected, WorkItemBoardVisibility.IsBoardCard(kind, parentKind));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Expression_matches_the_function_for_every_kind_and_parent(
        WorkItemKind kind, WorkItemKind? parentKind, bool expected)
    {
        var item = Build(kind, parentKind);

        Assert.Equal(expected, WorkItemBoardVisibility.IsBoardCardExpression.Compile()(item));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Personal_list_keeps_planning_items_and_every_card(
        WorkItemKind kind, WorkItemKind? parentKind, bool isCard)
    {
        var expected = PlanningKinds.Contains(kind) || isCard;
        var item = Build(kind, parentKind);

        Assert.Equal(expected, WorkItemBoardVisibility.IsPersonalListItem(kind, parentKind));
        Assert.Equal(expected, WorkItemBoardVisibility.IsPersonalListItemExpression.Compile()(item));
    }

    [Theory]
    [InlineData(WorkItemKind.Task, null, true)]                       // Tarefa de topo
    [InlineData(WorkItemKind.Bug, null, true)]                        // Bug de topo
    [InlineData(WorkItemKind.Task, WorkItemKind.UserStory, true)]     // Tarefa filha da História (task do método)
    [InlineData(WorkItemKind.Task, WorkItemKind.Epic, true)]          // execução sob Épico
    [InlineData(WorkItemKind.Bug, WorkItemKind.Feature, true)]        // execução sob Funcionalidade
    [InlineData(WorkItemKind.Subtask, WorkItemKind.Task, false)]      // Subtarefa continua dentro do card
    [InlineData(WorkItemKind.Task, WorkItemKind.Task, false)]         // Tarefa sob Tarefa não é card
    [InlineData(WorkItemKind.UserStory, null, false)]                 // História de topo sai do quadro
    [InlineData(WorkItemKind.UserStory, WorkItemKind.Feature, false)]
    [InlineData(WorkItemKind.Feature, WorkItemKind.Epic, false)]
    [InlineData(WorkItemKind.Epic, null, false)]
    public void Scenarios_from_the_acceptance_criteria(WorkItemKind kind, WorkItemKind? parentKind, bool expected)
    {
        Assert.Equal(expected, WorkItemBoardVisibility.IsBoardCard(kind, parentKind));
    }

    [Fact]
    public void Planning_kinds_are_exactly_epic_feature_and_story()
    {
        var planning = Enum.GetValues<WorkItemKind>().Where(WorkItemBoardVisibility.IsPlanningKind).ToArray();

        Assert.Equal(PlanningKinds, planning);
    }

    private static WorkItem Build(WorkItemKind kind, WorkItemKind? parentKind)
    {
        var item = new WorkItem { Id = Guid.NewGuid(), Kind = kind, Title = "Item" };
        if (parentKind is not null)
        {
            var parent = new WorkItem { Id = Guid.NewGuid(), Kind = parentKind.Value, Title = "Pai" };
            item.ParentId = parent.Id;
            item.Parent = parent;
        }
        return item;
    }
}
