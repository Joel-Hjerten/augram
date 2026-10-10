using Augram.App.Components.CommandTree;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

/// <summary>
/// A command's own drag distance in the header (Joel, 2026-10-10, plan 0004 step 3): shown only for a trigger whose held
/// buttons are handed back as drags (Right + wheel, not the stroke button), the Options value or its own 1–200 px, edited
/// through the trigger's one edit path, so each change is one undo step and a platform's own trigger can have its own.
/// </summary>
public sealed class CommandsViewModelDragDistanceTests
{
    private static readonly TriggerHold Right = new(HeldButtons.Right);

    [AvaloniaFact]
    public void TheDragDistanceShowsOnlyForAHoldThatHandsBackDrags()
    {
        var (vm, _) = GlobalWithRightWheelVolume();
        Select(vm, "Close window");
        Assert.False(vm.SelectedCommand!.ShowsDragDistance);

        Select(vm, "Three steps");
        Assert.False(vm.SelectedCommand!.ShowsDragDistance);

        // A wheel trigger held with the stroke button.
        Select(vm, "Minimize");
        Handle(vm, CommandTreeAction.SetTriggerKind, kind: TriggerKind.Wheel);
        Assert.Equal(Trigger.ForWheel(WheelDirection.Up), vm.SelectedCommand!.Trigger);
        Assert.False(vm.SelectedCommand.ShowsDragDistance);

        Select(vm, "Volume up");
        Assert.True(vm.SelectedCommand!.ShowsDragDistance);
        Assert.Equal(CaptureThresholds.Default.ButtonDragDistancePx, vm.SelectedCommand.OptionsDragDistancePx);

        // Right and the stroke button: the press is the stroke button's, which a drag distance does not apply to.
        Handle(vm, CommandTreeAction.SetTriggerHold, hold: new TriggerHold(HeldButtons.Stroke | HeldButtons.Right));
        Assert.False(vm.SelectedCommand!.ShowsDragDistance);

        // A command under a hold remap has an input, never a drag distance.
        var (blender, _, _) = HoldRemapTestData.CreateBlender();
        var pan = blender.Sections.SelectMany(section => section.Commands).Single(command => command.Name == "Pan");
        Assert.False(pan.ShowsDragDistance);
    }

    [AvaloniaFact]
    public void SettingAndClearingTheOwnDistanceIsOneUndoStepEach()
    {
        var (vm, store) = GlobalWithRightWheelVolume();
        Select(vm, "Volume up");

        Handle(vm, CommandTreeAction.SetTriggerHold, hold: Right with { DragDistancePx = 3 });

        Assert.Equal(3, Find(store, "Volume up").Trigger.Hold.DragDistancePx);
        Assert.Equal(3, vm.SelectedCommand!.Trigger.Hold.DragDistancePx);
        Assert.Equal("Right + wheel up", Item(vm, "Volume up").TriggerText);

        Handle(vm, CommandTreeAction.SetTriggerHold, hold: Right);

        Assert.Null(Find(store, "Volume up").Trigger.Hold.DragDistancePx);
        Handle(vm, CommandTreeAction.Undo);
        Assert.Equal(3, Find(store, "Volume up").Trigger.Hold.DragDistancePx);
        Handle(vm, CommandTreeAction.Undo);
        Assert.Null(Find(store, "Volume up").Trigger.Hold.DragDistancePx);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public void ADistanceOutsideOneTo200IsRefusedWithTheRule_AndNothingChanges()
    {
        var (vm, store) = GlobalWithRightWheelVolume();
        Select(vm, "Volume up");
        var before = store.Current;

        Handle(vm, CommandTreeAction.SetTriggerHold, hold: Right with { DragDistancePx = 201 });

        Assert.Same(before, store.Current);
        Assert.Contains("The drag distance of 'Volume up' must be between 1 and 200 px.", vm.SelectedCommand!.DraftNote, StringComparison.Ordinal);
    }

    /// <summary>On the Mac, a Windows-authored command's distance is the Mac's own trigger, as any trigger edit there makes it.</summary>
    [AvaloniaFact]
    public void OnTheOtherPlatformTheDistanceMakesThatPlatformsOwnTrigger()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global, platform: HostPlatform.MacOS);
        Select(vm, "Volume up");
        Handle(vm, CommandTreeAction.SetTriggerHold, hold: Right);
        Handle(vm, CommandTreeAction.SetTriggerHold, hold: Right with { DragDistancePx = 5 });

