using System.Collections.Immutable;
using Augram.App.ViewModels.Commands;
using Xunit;

namespace Augram.App.Tests.Commands;

public sealed class StepIndexShiftTests
{
    [Theory]
    [InlineData(0, 2, 0, 2)]
    [InlineData(0, 2, 1, 0)]
    [InlineData(0, 2, 2, 1)]
    [InlineData(2, 0, 0, 1)]
    [InlineData(2, 0, 1, 2)]
    [InlineData(2, 0, 2, 0)]
    [InlineData(0, 1, 2, 2)]
    [InlineData(1, 2, -1, -1)]
    public void AMoveTakesAnIndexWithItsStep(int from, int to, int index, int expected)
        => Assert.Equal(expected, StepIndexShift.AfterMove(index, from, to));

    [Fact]
    public void TheOpenStepsKeepTheirStepsAcrossAnInsertADeleteAndAMove()
    {
        var open = ImmutableSortedSet.Create(0, 2);

        Assert.Equal([0, 2, 3], StepIndexShift.AfterInsert(open, 2));
        Assert.Equal([0, 2, 4], StepIndexShift.AfterInsert(open, 4));
        Assert.Equal([1], StepIndexShift.AfterRemove(open, 0));
        Assert.Equal([0, 1], StepIndexShift.AfterRemove(open, 1));
        Assert.Equal([1, 2], StepIndexShift.AfterMove(open, 0, 2));
        Assert.Equal([0], StepIndexShift.Clamp(open, 2));
        Assert.Equal(-1, StepIndexShift.AfterRemove(2, 2));
        Assert.Equal(-1, StepIndexShift.AfterInsert(-1, 0));
    }

    [Fact]
    public void ToggleOpensOrClosesOneStepAndLeavesTheOthers()
    {
        var open = ImmutableSortedSet.Create(0, 2);

        Assert.Equal([0, 1, 2], StepIndexShift.Toggle(open, 1));
        Assert.Equal([2], StepIndexShift.Toggle(open, 0));
    }
}
