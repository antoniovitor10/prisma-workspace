using Detran.Kanban.Application.Features.Productivity;
using Detran.Kanban.Domain.Enums;

namespace Detran.Kanban.Tests;

public sealed class ProductivityFeatureTests
{
    [Fact]
    public void AutomationGraph_AcceptsAnAcyclicChain()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();

        var hasCycle = AutomationRuleGuard.HasCycle(new[]
        {
            (Source: first, Target: second),
            (Source: second, Target: third)
        });

        Assert.False(hasCycle);
    }

    [Fact]
    public void AutomationGraph_RejectsAStageCycle()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();

        var hasCycle = AutomationRuleGuard.HasCycle(new[]
        {
            (Source: first, Target: second),
            (Source: second, Target: third),
            (Source: third, Target: first)
        });

        Assert.True(hasCycle);
    }

    [Fact]
    public void BulkValidator_RejectsEmptyAndOversizedSelections()
    {
        var validator = new BulkWorkItemsCommandValidator();
        var empty = validator.Validate(new BulkWorkItemsCommand(
            Guid.NewGuid(), Array.Empty<Guid>(), BulkActionType.SetPriority,
            null, Priority.High, "actor"));
        var oversized = validator.Validate(new BulkWorkItemsCommand(
            Guid.NewGuid(), Enumerable.Range(0, 201).Select(_ => Guid.NewGuid()).ToList(),
            BulkActionType.SetPriority, null, Priority.High, "actor"));

        Assert.False(empty.IsValid);
        Assert.False(oversized.IsValid);
        Assert.All(empty.Errors.Concat(oversized.Errors), error =>
            Assert.Contains("200", error.ErrorMessage));
    }

    [Fact]
    public void BulkValidator_RejectsDuplicateTaskIds()
    {
        var id = Guid.NewGuid();
        var validator = new BulkWorkItemsCommandValidator();

        var result = validator.Validate(new BulkWorkItemsCommand(
            Guid.NewGuid(), new[] { id, id }, BulkActionType.Move,
            Guid.NewGuid().ToString(), null, "actor"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("repetidas"));
    }
}
