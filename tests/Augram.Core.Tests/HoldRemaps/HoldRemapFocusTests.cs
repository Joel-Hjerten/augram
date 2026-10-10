using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Tests.HoldRemaps.Support;
using Xunit;

namespace Augram.Core.Tests.HoldRemaps;

/// <summary>
/// Focus moving away mid-hold (Joel, 2026-10-10): a hold with nothing owed ends at once, without a tap, and its hold key's
/// repeats and release stay swallowed; a hold with an input still owed keeps following it and ends with the last release.
/// </summary>
public sealed class HoldRemapFocusTests
{
    [Fact]
    public void AHoldWithNothingOwed_EndsWithoutATap_ItsKeyStaysSwallowedToItsRelease()
    {
        var script = new HoldScript().Hold().After(20).FocusMoved();

        Assert.Equal(["end Space"], script.Work);
        Assert.Equal(["suppress"], script.Decisions);
        Assert.Equal(HoldRemapState.Idle, script.Machine.State);

        script.Clear().After(10).KeyRepeat(KeyCode.Space).Down(MouseButton.Left).Up(MouseButton.Left).KeyDown(KeyCode.W).KeyUp(KeyCode.W).After(10).Release();

        Assert.Empty(script.Work);
        Assert.Equal(["suppress", "pass", "pass", "pass", "pass", "suppress"], script.Decisions);
    }

    [Fact]
    public void AHoldWithAnInputOwed_KeepsFollowingIt_ThenEndsAsUsual()
    {
        var script = new HoldScript().Hold().After(10).Down(MouseButton.Left).FocusMoved();

        Assert.Equal(["press Middle"], script.Work);
        Assert.Equal(HoldRemapState.Holding, script.Machine.State);

        script.Clear().After(10).Down(MouseButton.Right).Up(MouseButton.Right).Up(MouseButton.Left).After(10).Release();

        Assert.Equal(["release Middle", "press Ctrl + Middle", "release Ctrl + Middle", "press Middle", "release Middle"], script.Work);
        Assert.All(script.Decisions, decision => Assert.Equal("suppress", decision));
        Assert.Equal(HoldRemapState.Idle, script.Machine.State);
    }

    [Fact]
    public void ARolledOverHoldEnds_TheReplayedKeyStillPairs()
    {
        var script = new HoldScript().Hold().After(50).KeyDown(KeyCode.A).FocusMoved();

        Assert.Equal(["tap Space", "replay A down", "end Space"], script.Work);

        script.Clear().KeyUp(KeyCode.A).Release();

        Assert.Equal(["replay A up"], script.Work);
        Assert.Equal(["suppress", "suppress"], script.Decisions);
    }

    [Fact]
    public void WithNoHold_FocusMovingDoesNothing_AndAResetForgetsAnEndedKey()
    {
        Assert.Empty(new HoldScript().FocusMoved().Work);

        var script = new HoldScript().Hold().FocusMoved().Reset();
        script.Clear().KeyRepeat(KeyCode.Space);

        Assert.Equal(["pass"], script.Decisions);
    }
}
