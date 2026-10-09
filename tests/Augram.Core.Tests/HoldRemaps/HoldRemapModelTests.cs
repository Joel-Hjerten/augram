using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps.Remap;
using Xunit;
using static Augram.Core.Tests.HoldRemaps.Support.Blender;
using static Augram.Core.Tests.Mapping.Support.MappingFixtures;

namespace Augram.Core.Tests.HoldRemaps;

/// <summary>
/// The hold remap model without JSON (plan 0002 step 1): the input trigger, the resolver and anchor planner leaving commands
/// under a hold remap alone, "Use on" through the hold remap, the store's mutators and a move between groups.
/// </summary>
public sealed class HoldRemapModelTests
{
    [Fact]
    public void AnInputDescribesItself()
    {
        Assert.Equal("Left + Right", Trigger.ForInput(HoldInput.Of(MouseButton.Right, MouseButton.Left)).Describe());
        Assert.Equal("Middle", Trigger.ForInput(HoldInput.Of(MouseButton.Middle)).Describe());
        Assert.Equal("wheel up", Trigger.ForInput(new HoldInput.Wheel(WheelDirection.Up)).Describe());
        Assert.Equal("W", Trigger.ForInput(new HoldInput.Key(KeyCode.W)).Describe());
        Assert.Equal("Num .", Trigger.ForInput(new HoldInput.Key(KeyCode.NumPadDecimal)).Describe());
    }

    [Fact]
    public void AnInputHasValueEquality_HoldsNothing_AndKeepsItsInputThroughNormalisingAndConversion()
    {
        var input = Trigger.ForInput(HoldInput.Of(MouseButton.Left, MouseButton.Right));

        Assert.Equal(input, Trigger.ForInput(new HoldInput.Buttons(HeldButtons.Left | HeldButtons.Right | HeldButtons.Stroke)));
        Assert.Equal(TriggerHold.Default, input.Hold);
        Assert.Equal(input, input.WithHold(TriggerHold.WithStroke(KeyModifiers.Shift)));
        Assert.Equal(input, input.Normalised());
        Assert.Equal(input, TriggerConversion.Convert(input, HostPlatform.Windows, HostPlatform.MacOS).Trigger);
        Assert.True(input.Overlaps(Trigger.ForInput(HoldInput.Of(MouseButton.Right, MouseButton.Left))));
        Assert.False(input.Overlaps(Trigger.ForInput(HoldInput.Of(MouseButton.Left))));
        Assert.False(input.Overlaps(Trigger.ForWheel(WheelDirection.Up)));
    }

    [Fact]
    public void AnInputNeverMatchesAPress()
    {
        var input = Trigger.ForInput(new HoldInput.Wheel(WheelDirection.Up));

        Assert.False(PressedTrigger.Of(input).Matches(input));
        Assert.False(PressedTrigger.Of(Trigger.ForWheel(WheelDirection.Up)).Matches(input));
    }

