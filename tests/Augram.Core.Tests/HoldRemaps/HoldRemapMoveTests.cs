using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Tests.HoldRemaps.Support;
using Xunit;

namespace Augram.Core.Tests.HoldRemaps;

/// <summary>
/// Moves where the simulator re-posts drags (macOS, learnings 0005; plan 0002 step 3a): while the owed button set's output is
/// a button, a move is swallowed and re-posted as a drag of that output, under whichever output the set holds now; with no
/// button output held (nothing down, a set with no command, a key output) it passes. A move changes nothing.
/// </summary>
public sealed class HoldRemapMoveTests
{
    [Fact]
    public void WithNoButtonOutputHeld_MovesPass()
    {
        var script = new HoldScript().Move(110, 100).Hold().After(10).Move(120, 100).KeyDown(KeyCode.W).Move(130, 100);

        Assert.Equal(["press G"], script.Work);
        Assert.Equal(["pass", "suppress", "pass", "suppress", "pass"], script.Decisions);
    }

    [Fact]
    public void RollingLeft_LeftAndRight_Right_DragsUnderTheOutputHeldNow()
    {
        var script = new HoldScript().Hold().After(10).Down(MouseButton.Left).Clear();

        script.Move(120, 100).Move(130, 105)
            .At(130, 105).Down(MouseButton.Right).Move(140, 110)
            .Up(MouseButton.Left).Move(150, 115)
            .Up(MouseButton.Right).Move(160, 120);

        Assert.Equal(
            [
                "drag Middle at 120,100", "drag Middle at 130,105",
                "release Middle", "press Ctrl + Middle", "drag Ctrl + Middle at 140,110",
                "release Ctrl + Middle", "press Shift + Middle", "drag Shift + Middle at 150,115",
                "release Shift + Middle",
            ],
            script.Work);
        Assert.Equal(["suppress", "suppress", "suppress", "suppress", "suppress", "suppress", "suppress", "pass"], script.Decisions);
    }

    [Fact]
    public void ASetWithNoCommand_PassesMoves_UntilASetWithAButtonOutputIsHeldAgain()
    {
        var script = new HoldScript().Hold().After(10).Down(MouseButton.Left).Down(MouseButton.Middle).Clear();

        script.Move(120, 100).Up(MouseButton.Middle).Move(130, 100);

        Assert.Equal(["press Middle", "drag Middle at 130,100"], script.Work);
        Assert.Equal(["pass", "suppress", "suppress"], script.Decisions);
    }

    [Fact]
    public void AfterTheHoldKeyIsUp_MovesAreDraggedWhileTheButtonsAreFollowed()
    {
        var script = new HoldScript().Hold().After(10).Down(MouseButton.Left).After(10).Release().Clear();

        script.Move(120, 100).Up(MouseButton.Left).Move(130, 100);

        Assert.Equal(["drag Middle at 120,100", "release Middle"], script.Work);
        Assert.Equal(["suppress", "suppress", "pass"], script.Decisions);
        Assert.Equal(HoldRemapState.Idle, script.Machine.State);
    }

    [Fact]
    public void AfterAReset_MovesPass()
    {
        var script = new HoldScript().Hold().After(10).Down(MouseButton.Right).Reset().Clear();

        script.Move(120, 100);

        Assert.Empty(script.Work);
        Assert.Equal(["pass"], script.Decisions);
    }
}
