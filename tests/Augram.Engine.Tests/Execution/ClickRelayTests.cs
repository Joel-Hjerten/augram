using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Engine.Execution;
using Xunit;

namespace Augram.Engine.Tests.Execution;

/// <summary><see cref="ClickRelay.Replay"/>'s whole injection, in the one order it is made (S7 review note).</summary>
public sealed class ClickRelayTests
{
    [Fact]
    public void KeysDown_TheClick_ACtrlTapForAlt_KeysUpInReverse()
    {
        var simulator = new FakeInputSimulator();

        var result = ClickRelay.Replay(simulator, MouseButton.Right, 10, 20, KeyModifiers.Alt | KeyModifiers.Shift);

        Assert.Equal(SimulationResult.Success, result);
        Assert.Equal(
            ["press LeftAlt", "press LeftShift", "click Right@10,20", "press LeftControl", "release LeftControl", "release LeftShift", "release LeftAlt"],
            simulator.All);
    }
}
