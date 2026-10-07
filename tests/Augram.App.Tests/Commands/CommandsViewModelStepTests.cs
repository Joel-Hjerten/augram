using Augram.App.Components.CommandTree;
using Augram.App.Components.StepList;
using Augram.App.Tests.Support;
using Augram.Core.Abstractions;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Steps.WindowOp;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Augram.App.Tests.Commands;

public sealed class CommandsViewModelStepTests
{
    [AvaloniaFact]
    public void AddAppendsADefaultStepAuthoredHereAndExpandsIt()
    {
        var (vm, store, _, _) = Select("Three steps");

        vm.Handle(new StepListActionEventArgs(StepListAction.Add, type: WindowOpStepType.Instance));

        var steps = CommandsTestData.Find(store, "Three steps").Steps;
        Assert.Equal(4, steps.Count);
        Assert.Equal("Minimize window", steps[3].Step.Summary);
        Assert.Equal(HostPlatform.Windows, steps[3].AuthoredOn);
        Assert.Equal(3, vm.SelectedStepIndex);
        Assert.Equal(["Wait 10 ms", "Wait 20 ms", "Wait 30 ms", "Minimize window"], vm.Steps.Select(step => step.Summary));
        Assert.True(vm.CanUndo);
    }

    [AvaloniaFact]
    public void EditReplacesTheStepRecordOneUndoStepPerEdit()
    {
        var (vm, store, _, _) = Select("Three steps");
        vm.Handle(new StepListActionEventArgs(StepListAction.Select, vm.Steps[1]));

        vm.Handle(new StepListActionEventArgs(StepListAction.Edit, vm.Steps[1], edited: new DelayStep(99)));
        vm.Handle(new StepListActionEventArgs(StepListAction.Edit, vm.Steps[1], edited: new DelayStep(100)));

        Assert.Equal("Wait 100 ms", CommandsTestData.Find(store, "Three steps").Steps[1].Step.Summary);
        Assert.Equal(1, vm.SelectedStepIndex);

        vm.Handle(new StepListActionEventArgs(StepListAction.Undo));
        Assert.Equal("Wait 99 ms", vm.Steps[1].Summary);
        vm.Handle(new StepListActionEventArgs(StepListAction.Redo));
        Assert.Equal("Wait 100 ms", vm.Steps[1].Summary);
    }

    [AvaloniaFact]
    public void DuplicateGoesDirectlyBelowAndPasteGoesToTheBottom()
    {
        var (vm, store, _, _) = Select("Three steps");

        vm.Handle(new StepListActionEventArgs(StepListAction.Duplicate, vm.Steps[0]));
        Assert.Equal(["Wait 10 ms", "Wait 10 ms", "Wait 20 ms", "Wait 30 ms"], vm.Steps.Select(step => step.Summary));
        Assert.Equal(1, vm.SelectedStepIndex);

        vm.Handle(new StepListActionEventArgs(StepListAction.Paste));
        Assert.Equal("Nothing to paste: copy a step first.", vm.Message);

        vm.Handle(new StepListActionEventArgs(StepListAction.Copy, vm.Steps[2]));
        Assert.StartsWith("Copied step 'Wait 20 ms'.", vm.Message, StringComparison.Ordinal);
        vm.Handle(new StepListActionEventArgs(StepListAction.Paste));

        Assert.Equal(["Wait 10 ms", "Wait 10 ms", "Wait 20 ms", "Wait 30 ms", "Wait 20 ms"], vm.Steps.Select(step => step.Summary));
        Assert.Equal(4, vm.SelectedStepIndex);
        Assert.Equal(5, CommandsTestData.Find(store, "Three steps").Steps.Count);
    }

    [AvaloniaFact]
    public void DeleteAsksNothingSelectsTheNeighbourAndUndoRestores()
    {
        var confirm = new FakeConfirmPresenter();
        var (vm, store, _, dialogs) = Select("Three steps", confirm);

        vm.Handle(new StepListActionEventArgs(StepListAction.Delete, vm.Steps[2]));

        Assert.Empty(dialogs.Requests);
        Assert.Empty(confirm.Requests);
        Assert.Equal(["Wait 10 ms", "Wait 20 ms"], vm.Steps.Select(step => step.Summary));
        Assert.Equal(1, vm.SelectedStepIndex);
        Assert.Equal($"Deleted step 'Wait 30 ms'. {CommandsKeymap.Current.Undo} undoes it.", vm.Message);

        vm.Handle(new StepListActionEventArgs(StepListAction.Undo));
        Assert.Equal(3, CommandsTestData.Find(store, "Three steps").Steps.Count);
        Assert.Equal(3, vm.Steps.Count);
    }

    [AvaloniaFact]
    public void ToggleActiveAndReorderUpdateTheCommand()
    {
        var (vm, store, _, _) = Select("Three steps");

        vm.Handle(new StepListActionEventArgs(StepListAction.ToggleActive, vm.Steps[0]));
        Assert.False(CommandsTestData.Find(store, "Three steps").Steps[0].IsActive);
        Assert.False(vm.Steps[0].IsActive);

        vm.Handle(new StepListActionEventArgs(StepListAction.Reorder, vm.Steps[0], targetIndex: 2));
        Assert.Equal(["Wait 20 ms", "Wait 30 ms", "Wait 10 ms"], vm.Steps.Select(step => step.Summary));
        Assert.Equal(2, vm.SelectedStepIndex);
        Assert.False(vm.Steps[2].IsActive);

        vm.Handle(new StepListActionEventArgs(StepListAction.Reorder, vm.Steps[2], targetIndex: 0));
        Assert.Equal(["Wait 10 ms", "Wait 20 ms", "Wait 30 ms"], vm.Steps.Select(step => step.Summary));
    }

    [AvaloniaFact]
    public void StepActionsNeedASelectedCommandAndSelectionResetsWhenTheCommandChanges()
    {
        var (vm, _, _, _) = CommandsTestData.Create();

        vm.Handle(new StepListActionEventArgs(StepListAction.Add, type: MediaKeyStepType.Instance));
        Assert.Equal("Select a command first.", vm.Message);

        var global = vm.Groups[0];
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, global, global.Commands.Single(command => command.Name == "Three steps")));
        vm.Handle(new StepListActionEventArgs(StepListAction.Select, vm.Steps[2]));
        Assert.Equal(2, vm.SelectedStepIndex);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, global, global.Commands.Single(command => command.Name == "Volume up")));
        Assert.Equal(-1, vm.SelectedStepIndex);
        Assert.Equal("Volume up", Assert.Single(vm.Steps).Summary);
    }

    private static (ViewModels.Commands.CommandsViewModel Vm, Core.Mapping.MappingStore Store, FakeGesturePickerPresenter Picker, FakeFormDialogPresenter Dialogs) Select(string command, FakeConfirmPresenter? confirm = null)
    {
        var created = CommandsTestData.Create(confirm ?? new FakeConfirmPresenter());
        var global = created.Vm.Groups[0];
        created.Vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, global, global.Commands.Single(item => item.Name == command)));
        return created;
    }
}
