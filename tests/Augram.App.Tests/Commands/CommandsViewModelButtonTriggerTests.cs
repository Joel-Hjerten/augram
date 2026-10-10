using Augram.App.Components.CommandTree;
using Augram.App.Components.StepList;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Remap;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

/// <summary>
/// The Button trigger in the editor (plan 0005 step 5, Joel 2026-10-10: Eyeris's loupe chord "hold Right, press Left" in
/// Augram): the kind beside Gesture and Wheel with the pressed button chosen beside it, the "While holding" set without the
/// stroke button and never with the pressed button, a draft that waits for a button to hold, Swap and Take it on a chord
/// another command uses, and the Remap step offered on it with a key output only.
/// </summary>
public sealed class CommandsViewModelButtonTriggerTests
{
    private static readonly TriggerHold Right = new(HeldButtons.Right);
    private static readonly Trigger RightLeft = Trigger.ForButton(MouseButton.Left, Right);
    private static readonly Trigger RightMiddle = Trigger.ForButton(MouseButton.Middle, Right);

    [AvaloniaFact]
    public void Button_KeepsTheButtonsHeldWithoutTheStrokeButton_PressesLeft_InOneUndoStep()
    {
        var (vm, store) = GlobalWith(("Zoom in", Trigger.ForWheel(WheelDirection.Up, Right with { Keys = KeyModifiers.Shift })));
        Select(vm, "Zoom in");

        Handle(vm, CommandTreeAction.SetTriggerKind, kind: TriggerKind.Button);

        var shiftRightLeft = Trigger.ForButton(MouseButton.Left, Right with { Keys = KeyModifiers.Shift });
        Assert.Equal(shiftRightLeft, Find(store, "Zoom in").Trigger);
        var header = vm.SelectedCommand!;
        Assert.Equal((TriggerKind.Button, "Shift + Right + Left", null), (header.TriggerKind, header.TriggerText, header.DraftNote));
        Assert.Equal("Hold Shift and Right, then press Left: it fires at the press, and a Remap step's key is held until either button is released.", header.TriggerHint);
        Assert.Equal("Right clicks in every app wait until you release or move.", header.AnchorWarning);
        Assert.True(header.ShowsDragDistance);
        Assert.Equal("Shift + Right + Left · Wait 10 ms", Item(vm, "Zoom in").StepSummary);

        Handle(vm, CommandTreeAction.Undo);
        Assert.IsType<Trigger.WheelTrigger>(Find(store, "Zoom in").Trigger);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public void Button_WithNoButtonLeftToHold_WaitsAsADraft_UntilOneIsTicked()
    {
        var (vm, store) = GlobalWith(("Zoom in", Trigger.ForWheel(WheelDirection.Down)));
        Select(vm, "Zoom in");

        // The stroke button is never held by a button trigger: nothing is left, so the draft says what to tick.
        Handle(vm, CommandTreeAction.SetTriggerKind, kind: TriggerKind.Button);

        Assert.Equal(Trigger.ForWheel(WheelDirection.Down), Find(store, "Zoom in").Trigger);
        Assert.Equal((TriggerKind.Button, Trigger.ForButton(MouseButton.Left, new TriggerHold(HeldButtons.None))), (vm.SelectedCommand!.TriggerKind, vm.SelectedCommand.Trigger));
        Assert.Equal("Not saved yet: tick a button to hold, as Right for Right + Left.", vm.SelectedCommand.DraftNote);
        Assert.Null(vm.SelectedCommand.TriggerHint);
        Assert.Equal(TriggerKind.Wheel, Item(vm, "Zoom in").TriggerKind);
        Assert.False(store.CanUndo);

        Handle(vm, CommandTreeAction.SetTriggerHold, hold: Right);

        Assert.Equal(RightLeft, Find(store, "Zoom in").Trigger);
        Assert.Null(vm.SelectedCommand!.DraftNote);
        Assert.Equal("Right + Left", Item(vm, "Zoom in").TriggerText);
        Handle(vm, CommandTreeAction.Undo);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public void ThePressedButtonNeverJoinsItsOwnSet()
    {
        var (vm, store) = GlobalWith(("Magnifier", RightLeft));
        Select(vm, "Magnifier");

        // Right pressed: Right leaves the set, which then holds nothing.
        Handle(vm, CommandTreeAction.SetTriggerButton, button: MouseButton.Right);
        Assert.Equal(RightLeft, Find(store, "Magnifier").Trigger);
        Assert.Equal(Trigger.ForButton(MouseButton.Right, new TriggerHold(HeldButtons.None)), vm.SelectedCommand!.Trigger);
        Assert.Equal("Not saved yet: tick a button to hold, as Left for Left + Right.", vm.SelectedCommand.DraftNote);

        Handle(vm, CommandTreeAction.SetTriggerHold, hold: new TriggerHold(HeldButtons.Left));
        var leftRight = Trigger.ForButton(MouseButton.Right, new TriggerHold(HeldButtons.Left));
        Assert.Equal(leftRight, Find(store, "Magnifier").Trigger);
        Assert.Equal("Left + Right", Item(vm, "Magnifier").TriggerText);

        // Ticking the pressed button as held changes nothing stored.
        var before = store.Current;
        Handle(vm, CommandTreeAction.SetTriggerHold, hold: new TriggerHold(HeldButtons.Left | HeldButtons.Right));
        Assert.Same(before, store.Current);
        Assert.Null(vm.SelectedCommand!.DraftNote);
    }

    [AvaloniaFact]
    public void FromAButtonTrigger_WheelKeepsTheSet_AndNoTriggerUnbinds()
    {
        var (vm, store) = GlobalWith(("Magnifier", RightLeft));
        Select(vm, "Magnifier");

        Handle(vm, CommandTreeAction.SetTriggerKind, kind: TriggerKind.Wheel);
        Assert.Equal(Trigger.ForWheel(WheelDirection.Up, Right), Find(store, "Magnifier").Trigger);

        Handle(vm, CommandTreeAction.SetTriggerKind, kind: TriggerKind.Button);
        Assert.Equal(RightLeft, Find(store, "Magnifier").Trigger);

        Handle(vm, CommandTreeAction.SetTriggerKind, kind: TriggerKind.None);
        Assert.Same(Trigger.None, Find(store, "Magnifier").Trigger);
    }

    [AvaloniaFact]
    public void AChordAnotherCommandUses_OffersSwapAndTakeIt_EachOneUndoStep()
    {
        var (vm, store) = GlobalWith(("Magnifier", RightLeft), ("Back", RightMiddle));
        Select(vm, "Back");

        Handle(vm, CommandTreeAction.SetTriggerButton, button: MouseButton.Left);

        var header = vm.SelectedCommand!;
        Assert.Equal("Not saved yet: 'Magnifier' already uses Right + Left here. Change the button pressed, a button held or a key.", header.DraftNote);
        Assert.Equal(("Magnifier", true, true), (header.ConflictName, header.CanSwapTrigger, header.CanTakeTrigger));

        Handle(vm, CommandTreeAction.SwapTrigger);

        Assert.Equal((RightLeft, RightMiddle), (Find(store, "Back").Trigger, Find(store, "Magnifier").Trigger));
        Assert.Equal($"Swapped with 'Magnifier', which now uses Right + Middle. {CommandsKeymap.Current.Undo} undoes both.", vm.Message);
        Handle(vm, CommandTreeAction.Undo);
        Assert.Equal((RightMiddle, RightLeft), (Find(store, "Back").Trigger, Find(store, "Magnifier").Trigger));
        Assert.False(vm.CanUndo);

        Select(vm, "Back");
        Handle(vm, CommandTreeAction.SetTriggerButton, button: MouseButton.Left);
        Handle(vm, CommandTreeAction.TakeTrigger);

        Assert.Equal(RightLeft, Find(store, "Back").Trigger);
        Assert.Same(Trigger.None, Find(store, "Magnifier").Trigger);
        Assert.Equal("Magnifier", vm.SelectedCommand!.Name);
    }

    [AvaloniaFact]
    public void TheHeaderChoosesThePressedButtonBesideTheKind_AndLocksTheStrokeAndPressedBoxes()
    {
        var (vm, store) = GlobalWith(("Magnifier", RightLeft));
        Select(vm, "Magnifier");
        var header = Bound(vm);

        Assert.True(header.IsButtonKind);
        Assert.False(header.IsWheelKind);
        Assert.Equal(TriggerKindExtensions.All.ToList().IndexOf(TriggerKind.Button), header.KindIndex);
        var pressed = Combo(header, "PART_TriggerButton");
        Assert.True(pressed.IsEffectivelyVisible);
        Assert.Equal(["Left", "Right", "Middle", "X1", "X2"], pressed.ItemsSource!.Cast<string>());
        Assert.Equal(0, header.PressedButtonIndex);
        Assert.Equal((false, false), (Box(header, "PART_HoldStroke").IsEnabled, Box(header, "PART_HoldStroke").IsChecked == true));
        Assert.Equal((false, false), (Box(header, "PART_HoldLeft").IsEnabled, Box(header, "PART_HoldLeft").IsChecked == true));
        Assert.Equal((true, true), (Box(header, "PART_HoldRight").IsEnabled, Box(header, "PART_HoldRight").IsChecked == true));

        pressed.SelectedIndex = TriggerKindExtensions.PressedButtons.ToList().IndexOf(MouseButton.Middle);

        Assert.Equal(RightMiddle, Find(store, "Magnifier").Trigger);
        Assert.Equal(2, header.PressedButtonIndex);
        Assert.Equal((true, false), (Box(header, "PART_HoldLeft").IsEnabled, Box(header, "PART_HoldLeft").IsChecked == true));
        Assert.False(Box(header, "PART_HoldMiddle").IsEnabled);

        Select(vm, "Close window");
        Assert.False(header.IsButtonKind);
        Assert.False(pressed.IsEffectivelyVisible);
        Assert.Equal(-1, header.PressedButtonIndex);
    }

    [AvaloniaFact]
    public void TheRemapStepIsOfferedOnAButtonTrigger_AndStartsAsAKeyWithNoKeySet()
    {
        var (vm, store) = GlobalWithSteps("Magnifier", RightLeft);
        Select(vm, "Close window");
        Assert.False(vm.SelectedCommand!.OffersRemapStep);

        Select(vm, "Magnifier");
        Assert.True(vm.SelectedCommand!.OffersRemapStep);

        vm.Handle(new StepListActionEventArgs(StepListAction.Add, type: RemapStepType.Instance));

        Assert.Equal(new RemapStep(new RemapOutput.Key(KeyCode.None)), Assert.Single(Find(store, "Magnifier").Steps).Step);
        Assert.Null(vm.Message);
    }

    [AvaloniaFact]
    public void AButtonTriggerTurnsARemapButtonOutputIntoAKey_InTheSameUndoStep()
    {
        // An ordinary command that kept its Remap step (pasted out of a hold remap, say), held with Right.
        var remap = new RemapStep(new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Shift));
        var (vm, store) = GlobalWithSteps("Orbit", Trigger.ForWheel(WheelDirection.Down, Right), remap);
        Select(vm, "Orbit");

        Handle(vm, CommandTreeAction.SetTriggerKind, kind: TriggerKind.Button);

        var orbit = Find(store, "Orbit");
        Assert.Equal(RightLeft, orbit.Trigger);
        Assert.Equal(new RemapStep(new RemapOutput.Key(KeyCode.None, KeyModifiers.Shift)), Assert.Single(orbit.Steps).Step);

        Handle(vm, CommandTreeAction.Undo);
        Assert.Equal(remap, Assert.Single(Find(store, "Orbit").Steps).Step);
        Assert.False(vm.CanUndo);
    }

    /// <summary>The test mapping's Global group with <paramref name="commands"/> added to Media (a 10 ms wait each), and no undo history.</summary>
    private static (CommandsViewModel Vm, MappingStore Store) GlobalWith(params (string Name, Trigger Trigger)[] commands)
        => Global([.. commands.Select(command => (command.Name, command.Trigger, new IStep[] { new DelayStep(10) }))]);

    /// <summary>The test mapping's Global group with one command in Media with <paramref name="steps"/>, and no undo history.</summary>
    private static (CommandsViewModel Vm, MappingStore Store) GlobalWithSteps(string name, Trigger trigger, params IStep[] steps)
        => Global([(name, trigger, steps)]);

    private static (CommandsViewModel Vm, MappingStore Store) Global(IReadOnlyList<(string Name, Trigger Trigger, IStep[] Steps)> commands)
    {
        var (vm, store, _, _) = Create(CommandsScope.Global);
        var media = Category(store, "Media").Id;
        foreach (var (name, trigger, steps) in commands)
        {
            store.AddCommand(GroupId.Global, new Command(CommandId.New(), name, trigger, IsActive: true, [.. steps.Select(step => new CommandStep(step, HostPlatform.Windows))]) { CategoryId = media });
        }

        store.ClearHistory();
        return (vm, store);
    }

    private static CommandHeader Bound(CommandsViewModel vm)
    {
        var header = new CommandHeader();
        header.Bind(CommandHeader.ItemProperty, new Binding(nameof(CommandsViewModel.SelectedCommand)) { Source = vm });
        header.ActionRequested += (_, e) => vm.Handle(e);
        new Window { Content = header, Width = 900 }.Show();
        return header;
    }

    private static void Select(CommandsViewModel vm, string name)
    {
        var item = Item(vm, name);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, vm.Sections.Single(section => section.Id == item.Section), item));
    }

    /// <summary>What the header raises for the selected command (the draft, when one waits).</summary>
    private static void Handle(CommandsViewModel vm, CommandTreeAction action, TriggerKind? kind = null, TriggerHold? hold = null, MouseButton? button = null)
        => vm.Handle(new CommandTreeActionEventArgs(action, command: vm.SelectedCommand, kind: kind, hold: hold, button: button));

    private static CheckBox Box(CommandHeader header, string name) => header.GetVisualDescendants().OfType<CheckBox>().Single(box => box.Name == name);

    private static ComboBox Combo(CommandHeader header, string name) => header.GetVisualDescendants().OfType<ComboBox>().Single(combo => combo.Name == name);
}
