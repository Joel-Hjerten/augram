using Augram.App.Components.CommandTree;
using Augram.App.Components.StepList;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Core.Mapping;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Remap;
using Avalonia.Headless.XUnit;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;
using static Augram.App.Tests.Commands.HoldRemapTestData;

namespace Augram.App.Tests.Commands;

/// <summary>
/// A command under a hold remap (F9, plan 0002 step 4): its input from the header (buttons built up into a set, a wheel
/// direction, a key), saved when Core's rules take it and otherwise kept as the header's draft with their words; its Remap
/// step, whose refused output shows the rule and puts the form back.
/// </summary>
public sealed class CommandsViewModelInputTests
{
    [AvaloniaFact]
    public void ButtonsBuildUpASet_WhatTheRulesRefuseWaitsAsADraftWithTheirWords()
    {
        var (vm, store, command) = NewCommandUnderSpace();

        Handle(vm, CommandTreeAction.SetInputKind, inputKind: InputKind.Buttons);

        Assert.Same(Trigger.None, Find(store, "New command 1").Trigger);
        Assert.Equal(Trigger.ForInput(new HoldInput.Buttons(HeldButtons.None)), vm.SelectedCommand!.Trigger);
        Assert.Equal("Not saved yet: 'New command 1' needs at least one button as its input.", vm.SelectedCommand.DraftNote);

        Handle(vm, CommandTreeAction.SetInput, input: HoldInput.Of(MouseButton.Left));
        Assert.Equal("Not saved yet: 'Orbit' already uses Left here. Choose another input.", vm.SelectedCommand!.DraftNote);
        Assert.Same(Trigger.None, Find(store, "New command 1").Trigger);

        Handle(vm, CommandTreeAction.SetInput, input: HoldInput.Of(MouseButton.Left, MouseButton.X1));

        Assert.Equal(Trigger.ForInput(HoldInput.Of(MouseButton.Left, MouseButton.X1)), Find(store, "New command 1").Trigger);
        Assert.Null(vm.SelectedCommand!.DraftNote);
        Assert.Equal("Left + X1", Item(vm, "New command 1").TriggerText);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.Same(Trigger.None, Find(store, command.Name).Trigger);
    }

    [AvaloniaFact]
    public void AKeyInputIsAKeyThatIsNotTheHoldKey_AWheelTakesAFreeDirection()
    {
        var (vm, store, _) = NewCommandUnderSpace();

        Handle(vm, CommandTreeAction.SetInputKind, inputKind: InputKind.Key);
        Assert.Equal("Not saved yet: 'New command 1' needs a key as its input.", vm.SelectedCommand!.DraftNote);

        Handle(vm, CommandTreeAction.SetInput, input: new HoldInput.Key(KeyCode.Space));
        Assert.Equal("Not saved yet: 'New command 1' cannot use Space as its input: it is the hold key of 'Space'.", vm.SelectedCommand!.DraftNote);

        Handle(vm, CommandTreeAction.SetInput, input: new HoldInput.Key(KeyCode.Q));
        Assert.Equal(Trigger.ForInput(new HoldInput.Key(KeyCode.Q)), Find(store, "New command 1").Trigger);
        Assert.Equal("Q", Item(vm, "New command 1").TriggerText);

        Handle(vm, CommandTreeAction.SetInputKind, inputKind: InputKind.Wheel);
        Assert.Equal(Trigger.ForInput(new HoldInput.Wheel(WheelDirection.Up)), Find(store, "New command 1").Trigger);
        Assert.Equal("wheel up", Item(vm, "New command 1").TriggerText);

        Handle(vm, CommandTreeAction.SetInputKind, inputKind: InputKind.None);
        Assert.Same(Trigger.None, Find(store, "New command 1").Trigger);
    }

