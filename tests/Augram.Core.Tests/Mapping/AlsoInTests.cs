using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Xunit;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.Mapping;

/// <summary>
/// A command's "Also in" (Joel, 2026-10-10, plan 0005 decision 7): it names Exclusions › Global entries the command still works
/// over. There only such commands apply: their anchors are held back and they fire, while the stroke button stays the app's and
/// every other command stays off. Only a trigger without the stroke button keeps it, never one under a hold remap, and it names
/// only plain Global entries.
/// </summary>
public sealed class AlsoInTests
{
    private const MouseButton Stroke = MouseButton.Middle;
    private static readonly Trigger RightLeft = Trigger.ForButton(MouseButton.Left, new TriggerHold(HeldButtons.Right));
    private static readonly Trigger RightWheelUp = Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right));

    private static IgnoredApp Excluded(string name, bool disableEntirely = false)
        => new(GroupId.New(), name, IsActive: true, ByProcess(name.ToLowerInvariant() + ".exe"), disableEntirely);

    private static MappingDocument Document(AppGroup[] groups, params IgnoredApp[] ignored)
        => MappingRules.ValidDocument(new MappingDocument(groups, ignored));

    private static PressedTrigger Chord(HeldButtons held) => new(new Trigger.ButtonTrigger(MouseButton.Left), new PressHold(held, Stroke));

    [Fact]
    public void OverAnExcludedApp_OnlyACommandAlsoInItFires()
    {
        var blender = Excluded("Blender");
        var magnifier = NewCommand("Magnifier", RightLeft, NewStep("loupe")) with { AlsoIn = [blender.Id] };
        var zoom = NewCommand("Zoom In", RightWheelUp, NewStep("zoom"));
        var close = NewCommand("Close", Up, NewStep("close"));
        var mapping = Document([NewGlobal(magnifier, zoom, close)], blender);
        var over = Window("blender.exe");

        var chord = CommandResolver.Resolve(mapping, over, Chord(HeldButtons.Right), HostPlatform.Windows);
        Assert.Equal("Magnifier", chord.Command?.Name);
        Assert.Equal("global, also in 'Blender'", chord.Reason);

        Assert.Equal(ResolutionOutcome.Ignored, CommandResolver.Resolve(mapping, over, RightWheelUp, HostPlatform.Windows).Outcome);
        Assert.Equal(ResolutionOutcome.Ignored, CommandResolver.Resolve(mapping, over, Trigger.ForGesture(Up), HostPlatform.Windows).Outcome);
        Assert.Equal("Zoom In", CommandResolver.Resolve(mapping, Window("chrome.exe"), RightWheelUp, HostPlatform.Windows).Command?.Name);
    }

    [Fact]
    public void OverAnExcludedApp_ThePlanHoldsBackOnlyTheAlsoInCommandsAnchors()
    {
        var blender = Excluded("Blender");
        var game = Excluded("Plague");
        var magnifier = NewCommand("Magnifier", RightLeft, NewStep("loupe")) with { AlsoIn = [blender.Id] };
        var back = NewCommand("Back", Trigger.ForButton(MouseButton.Right, new TriggerHold(HeldButtons.X1)), NewStep("back"));
        var stroke = NewCommand("Stroke + Left click", Trigger.ForClick(TriggerHold.WithStroke(KeyModifiers.None, HeldButtons.Left)), NewStep("click"));
        var mapping = Document([NewGlobal(magnifier, back, stroke)], blender, game);

        var overBlender = AnchorPlanner.Answer(mapping, null, HostPlatform.Windows, Stroke, excludedBy: blender.Id).Plan;
        Assert.True(overBlender.IsAnchor(MouseButton.Right));
        Assert.True(overBlender.Fires(MouseButton.Right, MouseButton.Left));
        Assert.False(overBlender.IsAnchor(MouseButton.X1));
        Assert.Equal(HeldButtons.None, overBlender.ExtrasFor(Stroke, ownerIsStroke: true));

        Assert.True(AnchorPlanner.Answer(mapping, null, HostPlatform.Windows, Stroke, excludedBy: game.Id).Plan.IsEmpty);
        Assert.True(AnchorPlanner.Answer(mapping, null, HostPlatform.Windows, Stroke).Plan.IsAnchor(MouseButton.X1));
    }

    [Fact]
    public void AlsoIn_KeepsOnlyPlainGlobalEntries_OnceEach_Sorted()
    {
        var blender = Excluded("Blender");
        var resolve = Excluded("Resolve");
        var vmware = Excluded("VMware", disableEntirely: true);
        var spine = new IgnoredApp(GroupId.New(), "Spine", IsActive: true, ByProcess("spine.exe"), DisableEntirely: false) { Scope = IgnoreScope.PerCommand };
        var magnifier = NewCommand("Magnifier", RightLeft, NewStep("loupe")) with { AlsoIn = [resolve.Id, vmware.Id, spine.Id, blender.Id, blender.Id, GroupId.New()] };

        var mapping = Document([NewGlobal(magnifier)], blender, resolve, vmware, spine);

        var kept = mapping.Global.Commands.Single().AlsoIn;
        Assert.Equal(new[] { blender.Id, resolve.Id }.OrderBy(id => id.Value), kept);
    }

    [Fact]
    public void AlsoIn_IsDroppedOnATriggerThatHoldsTheStrokeButton()
    {
        var blender = Excluded("Blender");
        var close = NewCommand("Close", Up, NewStep("close")) with { AlsoIn = [blender.Id] };
        var volume = NewCommand("Volume", Trigger.ForWheel(WheelDirection.Up), NewStep("volume")) with { AlsoIn = [blender.Id] };
        var zoom = NewCommand("Zoom In", RightWheelUp, NewStep("zoom")) with { AlsoIn = [blender.Id] };

        var mapping = Document([NewGlobal(close, volume, zoom)], blender);

        Assert.Empty(mapping.Global.Commands.Single(command => command.Name == "Close").AlsoIn);
        Assert.Empty(mapping.Global.Commands.Single(command => command.Name == "Volume").AlsoIn);
        Assert.Equal([blender.Id], mapping.Global.Commands.Single(command => command.Name == "Zoom In").AlsoIn);
    }

    [Fact]
    public void AllowedFor_SetFromTheEntrysSide_IsOneUndoStep_AndKeepsOnlyCommandsThatCanWorkThere()
    {
        var blender = Excluded("Blender");
        var resolve = Excluded("Resolve");
        var magnifier = NewCommand("Magnifier", RightLeft, NewStep("loupe")) with { AlsoIn = [resolve.Id] };
        var zoom = NewCommand("Zoom In", RightWheelUp, NewStep("zoom")) with { AlsoIn = [blender.Id] };
        var close = NewCommand("Close", Up, NewStep("close"));
        var store = new MappingStore(new MappingDocument([NewGlobal(magnifier, zoom, close)], [blender, resolve]));

        Assert.False(MappingRules.CanWorkOverExcluded(close));
        Assert.True(MappingRules.CanWorkOverExcluded(magnifier));

        var changed = store.SetAllowedFor(blender.Id, [magnifier.Id, close.Id]);

        Assert.Equal(2, changed);
        Assert.Equal(new[] { blender.Id, resolve.Id }.OrderBy(id => id.Value), store.FindCommand(magnifier.Id)!.Value.Command.AlsoIn);
        Assert.Empty(store.FindCommand(zoom.Id)!.Value.Command.AlsoIn);
        Assert.Empty(store.FindCommand(close.Id)!.Value.Command.AlsoIn);

        Assert.Equal(0, store.SetAllowedFor(blender.Id, [magnifier.Id]));
        Assert.True(store.Undo());
        Assert.Equal([blender.Id], store.FindCommand(zoom.Id)!.Value.Command.AlsoIn);
        Assert.Equal([resolve.Id], store.FindCommand(magnifier.Id)!.Value.Command.AlsoIn);
    }

    [Fact]
    public void ADisableWhileFocusedEntry_StopsEvenACommandThatWasAlsoInIt()
    {
        var vmware = Excluded("VMware", disableEntirely: true);
        var magnifier = NewCommand("Magnifier", RightLeft, NewStep("loupe")) with { AlsoIn = [vmware.Id] };
        var mapping = new MappingDocument([NewGlobal(magnifier)], [vmware]);

        Assert.Equal(ResolutionOutcome.Ignored, CommandResolver.Resolve(mapping, Window("vmware.exe"), Chord(HeldButtons.Right), HostPlatform.Windows).Outcome);
    }
}
