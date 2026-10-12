using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Xunit;

namespace Augram.Core.Tests.Steps;

/// <summary>
/// <see cref="InputSimulation"/>: the keys around an injection go down in order and up in reverse, every one of them whatever
/// an earlier injection answered, and the worst answer is the call's.
/// </summary>
public sealed class InputSimulationTests
{
    private static readonly KeyCode[] CtrlShift = [KeyCode.LeftControl, KeyCode.LeftShift];

    [Fact]
    public void WithModifiersHeld_PressesInOrder_RunsTheAction_ReleasesInReverse()
    {
        var input = new FakeInputSimulator();

        var result = input.WithModifiersHeld(CtrlShift, () => input.Click(MouseButton.Right, 1, 2));

        Assert.Equal(SimulationResult.Success, result);
        Assert.Equal(["press LeftControl", "press LeftShift", "click Right@1,2", "release LeftShift", "release LeftControl"], input.All);
    }

    [Fact]
    public void WithModifiersHeld_NoKeys_IsTheActionAlone()
    {
        var input = new FakeInputSimulator { OtherResult = SimulationResult.Failed };

        Assert.Equal(SimulationResult.Failed, input.WithModifiersHeld([], () => input.Click(MouseButton.Left, 0, 0)));
        Assert.Equal(["click Left@0,0"], input.All);
    }

    [Fact]
    public void WithModifiersHeld_AFailedPressStillRunsEverything_AndTheWorstAnswerIsReturned()
    {
        var input = new FakeInputSimulator { PressResult = SimulationResult.Failed };

        var result = input.WithModifiersHeld(CtrlShift, () => SimulationResult.Unsupported);

        Assert.Equal(SimulationResult.Unsupported, result);
        Assert.Equal(["press LeftControl", "press LeftShift", "release LeftShift", "release LeftControl"], input.All);
    }

    [Fact]
    public void PressAndReleaseHalves_AreTheBracketSplitInTwo()
    {
        var input = new FakeInputSimulator { ReleaseResult = SimulationResult.Failed };

        Assert.Equal(SimulationResult.Success, input.PressKeys(CtrlShift));
        Assert.Equal(SimulationResult.Failed, input.ReleaseKeys(CtrlShift));
        Assert.Equal(["press LeftControl", "press LeftShift", "release LeftShift", "release LeftControl"], input.All);
    }

    [Theory]
    [InlineData(SimulationResult.Success, SimulationResult.Success, SimulationResult.Success)]
    [InlineData(SimulationResult.Success, SimulationResult.Failed, SimulationResult.Failed)]
    [InlineData(SimulationResult.Failed, SimulationResult.Success, SimulationResult.Failed)]
    [InlineData(SimulationResult.Failed, SimulationResult.Unsupported, SimulationResult.Unsupported)]
    [InlineData(SimulationResult.Unsupported, SimulationResult.Failed, SimulationResult.Unsupported)]
    public void Worst_TheLaterMemberWins(SimulationResult a, SimulationResult b, SimulationResult expected)
        => Assert.Equal(expected, InputSimulation.Worst(a, b));
}
