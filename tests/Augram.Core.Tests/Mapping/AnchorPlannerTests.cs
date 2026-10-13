using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

/// <summary>
/// Anchors are decided per app (Joel, 2026-10-09): a button is held back over a window only when an active command that
/// applies there (its app group's, plus Global's unless suppressed, used on this platform) holds it without the stroke button.
/// </summary>
public sealed class AnchorPlannerTests
{
    private const MouseButton Stroke = MouseButton.Middle;
    private static readonly Trigger RightWheelUp = Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right));

    [Fact]
    public void AnAppGroupsAnchor_HoldsTheButtonBackOverThatAppOnly()
    {
        var chrome = NewGroup("Chrome", null, NewCommand("Zoom in", RightWheelUp, NewStep("zoom")));
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(), chrome));

        Assert.True(AnchorPlanner.For(mapping, Window("chrome.exe"), HostPlatform.Windows, Stroke).IsAnchor(MouseButton.Right));
        Assert.False(AnchorPlanner.For(mapping, Window("explorer.exe"), HostPlatform.Windows, Stroke).IsAnchor(MouseButton.Right));
        Assert.False(AnchorPlanner.For(mapping, null, HostPlatform.Windows, Stroke).IsAnchor(MouseButton.Right));
        Assert.True(AnchorPlanner.NeedsPlan(mapping, HostPlatform.Windows));
    }

    [Fact]
    public void AGlobalAnchor_IsEverywhere_ExceptWhereGlobalsAreSuppressedOrAnAppOverridesItToNothing()
    {
        var global = NewGlobal(NewCommand("Zoom in", RightWheelUp, NewStep("zoom")));
        var game = NewGroup("Game") with { SuppressGlobals = true };
        var quiet = NewGroup("Quiet", null, NewCommand("No zoom", RightWheelUp));
        var mapping = MappingRules.ValidDocument(Document(global, game, quiet));

        Assert.True(AnchorPlanner.For(mapping, Window("notepad.exe"), HostPlatform.Windows, Stroke).IsAnchor(MouseButton.Right));
        Assert.False(AnchorPlanner.For(mapping, Window("game.exe"), HostPlatform.Windows, Stroke).IsAnchor(MouseButton.Right));
        Assert.False(AnchorPlanner.For(mapping, Window("quiet.exe"), HostPlatform.Windows, Stroke).IsAnchor(MouseButton.Right));
    }

    [Fact]
    public void InactiveCommandsAndOtherPlatforms_HoldNothingBack()
    {
        var off = NewCommand("Off", RightWheelUp, NewStep("zoom")) with { IsActive = false };
        var macOnly = NewCommand("Mac zoom", Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.X1)), NewStep("zoom")) with { UseOn = PlatformSet.MacOS };
        var mapping = MappingRules.ValidDocument(Document(NewGlobal(off, macOnly)));

        Assert.True(AnchorPlanner.For(mapping, null, HostPlatform.Windows, Stroke).IsEmpty);
        Assert.False(AnchorPlanner.NeedsPlan(mapping, HostPlatform.Windows));
        Assert.True(AnchorPlanner.For(mapping, null, HostPlatform.MacOS, Stroke).IsAnchor(MouseButton.X1));
    }

    [Fact]
    public void ButtonsHeldWithAnAnchor_JoinItsPress_AndEachButtonOfASetIsAnAnchor()
    {
        var strokeAndLeft = NewCommand("Back", Trigger.ForClick(new TriggerHold(HeldButtons.Stroke | HeldButtons.Left)), NewStep("back"));
        var rightAndX1 = NewCommand("Volume", Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right | HeldButtons.X1)), NewStep("volume"));
        var plan = AnchorPlanner.For(MappingRules.ValidDocument(Document(NewGlobal(strokeAndLeft, rightAndX1))), null, HostPlatform.Windows, Stroke);

        Assert.True(plan.Claims(Stroke, ownerIsStroke: true, MouseButton.Left));
        Assert.False(plan.Claims(Stroke, ownerIsStroke: true, MouseButton.Right));
        Assert.True(plan.IsAnchor(MouseButton.Right) && plan.IsAnchor(MouseButton.X1));
        Assert.True(plan.Claims(MouseButton.Right, ownerIsStroke: false, MouseButton.X1));
        Assert.True(plan.Claims(MouseButton.X1, ownerIsStroke: false, MouseButton.Right));
        Assert.False(plan.IsAnchor(MouseButton.Left));
    }

    /// <summary>Joel, 2026-10-13: a bare click binds the stroke button's plain click only over windows where its command applies; an override to nothing gives the click back.</summary>
    [Fact]
    public void ABareClick_BindsThePlainClick_WhereItApplies_AndAnOverrideToNothingUnbindsIt()
    {
        var bare = Trigger.ForClick(TriggerHold.Default);
        var chrome = NewGroup("Chrome", null, NewCommand("Mission Control", bare, NewStep("open")));
        var inApp = MappingRules.ValidDocument(Document(NewGlobal(), chrome));

        Assert.True(AnchorPlanner.For(inApp, Window("chrome.exe"), HostPlatform.Windows, Stroke).ClickBound);
        Assert.False(AnchorPlanner.For(inApp, Window("explorer.exe"), HostPlatform.Windows, Stroke).ClickBound);
        Assert.True(AnchorPlanner.NeedsPlan(inApp, HostPlatform.Windows), "the hook needs a plan per window although no other button is held");

        var game = NewGroup("Game") with { SuppressGlobals = true };
        var quiet = NewGroup("Quiet", null, NewCommand("No Mission Control", bare));
        var everywhere = MappingRules.ValidDocument(Document(NewGlobal(NewCommand("Mission Control", bare, NewStep("open"))), game, quiet));

        Assert.True(AnchorPlanner.For(everywhere, Window("notepad.exe"), HostPlatform.Windows, Stroke).ClickBound);
        Assert.True(AnchorPlanner.For(everywhere, null, HostPlatform.Windows, Stroke).ClickBound);
        Assert.False(AnchorPlanner.For(everywhere, Window("game.exe"), HostPlatform.Windows, Stroke).ClickBound);
        Assert.False(AnchorPlanner.For(everywhere, Window("quiet.exe"), HostPlatform.Windows, Stroke).ClickBound);
        Assert.False(AnchorPlanner.For(everywhere, null, HostPlatform.Windows, Stroke).IsAnchor(MouseButton.Right), "a bare click holds no other button back");
    }

    [Fact]
    public void ATriggerNamingThisMachinesStrokeButton_IsNoAnchorHere()
    {
        var middleWheel = NewCommand("Zoom", Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Middle)), NewStep("zoom"));

        Assert.True(AnchorPlanner.For(MappingRules.ValidDocument(Document(NewGlobal(middleWheel))), null, HostPlatform.Windows, MouseButton.Middle).IsEmpty);
        Assert.True(AnchorPlanner.For(MappingRules.ValidDocument(Document(NewGlobal(middleWheel))), null, HostPlatform.Windows, MouseButton.Right).IsAnchor(MouseButton.Middle));
    }
}
