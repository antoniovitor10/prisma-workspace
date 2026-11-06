using Prisma.Workspace.Application.Features.WorkItems.Commands;
using Prisma.Workspace.Domain.Enums;

namespace Prisma.Workspace.Tests;

public class KanbanCreationPositionTests
{
    [Theory]
    [InlineData(-200)]
    [InlineData(-100)]
    [InlineData(0)]
    [InlineData(100)]
    public void CreationAllowsFinitePositionsBeforeZero(double position)
    {
        var result = new CreateWorkItemCommandValidator().Validate(Command(position));
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(error => error.ErrorMessage)));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(-999_999_999_999d)]
    [InlineData(999_999_999_999d)]
    public void CreationStillRejectsNonFiniteAndOutOfRangePositions(double position)
    {
        var result = new CreateWorkItemCommandValidator().Validate(Command(position));
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateWorkItemCommand.Position)
            && error.ErrorMessage == "A posição da tarefa está fora do intervalo permitido.");
    }

    private static CreateWorkItemCommand Command(double position)
        => new(Guid.NewGuid(), null, null, "Tarefa", null, null, Priority.Medium,
            null, null, position, "user", ResponsibleId: "user");
}