        var volume = Find(store, "Volume up");
        Assert.Equal(5, volume.TriggerFor(HostPlatform.MacOS).Hold.DragDistancePx);
        Assert.Equal(Trigger.ForWheel(WheelDirection.Up), volume.Trigger);
    }

    [AvaloniaFact]
    public void TheHeaderOffersTheOptionsValueOrItsOwn_AndAsksForEachThroughTheTriggerHold()
    {
        var (vm, store) = GlobalWithRightWheelVolume();
        vm.OptionsDragDistancePx = 12;
        Select(vm, "Volume up");
        var header = Bound(vm);

        Assert.True(header.ShowsDragDistance);
        Assert.False(header.HasOwnDragDistance);
        Assert.Equal(["Options value (12 px)", CommandHeader.OwnDragDistanceLabel], Combo(header, "PART_DragDistanceMode").ItemsSource!.Cast<string>());
        Assert.Equal(0, header.DragDistanceModeIndex);

        // Own starts at the Options value, so the press behaves the same until the number changes.
        Combo(header, "PART_DragDistanceMode").SelectedIndex = 1;
        Assert.Equal(12, Find(store, "Volume up").Trigger.Hold.DragDistancePx);
        Assert.True(header.HasOwnDragDistance);
        Assert.Equal(1, header.DragDistanceModeIndex);
        var number = header.GetVisualDescendants().OfType<NumericUpDown>().Single(box => box.Name == "PART_DragDistance");
        Assert.Equal(12, number.Value);

        number.Value = 3;
        Assert.Equal(3, Find(store, "Volume up").Trigger.Hold.DragDistancePx);

        Combo(header, "PART_DragDistanceMode").SelectedIndex = 0;
        Assert.Null(Find(store, "Volume up").Trigger.Hold.DragDistancePx);
        Assert.False(header.HasOwnDragDistance);

        // A gesture shows no row.
        Select(vm, "Close window");
        Assert.False(header.ShowsDragDistance);
        Assert.Equal(-1, header.DragDistanceModeIndex);
    }

    [AvaloniaFact]
    public void TheHeaderAloneOnlyAsks_AndStaysOnTheItemsValue()
    {
        var (vm, _) = GlobalWithRightWheelVolume();
        var actions = new List<CommandTreeActionEventArgs>();
        var header = new CommandHeader { Item = Item(vm, "Volume up") };
        header.ActionRequested += (_, e) => actions.Add(e);
        new Window { Content = header }.Show();

        Combo(header, "PART_DragDistanceMode").SelectedIndex = 1;
        header.ChooseDragDistance(500);

        Assert.Equal([Right with { DragDistancePx = 10 }, Right with { DragDistancePx = 200 }], actions.Select(action => action.Hold));
        Assert.All(actions, action => Assert.Equal(CommandTreeAction.SetTriggerHold, action.Action));
        Assert.Equal(0, header.DragDistanceModeIndex);
        Assert.False(header.HasOwnDragDistance);
    }

    /// <summary>The test mapping's Global group with Volume up on Right + wheel up, and no undo history.</summary>
    private static (CommandsViewModel Vm, MappingStore Store) GlobalWithRightWheelVolume()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);
        var volume = Find(store, "Volume up");
        store.UpdateCommand(GroupId.Global, volume with { Trigger = Trigger.ForWheel(WheelDirection.Up, Right) });
        store.ClearHistory();
        return (vm, store);
    }

    private static CommandHeader Bound(CommandsViewModel vm)
    {
        var header = new CommandHeader();
        header.Bind(CommandHeader.ItemProperty, new Binding(nameof(CommandsViewModel.SelectedCommand)) { Source = vm });
        header.ActionRequested += (_, e) => vm.Handle(e);
        new Window { Content = header }.Show();
        return header;
    }

    private static void Select(CommandsViewModel vm, string name)
    {
        var item = Item(vm, name);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, vm.Sections.Single(section => section.Id == item.Section), item));
    }

    /// <summary>What the header raises for the selected command.</summary>
    private static void Handle(CommandsViewModel vm, CommandTreeAction action, TriggerKind? kind = null, TriggerHold? hold = null)
        => vm.Handle(new CommandTreeActionEventArgs(action, command: vm.SelectedCommand, kind: kind, hold: hold));

    private static ComboBox Combo(CommandHeader header, string name) => header.GetVisualDescendants().OfType<ComboBox>().Single(combo => combo.Name == name);
}