    /// <summary>The coordinator's follow-up: a changed input brings its Remap output along (Core's FittedTo), one undo step, both ways.</summary>
    [AvaloniaFact]
    public void ChangingTheInputFitsTheRemapOutputInTheSameUndoStep()
    {
        var (vm, store, _) = CreateBlender();
        Select(vm, "Pan");

        Handle(vm, CommandTreeAction.SetInputKind, inputKind: InputKind.Wheel);

        Assert.Equal(Trigger.ForInput(new HoldInput.Wheel(WheelDirection.Up)), Find(store, "Pan").Trigger);
        Assert.Equal(new RemapOutput.Wheel(ScrollDirection.Up, KeyModifiers.Shift), RemapOf(store, "Pan"));
        Assert.Null(vm.SelectedCommand!.DraftNote);
        Assert.Equal("Remap to Shift + wheel up", Item(vm, "Pan").StepSummary);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.Equal(Trigger.ForInput(HoldInput.Of(MouseButton.Right)), Find(store, "Pan").Trigger);
        Assert.Equal(new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Shift), RemapOf(store, "Pan"));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Redo));
        Handle(vm, CommandTreeAction.SetInput, input: HoldInput.Of(MouseButton.X1));

        Assert.Equal(Trigger.ForInput(HoldInput.Of(MouseButton.X1)), Find(store, "Pan").Trigger);
        Assert.Equal(new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Shift), RemapOf(store, "Pan"));

        // A key output fits every input: it stays.
        Select(vm, "Grab");
        Handle(vm, CommandTreeAction.SetInput, input: new HoldInput.Wheel(WheelDirection.Down));
        Assert.Equal(new RemapOutput.Key(KeyCode.G), RemapOf(store, "Grab"));
    }

    [AvaloniaFact]
    public void ARemapStepAddedUnderAWheelInputStartsAsANotchTheSameWay()
    {
        var (vm, store, _) = NewCommandUnderSpace();
        Handle(vm, CommandTreeAction.SetInput, input: new HoldInput.Wheel(WheelDirection.Down));

        vm.Handle(new StepListActionEventArgs(StepListAction.Add, type: RemapStepType.Instance));

        Assert.Null(vm.Message);
        Assert.Equal(new RemapOutput.Wheel(ScrollDirection.Down), RemapOf(store, "New command 1"));
        Assert.Equal("Remap to wheel down", Item(vm, "New command 1").StepSummary);
    }

    [AvaloniaFact]
    public void ARemapStepIsAddedUnderAHoldRemap_ARefusedOutputShowsTheRuleAndTheStoredStepAgain()
    {
        var (vm, store, _) = NewCommandUnderSpace();

        vm.Handle(new StepListActionEventArgs(StepListAction.Add, type: RemapStepType.Instance));

        var remap = Assert.IsType<RemapStep>(Assert.Single(Find(store, "New command 1").Steps).Step);
        Assert.Equal(new RemapOutput.Button(MouseButton.Middle), remap.Output);
        Assert.Equal(0, vm.SelectedStepIndex);

        vm.Handle(new StepListActionEventArgs(StepListAction.Add, type: DelayStepType.Instance));
        Assert.Equal("'New command 1' has a Remap step among other steps: a Remap step is a command's only step.", vm.Message);
        Assert.Single(Find(store, "New command 1").Steps);

        Select(vm, "Orbit");
        var steps = vm.Steps;
        vm.Handle(new StepListActionEventArgs(StepListAction.Edit, vm.Steps[0], edited: new RemapStep(new RemapOutput.Wheel(ScrollDirection.Up))));

        Assert.Equal("'Orbit' sends a wheel notch: that output is for a wheel input only.", vm.Message);
        Assert.Equal(new RemapOutput.Button(MouseButton.Middle), Assert.IsType<RemapStep>(Find(store, "Orbit").Steps[0].Step).Output);
        Assert.NotSame(steps, vm.Steps);
        Assert.Equal(steps, vm.Steps);

        vm.Handle(new StepListActionEventArgs(StepListAction.Edit, vm.Steps[0], edited: new RemapStep(new RemapOutput.Button(MouseButton.Middle, KeyModifiers.Alt))));
        Assert.Null(vm.Message);
        Assert.Equal("Remap to Alt + Middle", Item(vm, "Orbit").StepSummary);
    }

    [AvaloniaFact]
    public void TheWorkbenchKnowsWhenTheSelectedCommandIsUnderAHoldRemap()
    {
        var (vm, _, _) = CreateBlender();

        Select(vm, "Orbit");
        Assert.True(vm.SelectedCommand!.IsUnderHoldRemap);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, Section(vm, "Blender"), Item(vm, "Undo")));
        Assert.False(vm.SelectedCommand!.IsUnderHoldRemap);
        Assert.Equal("Left", vm.SelectedCommand.TriggerText);
    }

    private static (CommandsViewModel Vm, MappingStore Store, Command Command) NewCommandUnderSpace()
    {
        var (vm, store, _) = CreateBlender();
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.NewCommand, HoldRemapSection(vm, "Space")));
        return (vm, store, Find(store, "New command 1"));
    }

    private static RemapOutput RemapOf(MappingStore store, string command)
        => Assert.IsType<RemapStep>(Assert.Single(Find(store, command).Steps).Step).Output;

    private static void Select(CommandsViewModel vm, string command)
    {
        var item = Item(vm, command);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, vm.Sections.Single(section => section.Id == item.Section), item));
    }

    private static void Handle(CommandsViewModel vm, CommandTreeAction action, InputKind? inputKind = null, HoldInput? input = null)
        => vm.Handle(new CommandTreeActionEventArgs(action, command: vm.SelectedCommand, inputKind: inputKind, input: input));
}
