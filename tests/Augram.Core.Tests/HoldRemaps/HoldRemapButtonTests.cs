using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Tests.HoldRemaps.Support;
using Xunit;

namespace Augram.Core.Tests.HoldRemaps;

/// <summary>
/// Buttons under a hold remap (F9 "Rolling between buttons", Joel 2026-10-10; decision 7): the output held is the command whose
/// set equals the input buttons held now; on every change the old output is released first, then the new one pressed; the
/// remap keeps following the buttons until the last is up, even after the hold key is.
/// </summary>
public sealed class HoldRemapButtonTests
{
    [Fact]
    public void SpaceLeftOrbits_MiddleIsPressedWhereLeftWent_AndHeldUntilLeftIsUp()
    {
        var script = new HoldScript().Hold();

        var outcomes = script.Machine.Handle(new HoldRemapEvent.Button(MouseButton.Left, IsDown: true, 310, 205, 20));
        script.After(500).Up(MouseButton.Left);

        Assert.Equal(HoldRemapOutcome.Suppress.Instance, outcomes[0]);
        var press = Assert.IsType<HoldRemapOutcome.PressOutput>(Assert.Single(outcomes.Skip(1)));
        Assert.Equal((Blender.Middle, 310, 205), (press.Output, press.X, press.Y));
        Assert.Equal(["release Middle"], script.Work);
    }

    [Fact]
    public void RollingLeftToBothToRightToBothToLeft_ReleasesTheOldOutputBeforePressingTheNew()
    {
        var script = new HoldScript()
            .Hold()
            .After(20).Down(MouseButton.Left)
            .After(200).Down(MouseButton.Right)
            .After(200).Up(MouseButton.Left)
            .After(200).Down(MouseButton.Left)
            .After(200).Up(MouseButton.Right)
            .After(200).Up(MouseButton.Left)
            .After(50).Release();

        Assert.Equal(
            [
                "press Middle",
                "release Middle", "press Ctrl + Middle",
                "release Ctrl + Middle", "press Shift + Middle",
                "release Shift + Middle", "press Ctrl + Middle",
                "release Ctrl + Middle", "press Middle",
                "release Middle",
            ],
            script.Work);
        Assert.All(script.Decisions, decision => Assert.Equal("suppress", decision));
        Assert.Equal(HoldRemapState.Idle, script.Machine.State);
    }

    [Fact]
    public void SpaceReleasedBeforeTheButtons_TheRemapFollowsThemUntilTheLastIsUp()
    {
        var script = new HoldScript()
            .Hold()
            .After(20).Down(MouseButton.Left)
            .After(300).Release()
            .After(100).Down(MouseButton.Right)
            .After(100).Up(MouseButton.Right)
            .After(100).Up(MouseButton.Left);

        Assert.Equal(
            ["press Middle", "release Middle", "press Ctrl + Middle", "release Ctrl + Middle", "press Middle", "release Middle"],
            script.Work);
        Assert.Equal(HoldRemapState.Idle, script.Machine.State);
        Assert.Equal(["suppress", "suppress", "suppress", "suppress", "suppress", "suppress"], script.Decisions);
    }

    [Fact]
    public void AfterTheLastButtonIsUpTheHoldIsOver_ButtonsPassAgain()
    {
        var script = new HoldScript().Hold().Down(MouseButton.Left).Release().Up(MouseButton.Left).Clear().Click(MouseButton.Left);

        Assert.Empty(script.Work);
        Assert.Equal(["pass", "pass"], script.Decisions);
    }

    [Fact]
    public void AButtonOfNoInputPassesEvenWhileFollowing_AndItsReleaseToo()
    {
        var script = new HoldScript().Hold().Down(MouseButton.Left).Release().Clear()
            .Down(MouseButton.X1).Up(MouseButton.X1);

        Assert.Empty(script.Work);
        Assert.Equal(["pass", "pass"], script.Decisions);
    }

    [Fact]
    public void ASetWithNoCommandHoldsNothing_ItsButtonsStillBelongToTheHold()
    {
        var script = new HoldScript()
            .Hold()
            .Down(MouseButton.Middle)
            .Down(MouseButton.Left)
            .Up(MouseButton.Middle)
            .Up(MouseButton.Left)
            .Release();

        Assert.Equal(["press Ctrl + Middle", "release Ctrl + Middle", "press Middle", "release Middle"], script.Work);
        Assert.All(script.Decisions, decision => Assert.Equal("suppress", decision));
    }

    [Fact]
    public void AButtonPressedBeforeTheHoldStaysTheApps_ItsReleaseIsNoPressAndTheTapStands()
    {
        var script = new HoldScript().Down(MouseButton.Left).Hold().After(50).Up(MouseButton.Left).After(50).Release();

        Assert.Equal(["tap Space"], script.Work);
        Assert.Equal(["pass", "suppress", "pass", "suppress"], script.Decisions);
    }

    [Fact]
    public void TheSameHoldKeyAgainWhileFollowingResumesTheHold_WithNoTap()
    {
        var script = new HoldScript()
            .Hold().Down(MouseButton.Left).After(300).Release()
            .After(50).Hold()
            .Down(MouseButton.Right)
            .After(20).Release()
            .Up(MouseButton.Right).Up(MouseButton.Left);

        Assert.Equal(["press Middle", "release Middle", "press Ctrl + Middle", "release Ctrl + Middle", "press Middle", "release Middle"], script.Work);
        Assert.Equal(HoldRemapState.Idle, script.Machine.State);
    }
}
