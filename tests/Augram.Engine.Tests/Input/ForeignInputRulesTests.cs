using Augram.Core.Capture;
using Augram.Engine.Input;
using Xunit;

namespace Augram.Engine.Tests.Input;

/// <summary>
/// The hook's decision for a simulated mouse event, per <see cref="ForeignInput"/> policy: Augram's own (announced) are claimed and
/// dropped on both; the rest is dropped on Windows (a release as "released elsewhere", plan 0005 decision 10) and taken as real on
/// macOS (2026-10-19, the Wacom pen). Without a ledger everything simulated is dropped.
/// </summary>
public sealed class ForeignInputRulesTests
{
    [Theory]
    [InlineData(ForeignInput.Drop)]
    [InlineData(ForeignInput.Take)]
    public void AnAnnouncedPressReleaseOrMoveOfOurOwnIsClaimedAndDropped_Once(ForeignInput policy)
    {
        var own = new OwnInjections(() => 0);
        own.Expect(OwnInjections.PressSlotOf(MouseButton.Right));
        own.Expect(OwnInjections.ReleaseSlotOf(MouseButton.Right));
        own.Expect(InjectionSlot.Move);

        // Under Drop a press is dropped without a claim (today's rule, byte for byte): the announcement expires on its own.
        Assert.Equal(SimulatedButtonVerdict.Drop, ForeignInputRules.Button(policy, own, MouseButton.Right, pressed: true));
        Assert.Equal(policy == ForeignInput.Take ? 0 : 1, own.Pending(OwnInjections.PressSlotOf(MouseButton.Right)));

        Assert.Equal(SimulatedButtonVerdict.Drop, ForeignInputRules.Button(policy, own, MouseButton.Right, pressed: false));
        Assert.Equal(0, own.Pending(OwnInjections.ReleaseSlotOf(MouseButton.Right)));
        Assert.NotEqual(SimulatedButtonVerdict.Drop, ForeignInputRules.Button(policy, own, MouseButton.Right, pressed: false));

        // A move too: claimed under Take, left to expire under Drop.
        Assert.False(ForeignInputRules.TakesMove(policy, own));
        Assert.Equal(policy == ForeignInput.Take ? 0 : 1, own.Pending(InjectionSlot.Move));
    }

    [Fact]
    public void Drop_AnotherProgramsPressAndMoveAreDropped_ItsReleaseIsReleasedElsewhere()
    {
        var own = new OwnInjections(() => 0);

        Assert.Equal(SimulatedButtonVerdict.Drop, ForeignInputRules.Button(ForeignInput.Drop, own, MouseButton.Left, pressed: true));
        Assert.Equal(SimulatedButtonVerdict.ReleasedElsewhere, ForeignInputRules.Button(ForeignInput.Drop, own, MouseButton.Left, pressed: false));
        Assert.False(ForeignInputRules.TakesMove(ForeignInput.Drop, own));
    }

    [Fact]
    public void Take_AnotherProgramsPressReleaseAndMoveAreTakenAsReal()
    {
        var own = new OwnInjections(() => 0);

        Assert.Equal(SimulatedButtonVerdict.Take, ForeignInputRules.Button(ForeignInput.Take, own, MouseButton.Left, pressed: true));
        Assert.Equal(SimulatedButtonVerdict.Take, ForeignInputRules.Button(ForeignInput.Take, own, MouseButton.Left, pressed: false));
        Assert.True(ForeignInputRules.TakesMove(ForeignInput.Take, own));
    }

    [Fact]
    public void Take_AClaimIsForTheButtonAndKindAnnounced_NotAnother()
    {
        var own = new OwnInjections(() => 0);
        own.Expect(OwnInjections.PressSlotOf(MouseButton.Middle));

        Assert.Equal(SimulatedButtonVerdict.Take, ForeignInputRules.Button(ForeignInput.Take, own, MouseButton.Left, pressed: true));
        Assert.Equal(SimulatedButtonVerdict.Take, ForeignInputRules.Button(ForeignInput.Take, own, MouseButton.Middle, pressed: false));
        Assert.Equal(SimulatedButtonVerdict.Drop, ForeignInputRules.Button(ForeignInput.Take, own, MouseButton.Middle, pressed: true));
    }

    [Theory]
    [InlineData(ForeignInput.Drop)]
    [InlineData(ForeignInput.Take)]
    public void WithoutALedgerEverythingSimulatedIsDropped(ForeignInput policy)
    {
        Assert.Equal(SimulatedButtonVerdict.Drop, ForeignInputRules.Button(policy, null, MouseButton.Left, pressed: true));
        Assert.Equal(SimulatedButtonVerdict.Drop, ForeignInputRules.Button(policy, null, MouseButton.Left, pressed: false));
        Assert.False(ForeignInputRules.TakesMove(policy, null));
    }
}
