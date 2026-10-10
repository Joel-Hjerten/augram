using Augram.App.Components.CommandTree;
using Augram.App.Components.GesturePicker;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Augram.Core.Steps.Delay;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

/// <summary>
/// Swap and Take it on a trigger draft (Joel, 2026-10-10: Global › Media › Zoom In on Right + wheel down and Zoom Out on Right
/// + wheel up could not be inverted, since each direction was the other's). A draft refused only because another command of
/// the group uses it here offers both in its note, each when the rules accept it; each is one undo step for both commands.
/// </summary>
public sealed class CommandsViewModelTriggerConflictTests
{
    private static readonly Trigger RightWheelUp = Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right));
    private static readonly Trigger RightWheelDown = Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.Right));

    [AvaloniaFact]
    public void JoelsZoomCase_TheDraftOffersBoth_SwapInvertsThemInOneUndoStep()
    {
        var (vm, store) = GlobalWithZoom();
        Select(vm, "Zoom In");

        Handle(vm, CommandTreeAction.SetWheelDirection, wheel: WheelDirection.Up);

        var header = vm.SelectedCommand!;
        Assert.Equal("Not saved yet: 'Zoom Out' already uses Right + wheel up here. Change the direction, a button or a key.", header.DraftNote);
        Assert.Equal(("Zoom Out", true, true), (header.ConflictName, header.CanSwapTrigger, header.CanTakeTrigger));
        Assert.Null(Item(vm, "Zoom In").ConflictName);

        Handle(vm, CommandTreeAction.SwapTrigger);

        Assert.Equal((RightWheelUp, RightWheelDown), (Find(store, "Zoom In").Trigger, Find(store, "Zoom Out").Trigger));
        Assert.Equal("Zoom In", vm.SelectedCommand!.Name);
        Assert.Equal((null, null, false, false), (vm.SelectedCommand.DraftNote, vm.SelectedCommand.ConflictName, vm.SelectedCommand.CanSwapTrigger, vm.SelectedCommand.CanTakeTrigger));
        Assert.Equal($"Swapped with 'Zoom Out', which now uses Right + wheel down. {CommandsKeymap.Current.Undo} undoes both.", vm.Message);

        Handle(vm, CommandTreeAction.Undo);

        Assert.Equal((RightWheelDown, RightWheelUp), (Find(store, "Zoom In").Trigger, Find(store, "Zoom Out").Trigger));
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public void TakeItLeavesTheOtherWithNoTriggerAndOpensIt_InOneUndoStep()
    {
        var (vm, store) = GlobalWithZoom();
        Select(vm, "Zoom In");
        Handle(vm, CommandTreeAction.SetWheelDirection, wheel: WheelDirection.Up);

        Handle(vm, CommandTreeAction.TakeTrigger);

        Assert.Equal(RightWheelUp, Find(store, "Zoom In").Trigger);
        Assert.Same(Trigger.None, Find(store, "Zoom Out").Trigger);
        Assert.Equal("Zoom Out", vm.SelectedCommand!.Name);
        Assert.Null(vm.SelectedCommand.DraftNote);
        Assert.True(Section(vm, "Media").IsExpanded);
        Assert.Equal($"'Zoom Out' has no trigger now: give it one. {CommandsKeymap.Current.Undo} undoes both.", vm.Message);

        Handle(vm, CommandTreeAction.Undo);

        Assert.Equal((RightWheelDown, RightWheelUp), (Find(store, "Zoom In").Trigger, Find(store, "Zoom Out").Trigger));
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public void TakeItFromACommandThisPlatformLeavesOutListsTheOtherPlatformsSoItIsSelected()
    {
        var (vm, store) = GlobalWithZoom();
        store.UpdateCommand(GroupId.Global, Find(store, "Zoom Out") with { UseOn = PlatformSet.MacOS });
        Assert.DoesNotContain(vm.Sections.SelectMany(section => section.Commands), command => command.Name == "Zoom Out");
        Select(vm, "Zoom In");
        Handle(vm, CommandTreeAction.SetWheelDirection, wheel: WheelDirection.Up);

        Handle(vm, CommandTreeAction.TakeTrigger);

        Assert.True(vm.ShowOtherPlatforms);
        Assert.Equal("Zoom Out", vm.SelectedCommand!.Name);
        Assert.Same(Trigger.None, Find(store, "Zoom Out").Trigger);
    }

    [AvaloniaFact]
    public void AGestureIsTakenTheSameWay()
    {
        var (vm, store, picker, _) = Create(CommandsScope.Global);
        Select(vm, "Minimize");
        picker.Result = GesturePickerResult.Selected(Up);

        Handle(vm, CommandTreeAction.PickGesture);

        Assert.Equal("Not saved yet: 'Close window' already uses gesture 'Up' here. Change the gesture, a button or a key.", vm.SelectedCommand!.DraftNote);
        Assert.Equal(("Close window", true, true), (vm.SelectedCommand.ConflictName, vm.SelectedCommand.CanSwapTrigger, vm.SelectedCommand.CanTakeTrigger));

        Handle(vm, CommandTreeAction.TakeTrigger);

        Assert.Equal(Trigger.ForGesture(Up), Find(store, "Minimize").Trigger);
        Assert.Same(Trigger.None, Find(store, "Close window").Trigger);
        Assert.Equal("Close window", vm.SelectedCommand!.Name);
        Assert.Equal($"'Close window' has no trigger now: give it one. {CommandsKeymap.Current.Undo} undoes both.", vm.Message);
    }

    [AvaloniaFact]
    public void NoButtonsForAClashOnlyOnTheOtherPlatform_NorForAWheelWithoutAButton()
    {
        var (vm, store) = GlobalWithZoom();
        // Scrub was authored on macOS on Shift + Right + wheel up and has Ctrl + Right + wheel up of its own here.
        var scrub = new Command(CommandId.New(), "Scrub", Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right, KeyModifiers.Shift)), IsActive: true, [new CommandStep(new DelayStep(1), HostPlatform.MacOS)])
            .WithTriggerFor(HostPlatform.Windows, Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right, KeyModifiers.Control)), DateTimeOffset.UnixEpoch);
        store.AddCommand(GroupId.Global, scrub);
        Select(vm, "Zoom Out");

        Handle(vm, CommandTreeAction.SetTriggerHold, hold: new TriggerHold(HeldButtons.Right, KeyModifiers.Shift));

        Assert.EndsWith("on macOS. Change the direction, a button or a key.", vm.SelectedCommand!.DraftNote, StringComparison.Ordinal);
        Assert.Contains("'Scrub'", vm.SelectedCommand.DraftNote, StringComparison.Ordinal);
        AssertOffersNothing(vm.SelectedCommand);

        Handle(vm, CommandTreeAction.SetTriggerHold, hold: new TriggerHold(HeldButtons.None));

        Assert.Equal("Not saved yet: a wheel trigger needs the stroke button or another button held.", vm.SelectedCommand!.DraftNote);
        AssertOffersNothing(vm.SelectedCommand);
    }

    /// <summary>
    /// Zoom In has its own macOS trigger (Middle + wheel down); Pan, authored on macOS, is on Right + wheel down there. Swapping
    /// would give Zoom Out Right + wheel down here and, converted, on macOS, where Pan has it: refused, so only Take it is offered.
    /// </summary>
    [AvaloniaFact]
    public void ASwapTheRulesWouldRefuseIsNotOffered()
    {
        var (vm, store) = GlobalWithZoom();
        var zoomIn = Find(store, "Zoom In");
        store.UpdateCommand(GroupId.Global, zoomIn.WithTriggerFor(HostPlatform.MacOS, Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.Middle)), DateTimeOffset.UnixEpoch));
        var pan = new Command(CommandId.New(), "Pan", RightWheelDown, IsActive: true, [new CommandStep(new DelayStep(1), HostPlatform.MacOS)])
            .WithTriggerFor(HostPlatform.Windows, Trigger.ForWheel(WheelDirection.Down, new TriggerHold(HeldButtons.Right, KeyModifiers.Shift)), DateTimeOffset.UnixEpoch);
        store.AddCommand(GroupId.Global, pan);
        Select(vm, "Zoom In");

        Handle(vm, CommandTreeAction.SetWheelDirection, wheel: WheelDirection.Up);

        Assert.Equal(("Zoom Out", false, true), (vm.SelectedCommand!.ConflictName, vm.SelectedCommand.CanSwapTrigger, vm.SelectedCommand.CanTakeTrigger));

        // Asked anyway (a stale button), Swap is refused by the store with its words and changes nothing.
        var before = store.Current;
        Handle(vm, CommandTreeAction.SwapTrigger);
        Assert.Same(before, store.Current);
        Assert.Contains("'Pan'", vm.Message, StringComparison.Ordinal);
    }

    /// <summary>The note's buttons through a real header bound to the view model: inside the red box, named, with their tooltips.</summary>
    [AvaloniaFact]
    public void TheNoteHoldsTakeItAndSwap_ClickingTakeItOpensTheOther()
    {
        var (vm, store) = GlobalWithZoom();
        Select(vm, "Zoom In");
        var header = new CommandHeader();
        header.Bind(CommandHeader.ItemProperty, new Binding(nameof(CommandsViewModel.SelectedCommand)) { Source = vm });
        header.ActionRequested += (_, e) => vm.Handle(e);
        new Window { Content = header }.Show();

        Assert.False(Part(header, "PART_TakeTrigger").IsEffectivelyVisible);
        Assert.False(Part(header, "PART_SwapTrigger").IsEffectivelyVisible);

        Combo(header, "PART_WheelDirection").SelectedIndex = TriggerKindExtensions.Directions.ToList().IndexOf(WheelDirection.Up);

        var note = header.GetVisualDescendants().OfType<Border>().Single(border => border.Classes.Contains("warning-note"));
        var take = Part(note, "PART_TakeTrigger");
        var swap = Part(note, "PART_SwapTrigger");
        Assert.True(take.IsEffectivelyVisible);
        Assert.True(swap.IsEffectivelyVisible);
        Assert.Equal(("Take it from 'Zoom Out'", "Swap with 'Zoom Out'"), (take.Content, swap.Content));
        Assert.Equal("Take it: Zoom Out is left with no trigger, and is opened so you can give it one.", ToolTip.GetTip(take));
        Assert.Equal("Swap: Zoom Out gets this command's previous trigger.", ToolTip.GetTip(swap));

        take.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(RightWheelUp, Find(store, "Zoom In").Trigger);
        Assert.Same(Trigger.None, Find(store, "Zoom Out").Trigger);
        Assert.Equal("Zoom Out", header.NameText);
        Assert.Null(header.DraftNote);
        Assert.False(take.IsEffectivelyVisible);
    }

    /// <summary>The test mapping's Global group with Joel's Zoom In (Right + wheel down) and Zoom Out (Right + wheel up) in Media, and no undo history.</summary>
    private static (CommandsViewModel Vm, MappingStore Store) GlobalWithZoom()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);
        var media = Category(store, "Media").Id;
        store.AddCommand(GroupId.Global, Zoom("Zoom In", RightWheelDown) with { CategoryId = media });
        store.AddCommand(GroupId.Global, Zoom("Zoom Out", RightWheelUp) with { CategoryId = media });
        store.ClearHistory();
        return (vm, store);
    }

    private static Command Zoom(string name, Trigger trigger)
        => new(CommandId.New(), name, trigger, IsActive: true, [new CommandStep(new DelayStep(10), HostPlatform.Windows)]);

    private static void AssertOffersNothing(CommandItem item)
        => Assert.Equal((null, false, false), (item.ConflictName, item.CanSwapTrigger, item.CanTakeTrigger));

    private static void Select(CommandsViewModel vm, string name)
    {
        var item = Item(vm, name);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, vm.Sections.Single(section => section.Id == item.Section), item));
    }

    /// <summary>What the header raises for the selected command (the draft, when one waits).</summary>
    private static void Handle(CommandsViewModel vm, CommandTreeAction action, TriggerHold? hold = null, WheelDirection? wheel = null)
        => vm.Handle(new CommandTreeActionEventArgs(action, command: vm.SelectedCommand, hold: hold, wheel: wheel));

    private static Button Part(Visual root, string name) => root.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);

    private static ComboBox Combo(CommandHeader header, string name) => header.GetVisualDescendants().OfType<ComboBox>().Single(combo => combo.Name == name);
}
