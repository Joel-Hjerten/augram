using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Tests.HoldRemaps.Support;
using Xunit;

namespace Augram.Core.Tests.HoldRemaps;

/// <summary>
/// Typing rollover (F9; Joel has hit it "many times" with the script): a key that is no input, pressed while the hold key is
/// down, within the tap time and before anything was used, is typing: the hold key goes first, then that key, and the hold
/// remap is off until the hold key is released. "a b" typed fast stays "a b", not "ab ".
/// </summary>
public sealed class HoldRemapRolloverTests
{
    [Fact]
    public void ASpaceBTypedFast_StaysASpaceB()
    {
        var script = new HoldScript()
            .KeyDown(KeyCode.A)
            .After(40).Hold()
            .After(10).KeyUp(KeyCode.A)
            .After(40).KeyDown(KeyCode.B)
            .After(30).Release()
            .After(20).KeyUp(KeyCode.B);

        Assert.Equal(["tap Space", "replay B down", "replay B up"], script.Work);
        Assert.Equal(["pass", "suppress", "pass", "suppress", "suppress", "suppress"], script.Decisions);
        Assert.Equal(HoldRemapState.Idle, script.Machine.State);
    }

    [Fact]
    public void TheRolledOverKeysRepeatsAndReleaseAreReplayedInTurn_AlsoBeforeTheHoldKeyIsUp()
    {
        var script = new HoldScript().Hold().After(50).KeyDown(KeyCode.B).KeyRepeat(KeyCode.B).KeyUp(KeyCode.B).After(10).Release();

        Assert.Equal(["tap Space", "replay B down", "replay B repeat", "replay B up"], script.Work);
        Assert.Equal(HoldRemapState.Idle, script.Machine.State);
    }

    [Fact]
    public void AfterARolloverTheHoldRemapIsOff_InputsPassAndNothingMoreIsSent()
    {
        var script = new HoldScript().Hold().After(50).Type(KeyCode.B).Clear()
            .After(20).Type(KeyCode.W)
            .Click(MouseButton.Left)
            .Wheel(WheelDirection.Down)
            .After(300).Release();

        Assert.Empty(script.Work);
        Assert.Equal(["pass", "pass", "pass", "pass", "pass", "suppress"], script.Decisions);
    }

    [Fact]
    public void NoRolloverOnceSomethingWasUsed()
    {
        var script = new HoldScript().Hold().After(20).Type(KeyCode.W).Clear().After(20).Type(KeyCode.B).Release();

        Assert.Empty(script.Work);
        Assert.Equal(["pass", "pass", "suppress"], script.Decisions);
    }

    [Fact]
    public void AKeyHeldBeforeTheHoldOnlyPasses_ItsRepeatIsNoPress()
    {
        var script = new HoldScript().KeyDown(KeyCode.B).After(30).Hold().After(30).KeyRepeat(KeyCode.B).KeyUp(KeyCode.B).After(30).Release();

        Assert.Equal(["tap Space"], script.Work);
        Assert.Equal(["pass", "suppress", "pass", "pass", "suppress"], script.Decisions);
    }

    [Fact]
    public void AReplayedKeyStillDownAtTheNextHoldIsReplayedUp_AndTheNewHoldCanTap()
    {
        var script = new HoldScript().Hold().After(30).KeyDown(KeyCode.B).Release()
            .After(20).Hold()
            .After(20).KeyUp(KeyCode.B)
            .After(20).Release();

        Assert.Equal(["tap Space", "replay B down", "replay B up", "tap Space"], script.Work);
    }
}
