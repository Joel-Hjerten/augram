using Augram.App.Components.CommandTree;
using Augram.App.Components.GesturePicker;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Augram.Core.Steps.MediaKey;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

/// <summary>
/// The trigger draft (Joel, 2026-10-09: Wheel could not be chosen in Global, and a ticked button could not be unticked): a
/// trigger edit the rules refuse waits in the header with a note, the next edit starts from it, and it saves as one undo step
/// as soon as it is valid. The row keeps showing the stored trigger.
/// </summary>
public sealed class CommandsViewModelTriggerDraftTests
{
    private const string WheelUpTaken = "Not saved yet: 'Volume up' already uses Stroke button + wheel up here. Change the direction, a button or a key.";
    private const string NeedsButton = "Not saved yet: a wheel trigger needs the stroke button or another button held.";

    [AvaloniaFact]
    public void ChoosingWheelWhereBothDirectionsAreTakenKeepsADraft_UntickingStrokeAndTickingMiddleSavesMiddleWheelUp()
    {
        var (vm, store) = GlobalWithBothWheelDirectionsTaken();
        var before = store.Current;
        Select(vm, "Close window");

        Handle(vm, CommandTreeAction.SetTriggerKind, kind: TriggerKind.Wheel);

        Assert.Same(before, store.Current);
        Assert.Equal((TriggerKind.Wheel, Trigger.ForWheel(WheelDirection.Up)), (vm.SelectedCommand!.TriggerKind, vm.SelectedCommand.Trigger));
        Assert.Equal(WheelUpTaken, vm.SelectedCommand.DraftNote);
        Assert.Null(vm.Message);
        AssertRowIsStored(vm);

        Handle(vm, CommandTreeAction.SetTriggerHold, hold: new TriggerHold(HeldButtons.None));

        Assert.Same(before, store.Current);
        Assert.Equal(Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.None)), vm.SelectedCommand!.Trigger);
        Assert.Equal(NeedsButton, vm.SelectedCommand.DraftNote);
        AssertRowIsStored(vm);

        Handle(vm, CommandTreeAction.SetTriggerHold, hold: new TriggerHold(HeldButtons.Middle));

        Assert.Equal(Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Middle)), Find(store, "Close window").Trigger);
        Assert.Null(vm.SelectedCommand!.DraftNote);
        Assert.Equal("Middle + wheel up", Item(vm, "Close window").TriggerText);
        AssertOneUndoStep(vm, store);
    }

    [AvaloniaFact]
    public void TickingShiftOnTheDraftSavesStrokeShiftWheelUpAsOneUndoStep()
    {
        var (vm, store) = GlobalWithBothWheelDirectionsTaken();
        Select(vm, "Close window");
        Handle(vm, CommandTreeAction.SetTriggerKind, kind: TriggerKind.Wheel);

        Handle(vm, CommandTreeAction.SetTriggerHold, hold: TriggerHold.WithStroke(KeyModifiers.Shift));

        Assert.Equal(Trigger.ForWheel(WheelDirection.Up, TriggerHold.WithStroke(KeyModifiers.Shift)), Find(store, "Close window").Trigger);
        Assert.Null(vm.SelectedCommand!.DraftNote);
        Assert.Equal("Shift + wheel up", Item(vm, "Close window").TriggerText);
        AssertOneUndoStep(vm, store);
    }

    [AvaloniaFact]
    public void UntickingTheLastButtonKeepsADraftThatNeedsAButton_AndAnUndoUnderItDropsIt()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);
        Select(vm, "Volume up");
        Handle(vm, CommandTreeAction.SetTriggerHold, hold: TriggerHold.WithStroke(KeyModifiers.Shift));
        var saved = store.Current;

        Handle(vm, CommandTreeAction.SetTriggerHold, hold: new TriggerHold(HeldButtons.None, KeyModifiers.Shift));

        Assert.Same(saved, store.Current);
        Assert.Equal(NeedsButton, vm.SelectedCommand!.DraftNote);
        Assert.False(vm.SelectedCommand.Trigger.Hold.HoldsStroke);
        Assert.Equal("Shift + wheel up", Item(vm, "Volume up").TriggerText);

        // The undo takes Shift away from the stored trigger, so the draft drawn over it goes too.
        Handle(vm, CommandTreeAction.Undo);

        Assert.Equal(Trigger.ForWheel(WheelDirection.Up), Find(store, "Volume up").Trigger);
        Assert.Null(vm.SelectedCommand!.DraftNote);
        Assert.Equal(Trigger.ForWheel(WheelDirection.Up), vm.SelectedCommand.Trigger);
    }

    [AvaloniaFact]
    public void SelectingAnotherCommandDropsTheDraft()
    {
        var (vm, store) = GlobalWithBothWheelDirectionsTaken();
        Select(vm, "Close window");
        Handle(vm, CommandTreeAction.SetTriggerKind, kind: TriggerKind.Wheel);
        Assert.NotNull(vm.SelectedCommand!.DraftNote);

        Select(vm, "Minimize");
        Assert.Null(vm.SelectedCommand!.DraftNote);

        Select(vm, "Close window");
        Assert.Null(vm.SelectedCommand!.DraftNote);
        Assert.Equal((TriggerKind.Gesture, Trigger.ForGesture(Up)), (vm.SelectedCommand.TriggerKind, vm.SelectedCommand.Trigger));
        Assert.Equal(Trigger.ForGesture(Up), Find(store, "Close window").Trigger);
    }

    [AvaloniaFact]
    public void AGestureAnotherCommandUsesWaitsAsADraft_AKeySavesIt()
    {
        var (vm, store, picker, _) = Create(CommandsScope.Global);
        Select(vm, "Close window");
        picker.Result = GesturePickerResult.Selected(Down);

        Handle(vm, CommandTreeAction.PickGesture);

        Assert.Equal(Trigger.ForGesture(Up), Find(store, "Close window").Trigger);
        Assert.Equal((Trigger.ForGesture(Down), "Down"), (vm.SelectedCommand!.Trigger, vm.SelectedCommand.TriggerText));
        Assert.Equal("Not saved yet: 'Minimize' already uses gesture 'Down' here. Change the gesture, a button or a key.", vm.SelectedCommand.DraftNote);

        Handle(vm, CommandTreeAction.SetTriggerHold, hold: TriggerHold.WithStroke(KeyModifiers.Shift));

        Assert.Equal(Trigger.ForGesture(Down, TriggerHold.WithStroke(KeyModifiers.Shift)), Find(store, "Close window").Trigger);
        Assert.Null(vm.SelectedCommand!.DraftNote);
    }

    /// <summary>Joel's two reports through a real header bound to the view model: Wheel stays chosen, and every box unticks.</summary>
    [AvaloniaFact]
    public void TheHeaderShowsTheDraftInsteadOfSnappingBack()
    {
        var (vm, store) = GlobalWithBothWheelDirectionsTaken();
        Select(vm, "Close window");
        var header = new CommandHeader();
        header.Bind(CommandHeader.ItemProperty, new Binding(nameof(CommandsViewModel.SelectedCommand)) { Source = vm });
        header.ActionRequested += (_, e) => vm.Handle(e);
        new Window { Content = header }.Show();

        Combo(header, "PART_TriggerKind").SelectedIndex = TriggerKindExtensions.All.ToList().IndexOf(TriggerKind.Wheel);

        Assert.Equal(TriggerKindExtensions.All.ToList().IndexOf(TriggerKind.Wheel), header.KindIndex);
        Assert.Equal(0, header.WheelIndex);
        Assert.Equal(WheelUpTaken, header.DraftNote);
        Assert.Equal(WheelUpTaken, header.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PART_DraftNote").Text);

        Box(header, "PART_HoldStroke").IsChecked = false;
        Assert.False(Box(header, "PART_HoldStroke").IsChecked);
        Assert.Equal(NeedsButton, header.DraftNote);

        // Right + wheel up is free, so ticking Right saves it; unticking Right again leaves no button, which waits as a draft.
        Box(header, "PART_HoldRight").IsChecked = true;
        Assert.Equal(Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right)), Find(store, "Close window").Trigger);
        Assert.Null(header.DraftNote);
        Box(header, "PART_HoldRight").IsChecked = false;
        Assert.False(Box(header, "PART_HoldRight").IsChecked);
        Assert.Equal(NeedsButton, header.DraftNote);
        Assert.Equal(Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Right)), Find(store, "Close window").Trigger);

        Box(header, "PART_HoldMiddle").IsChecked = true;
        Assert.Equal(Trigger.ForWheel(WheelDirection.Up, new TriggerHold(HeldButtons.Middle)), Find(store, "Close window").Trigger);
        Assert.Null(header.DraftNote);
        Assert.True(Box(header, "PART_HoldMiddle").IsChecked);
        Assert.False(Box(header, "PART_HoldStroke").IsChecked);
    }

    /// <summary>The test mapping's Global group, with Volume down on wheel down beside Volume up on wheel up, and no undo history.</summary>
    private static (CommandsViewModel Vm, MappingStore Store) GlobalWithBothWheelDirectionsTaken()
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);
        var volumeDown = new Command(CommandId.New(), "Volume down", Trigger.ForWheel(WheelDirection.Down), IsActive: true, [new CommandStep(new MediaKeyStep(MediaKeyKind.VolumeDown), HostPlatform.Windows)]);
        store.AddCommand(GroupId.Global, volumeDown);
        store.ClearHistory();
        return (vm, store);
    }

    private static void Select(CommandsViewModel vm, string name)
    {
        var item = Item(vm, name);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, vm.Sections.Single(section => section.Id == item.Section), item));
    }

    /// <summary>What the header raises for the selected command (the draft, when one waits).</summary>
    private static void Handle(CommandsViewModel vm, CommandTreeAction action, TriggerKind? kind = null, TriggerHold? hold = null)
        => vm.Handle(new CommandTreeActionEventArgs(action, command: vm.SelectedCommand, kind: kind, hold: hold));

    /// <summary>The row of Close window still reads as stored: the Up gesture, no note.</summary>
    private static void AssertRowIsStored(CommandsViewModel vm)
    {
        var row = Item(vm, "Close window");
        Assert.Equal((TriggerKind.Gesture, "Up", Trigger.ForGesture(Up)), (row.TriggerKind, row.TriggerText, row.Trigger));
        Assert.Null(row.DraftNote);
    }

    private static void AssertOneUndoStep(CommandsViewModel vm, MappingStore store)
    {
        Assert.True(vm.CanUndo);
        Handle(vm, CommandTreeAction.Undo);
        Assert.Equal(Trigger.ForGesture(Up), Find(store, "Close window").Trigger);
        Assert.False(vm.CanUndo);
    }

    private static CheckBox Box(CommandHeader header, string name) => header.GetVisualDescendants().OfType<CheckBox>().Single(box => box.Name == name);

    private static ComboBox Combo(CommandHeader header, string name) => header.GetVisualDescendants().OfType<ComboBox>().Single(combo => combo.Name == name);
}
