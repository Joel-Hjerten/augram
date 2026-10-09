using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Tests.HoldRemaps.Support;
using Xunit;

namespace Augram.Core.Tests.HoldRemaps;

/// <summary>
/// The hold key's tap (F9, Joel 2026-10-09/10): the press is swallowed and its repeats; it is sent at its release only if it
/// was released within the tap time (180 ms) with nothing used; "if a mouse button is pressed while space is held then no
/// space will be sent"; Ctrl, Alt, Shift and Win never count (decision 4).
/// </summary>
public sealed class HoldRemapTapTests
{
    [Fact]
    public void ReleasedAt179MsIsATap()
    {
        var script = new HoldScript().Hold().After(179).Release();

        Assert.Equal(["tap Space"], script.Work);
        Assert.Equal(["suppress", "suppress"], script.Decisions);
        Assert.Equal(HoldRemapState.Idle, script.Machine.State);
    }

    [Fact]
    public void ReleasedAt180MsIsStillATap_At181MsItIsNot()
    {
        Assert.Equal(["tap Space"], new HoldScript().Hold().After(180).Release().Work);

        var late = new HoldScript().Hold().After(181).Release();

        Assert.Empty(late.Work);
        Assert.Equal(["suppress", "suppress"], late.Decisions);
    }

    [Fact]
    public void ALongSpaceWithNothingInItSendsNothing_ItsRepeatsAreSwallowed()
    {
        var script = new HoldScript().Hold();
        for (var i = 0; i < 20; i++)
        {
            script.After(50).KeyRepeat(KeyCode.Space);
        }

        script.After(10).Release();

        Assert.Empty(script.Work);
        Assert.All(script.Decisions, decision => Assert.Equal("suppress", decision));
        Assert.Equal(22, script.Decisions.Count);
    }

    [Fact]
    public void AClickOfAnotherButtonInsideAShortSpacePassesAndCancelsTheTap()
    {
        var script = new HoldScript().Hold().After(30).Click(MouseButton.X1).After(30).Release();

        Assert.Empty(script.Work);
        Assert.Equal(["suppress", "pass", "pass", "suppress"], script.Decisions);
    }

    [Fact]
    public void AClickOfAnInputInsideAShortSpaceIsRemappedAndCancelsTheTap()
    {
        var script = new HoldScript().Hold().After(30).Click(MouseButton.Left).After(30).Release();

        Assert.Equal(["press Middle", "release Middle"], script.Work);
        Assert.Equal(["suppress", "suppress", "suppress", "suppress"], script.Decisions);
    }

    [Theory]
    [InlineData(WheelDirection.Up)]
    [InlineData(WheelDirection.Down)]
    public void AWheelTurnInsideAShortSpaceCancelsTheTap_InputOrNot(WheelDirection direction)
    {
        var script = new HoldScript().Hold().After(20).Wheel(direction).After(20).Release();

        Assert.DoesNotContain("tap Space", script.Work);
    }

    [Fact]
    public void AnInputKeyInsideAShortSpaceIsUsedAndCancelsTheTap()
    {
        var script = new HoldScript().Hold().After(20).Type(KeyCode.W).After(20).Release();

        Assert.Equal(["press G", "release G"], script.Work);
    }

    [Fact]
    public void ShiftHeldAcrossATapPassesAndTheTapIsSent()
    {
        var script = new HoldScript()
            .KeyDown(KeyCode.LeftShift)
            .After(40).Hold()
            .After(30).KeyRepeat(KeyCode.LeftShift)
            .After(30).Release()
            .After(20).KeyUp(KeyCode.LeftShift);

        Assert.Equal(["tap Space"], script.Work);
        Assert.Equal(["pass", "suppress", "pass", "suppress", "pass"], script.Decisions);
    }

    [Theory]
    [InlineData(KeyCode.LeftControl)]
    [InlineData(KeyCode.RightAlt)]
    [InlineData(KeyCode.LeftShift)]
    [InlineData(KeyCode.LeftMeta)]
    public void AModifierPressedDuringAShortSpaceNeitherRollsOverNorCancelsTheTap(KeyCode modifier)
    {
        var script = new HoldScript().Hold().After(20).KeyDown(modifier).After(20).Release().KeyUp(modifier);

        Assert.Equal(["tap Space"], script.Work);
        Assert.Equal(["suppress", "pass", "suppress", "pass"], script.Decisions);
    }

    [Fact]
    public void AnotherKeyPastTheTapTimePassesAndNothingIsSentAtRelease()
    {
        var script = new HoldScript().Hold().After(200).Type(KeyCode.B).After(10).Release();

        Assert.Empty(script.Work);
        Assert.Equal(["suppress", "pass", "pass", "suppress"], script.Decisions);
    }

    [Fact]
    public void AZeroTapTimeNeverSendsTheHoldKey()
    {
        var space = Blender.NewSpace() with { TapTimeMs = 0 };
        var entry = HoldRemapPlan.ForGroup(Blender.Document(Blender.Group(space)).Groups[1], HostPlatform.Windows).Entries.Single();

        var script = new HoldScript(entry).Hold().After(1).Release();

        Assert.Empty(script.Work);
    }
}