    /// <summary>
    /// Belt and braces: the rules never let a command under a hold remap keep a gesture or a wheel trigger, but should one get
    /// that far (a raw document), the resolver and the anchor planner still pass it by.
    /// </summary>
    [Fact]
    public void TheResolverAndTheAnchorPlannerLeaveCommandsUnderAHoldRemapAlone()
    {
        var space = NewSpace();
        var hidden = NewCommand("Hidden", Up, NewStep("hidden")) with { HoldRemapId = space.Id };
        var wheel = NewCommand("Wheel", Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Left)), NewStep("wheel")) with { HoldRemapId = space.Id };
        var raw = Document(NewGlobal(NewCommand("Close", Up, NewStep("close"))), NewGroup("Blender", ByProcess("blender.exe"), hidden, wheel) with { HoldRemaps = [space] });

        var resolved = CommandResolver.Resolve(raw, Window("blender.exe"), Trigger.ForGesture(Up), HostPlatform.Windows);

        Assert.Equal(("Close", "global"), (resolved.Command?.Name, resolved.Reason));
        Assert.False(AnchorPlanner.UsesButtons(raw, HostPlatform.Windows));
        Assert.True(AnchorPlanner.ForGroup(raw, raw.Groups[1], HostPlatform.Windows, MouseButton.Right).IsEmpty);
    }

    [Fact]
    public void UseOnIsGroupAndHoldRemapAndCommand()
    {
        var space = NewSpace() with { UseOn = PlatformSet.Windows };
        var group = Group(space);
        var orbit = group.Commands.Single(command => command.Name == "Orbit");

        Assert.Equal(PlatformSet.Windows, group.UseOnLimitFor(orbit));
        Assert.True(group.IsCommandUsedOn(orbit, HostPlatform.Windows));
        Assert.False(group.IsCommandUsedOn(orbit, HostPlatform.MacOS));
        Assert.Same(space, group.HoldRemapOf(orbit));
    }

    [Fact]
    public void TheStoreAddsUpdatesAndRemovesAHoldRemap_OneUndoStepEach()
    {
        var store = new MappingStore(Document(NewGlobal(), NewGroup("Blender")));
        var blender = store.Current.Groups[1].Id;
        var version = store.Version;

        var added = store.AddHoldRemap(blender, HoldRemap.For(KeyCode.Space) with { Name = " " });
        var orbit = store.AddCommand(blender, Remap(added, "Orbit", HoldInput.Of(MouseButton.Left), Middle));
        var updated = store.UpdateHoldRemap(blender, added with { TapTimeMs = 150, Name = "Navigate" });

        Assert.Equal("Space", added.Name);
        Assert.Equal(added.Id, orbit.HoldRemapId);
        Assert.Equal((150, "Navigate"), (updated.TapTimeMs, updated.Name));
        Assert.Equal(version + 3, store.Version);

        var removed = store.RemoveHoldRemap(blender, added.Id);

        Assert.Equal(updated, removed);
        Assert.Empty(store.FindGroup(blender)!.HoldRemaps);
        Assert.Empty(store.FindGroup(blender)!.Commands);
        Assert.True(store.Undo());
        Assert.Equal(orbit.Id, store.FindGroup(blender)!.Commands.Single().Id);
        Assert.Equal("Navigate", store.FindGroup(blender)!.HoldRemaps.Single().Name);
    }

    [Fact]
    public void TheStoreRefusesABrokenHoldRemapAndStaysUnchanged()
    {
        var store = new MappingStore(Document(NewGlobal(), NewGroup("Blender")));
        var before = store.Current;

        Assert.Throws<MappingValidationException>(() => store.AddHoldRemap(store.Current.Groups[1].Id, HoldRemap.For(KeyCode.LeftShift)));
        Assert.Throws<KeyNotFoundException>(() => store.UpdateHoldRemap(store.Current.Groups[1].Id, HoldRemap.For(KeyCode.Space)));
        Assert.Throws<KeyNotFoundException>(() => store.RemoveHoldRemap(store.Current.Groups[1].Id, HoldRemapId.New()));
        Assert.Same(before, store.Current);
    }

    [Fact]
    public void AMoveKeepsTheHoldRemapByItsHoldKey_ElseTheCommandBecomesOrdinaryWithoutItsInput()
    {
        var space = NewSpace();
        var theirSpace = HoldRemap.For(KeyCode.Space) with { Name = "Navigate" };
        var store = new MappingStore(Document(NewGlobal(), Group(space), NewGroup("Maya") with { HoldRemaps = [theirSpace] }, NewGroup("Paint")));
        var orbit = store.Current.AllCommands().Single(pair => pair.Command.Name == "Orbit").Command;
        var grab = store.Current.AllCommands().Single(pair => pair.Command.Name == "Grab").Command;

        var toMaya = store.MoveCommand(orbit.Id, store.Current.Groups.Single(group => group.Name == "Maya").Id);
        var toPaint = store.MoveCommand(grab.Id, store.Current.Groups.Single(group => group.Name == "Paint").Id);

        Assert.Equal(theirSpace.Id, toMaya.HoldRemapId);
        Assert.Equal(orbit.Trigger, toMaya.Trigger);
        Assert.Null(toPaint.HoldRemapId);
        Assert.Same(Trigger.None, toPaint.Trigger);
        Assert.IsType<RemapStep>(toPaint.Steps.Single().Step);
    }

    [Fact]
    public void KnownAppsGuessBlenderBothWays()
    {
        Assert.Equal(["Blender"], KnownApps.Guess(["Blender.exe"], HostPlatform.MacOS));
        Assert.Equal(["blender.exe"], KnownApps.Guess(["Blender"], HostPlatform.Windows));
    }
}
