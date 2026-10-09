using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Tests.HoldRemaps.Support;
using Xunit;

namespace Augram.Core.Tests.HoldRemaps;

/// <summary>
/// Key inputs and Steps commands (F9): a Remap key output mirrors the input's down, repeats and up, with its modifiers held for
/// the whole press; a Steps command runs once per press (repeats ignored) and once per wheel notch. Plus the pairing of every
/// output across the hold key's release and across a reset (A19).
/// </summary>
public sealed class HoldRemapKeyAndStepsTests
{
    [Fact]
    public void SpaceWIsG_DownRepeatsAndUpMirrored()
    {
        var script = new HoldScript().Hold().After(250).KeyDown(KeyCode.W).KeyRepeat(KeyCode.W).KeyRepeat(KeyCode.W).KeyUp(KeyCode.W).Release();

        Assert.Equal(["press G", "repeat G", "repeat G", "release G"], script.Work);
        Assert.All(script.Decisions, decision => Assert.Equal("suppress", decision));
    }

    [Fact]
    public void SpaceRIsCtrlShiftAltR_SpaceFIsNumDot()
    {
        var script = new HoldScript().Hold().Type(KeyCode.R).Type(KeyCode.F).Release();

        Assert.Equal(["press Ctrl+Alt+Shift+R", "release Ctrl+Alt+Shift+R", "press Num .", "release Num ."], script.Work);
    }

    [Fact]
    public void AnInputKeyHeldPastTheHoldKeysReleaseIsStillMirroredUntilItsOwnRelease()
    {
        var script = new HoldScript().Hold().KeyDown(KeyCode.W).Release().KeyRepeat(KeyCode.W).KeyUp(KeyCode.W).Clear().Type(KeyCode.W);

        Assert.Empty(script.Work);
        Assert.Equal(["pass", "pass"], script.Decisions);
        Assert.Equal(HoldRemapState.Idle, script.Machine.State);
    }

    [Fact]
    public void AnInputKeyAfterTheHoldOrBeforeItIsJustTheKey()
    {
        var script = new HoldScript().KeyDown(KeyCode.W).Hold().KeyRepeat(KeyCode.W).KeyUp(KeyCode.W).After(300).Release().Type(KeyCode.E);

        Assert.Empty(script.Work);
        Assert.Equal(["pass", "suppress", "pass", "pass", "suppress", "pass", "pass"], script.Decisions);
    }

    [Fact]
    public void AStepsCommandRunsOncePerPress_RepeatsIgnored()
    {
        var script = new HoldScript().Hold().KeyDown(KeyCode.Q);
        for (var i = 0; i < 5; i++)
        {
            script.After(30).KeyRepeat(KeyCode.Q);
        }

        script.KeyUp(KeyCode.Q).After(50).Type(KeyCode.Q).Release();

        Assert.Equal(["run Note", "run Note"], script.Work);
        Assert.All(script.Decisions, decision => Assert.Equal("suppress", decision));
    }

    [Fact]
    public void AStepsCommandOnAWheelInputRunsOncePerNotch_TheOtherDirectionPasses()
    {
        var script = new HoldScript().Hold().Wheel(WheelDirection.Down).Wheel(WheelDirection.Down).Wheel(WheelDirection.Up).Wheel(WheelDirection.Down).Release();

        Assert.Equal(["run Nudge", "run Nudge", "run Nudge"], script.Work);
        Assert.Equal(["suppress", "suppress", "suppress", "pass", "suppress", "suppress"], script.Decisions);
    }

    [Fact]
    public void ResetWithOutputsHeldReleasesEveryOne_ThenThePhysicalReleasesPass()
    {
        var script = new HoldScript()
            .Hold()
            .Down(MouseButton.Left)
            .KeyDown(KeyCode.W)
            .KeyDown(KeyCode.E)
            .Reset();

        Assert.Equal(["press Middle", "press G", "press S", "release Middle", "release S", "release G"], script.Work);
        Assert.Equal(HoldRemapState.Idle, script.Machine.State);

        script.Clear().Up(MouseButton.Left).KeyUp(KeyCode.W).KeyUp(KeyCode.E).Release();

        Assert.Empty(script.Work);
        Assert.Equal(["pass", "pass", "pass", "suppress"], script.Decisions);
    }

    [Fact]
    public void ResetDuringARolloverReplaysTheKeysRelease()
    {
        var script = new HoldScript().Hold().After(20).KeyDown(KeyCode.B).Reset();

        Assert.Equal(["tap Space", "replay B down", "replay B up"], script.Work);
    }

    [Fact]
    public void AnotherHoldKeyDuringAHoldIsAnOrdinaryKeyOfThisHold()
    {
        var space = Blender.NewSpace();
        var group = Blender.Group(space) with { HoldRemaps = [space, HoldRemap.For(KeyCode.S)] };
        var other = HoldRemapPlan.ForGroup(Blender.Document(group).Groups[1], HostPlatform.Windows).Find(KeyCode.S)!;
        var script = new HoldScript().Hold().After(20);

        script.Feed(new HoldRemapEvent.HoldDown(other, 20)).Feed(new HoldRemapEvent.HoldUp(KeyCode.S, 40)).After(60).Release();

        Assert.Equal(["tap Space", "replay S down", "replay S up"], script.Work);
        Assert.Equal(HoldRemapState.Idle, script.Machine.State);
    }
}
