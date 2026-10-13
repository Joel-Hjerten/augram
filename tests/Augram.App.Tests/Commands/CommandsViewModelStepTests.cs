using Augram.App.Components.CommandTree;
using Augram.App.Components.ListEditing;
using Augram.App.Components.StepList;
using Augram.App.Tests.Support;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Augram.Core.Steps.Delay;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.MediaKey;
using Augram.Core.Steps.Remap;
using Augram.Core.Steps.WindowOp;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Augram.App.Tests.Commands;

public sealed class CommandsViewModelStepTests
{
    /// <summary>
    /// Plan 0011, Joel 2026-10-13 (third round): a Hotkey step's summary is the active layout's shortcut, so the step row and the
    /// command row re-read it when the layout changes (the system event, projected again on the UI thread). Through the process-wide
    /// <see cref="HotkeyText.Layouts"/> hook, reset in a <c>finally</c>; no other App test names the bracket key.
    /// </summary>
    [AvaloniaFact]
    public void TheStepAndCommandRowsRenameAHotkeyStepWhenTheKeyboardLayoutChanges()
    {
        var layouts = new FakeKeyboardLayouts(FakeKeyboardLayouts.Swedish, FakeKeyboardLayouts.Abc);
        layouts.CharactersOf(FakeKeyboardLayouts.Swedish.Id)[KeyCode.OpenBracket] = "å";
        layouts.CharactersOf(FakeKeyboardLayouts.Abc.Id)[KeyCode.OpenBracket] = "[";
        var events = new FakeSystemEvents();
        var store = CommandsTestData.Store();
        using var vm = new CommandsViewModel(CommandsScope.Global, store, new GestureLibrary(StarterGestures.All()), new FakeGesturePickerPresenter(), new FakeFormDialogPresenter(), new FakeConfirmPresenter(), new CommandClipboard(), HostPlatform.Windows, systemEvents: events);
        var item = CommandsTestData.Item(vm, "Volume up");
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, vm.Sections.Single(section => section.Id == item.Section), item));
        var bracket = new HotkeyBinding(KeyModifiers.Meta, KeyCode.OpenBracket);
        try
        {
            HotkeyText.Layouts = layouts;
            vm.Handle(new StepListActionEventArgs(StepListAction.Edit, vm.Steps[0], edited: new HotkeyStep(bracket.Modifiers, bracket.Key) { Layouts = new Dictionary<string, HotkeyBinding> { [FakeKeyboardLayouts.Swedish.Id] = bracket, [FakeKeyboardLayouts.Abc.Id] = bracket } }));
            Assert.Equal("Win+Å · per layout", vm.Steps[0].Summary);
            Assert.Equal("Win+Å · per layout", CommandsTestData.Item(vm, "Volume up").StepSummary);

            layouts.Active = FakeKeyboardLayouts.Abc;
            events.Raise(SystemEventKind.KeyboardLayoutChanged);

            Assert.Equal("Win+[ · per layout", vm.Steps[0].Summary);
            Assert.Equal("Win+[ · per layout", CommandsTestData.Item(vm, "Volume up").StepSummary);
        }
        finally
        {
            HotkeyText.Layouts = NullKeyboardLayouts.Instance;
        }
    }

    [AvaloniaFact]
    public void AddAppendsADefaultStepAuthoredHereAndOpensIt_TheOpenStepsStayOpen()
    {
        var (vm, store, _, _) = Select("Three steps");
        vm.Handle(new StepListActionEventArgs(StepListAction.ToggleOpen, vm.Steps[0]));

        vm.Handle(new StepListActionEventArgs(StepListAction.Add, type: WindowOpStepType.Instance));

        var steps = CommandsTestData.Find(store, "Three steps").Steps;
        Assert.Equal(4, steps.Count);
        Assert.Equal("Minimize window", steps[3].Step.Summary);
        Assert.Equal(HostPlatform.Windows, steps[3].AuthoredOn);
        Assert.Equal(3, vm.SelectedStepIndex);
        Assert.Equal([0, 3], vm.OpenStepIndexes);
        Assert.Equal(["Wait 10 ms", "Wait 20 ms", "Wait 30 ms", "Minimize window"], vm.Steps.Select(step => step.Summary));
        Assert.True(vm.CanUndo);
    }

    [AvaloniaFact]
    public void EditReplacesTheStepRecordOneUndoStepPerEdit_AndTheOpenStepsStayOpenThroughUndoAndRedo()
    {
        var (vm, store, _, _) = Select("Three steps");
        vm.Handle(new StepListActionEventArgs(StepListAction.ToggleOpen, vm.Steps[2]));
        vm.Handle(new StepListActionEventArgs(StepListAction.ToggleOpen, vm.Steps[1]));

        vm.Handle(new StepListActionEventArgs(StepListAction.Edit, vm.Steps[1], edited: new DelayStep(99)));
        vm.Handle(new StepListActionEventArgs(StepListAction.Edit, vm.Steps[1], edited: new DelayStep(100)));

        Assert.Equal("Wait 100 ms", CommandsTestData.Find(store, "Three steps").Steps[1].Step.Summary);
        Assert.Equal(1, vm.SelectedStepIndex);
        Assert.Equal([1, 2], vm.OpenStepIndexes);

        vm.Handle(new StepListActionEventArgs(StepListAction.Undo));
        Assert.Equal("Wait 99 ms", vm.Steps[1].Summary);
        vm.Handle(new StepListActionEventArgs(StepListAction.Redo));
        Assert.Equal("Wait 100 ms", vm.Steps[1].Summary);
        Assert.Equal([1, 2], vm.OpenStepIndexes);
    }

    [AvaloniaFact]
    public void DuplicateGoesDirectlyBelowAndPasteGoesToTheBottom_EachOpensTheNewStepAndKeepsTheOthersOpen()
    {
        var (vm, store, _, _) = Select("Three steps");
        vm.Handle(new StepListActionEventArgs(StepListAction.ToggleOpen, vm.Steps[2]));

        vm.Handle(new StepListActionEventArgs(StepListAction.Duplicate, vm.Steps[0]));
        Assert.Equal(["Wait 10 ms", "Wait 10 ms", "Wait 20 ms", "Wait 30 ms"], vm.Steps.Select(step => step.Summary));
        Assert.Equal(1, vm.SelectedStepIndex);
        Assert.Equal([1, 3], vm.OpenStepIndexes);

        vm.Handle(new StepListActionEventArgs(StepListAction.Paste));
        Assert.Equal("Nothing to paste: copy a step first.", vm.Message);

        vm.Handle(new StepListActionEventArgs(StepListAction.Copy, vm.Steps[2]));
        Assert.StartsWith("Copied step 'Wait 20 ms'.", vm.Message, StringComparison.Ordinal);
        vm.Handle(new StepListActionEventArgs(StepListAction.Paste));

        Assert.Equal(["Wait 10 ms", "Wait 10 ms", "Wait 20 ms", "Wait 30 ms", "Wait 20 ms"], vm.Steps.Select(step => step.Summary));
        Assert.Equal(4, vm.SelectedStepIndex);
        Assert.Equal([1, 3, 4], vm.OpenStepIndexes);
        Assert.Equal(5, CommandsTestData.Find(store, "Three steps").Steps.Count);
    }

    [AvaloniaFact]
    public void DeleteAsksNothing_OpensAndClosesNothingElse_AndUndoRestores()
    {
        var confirm = new FakeConfirmPresenter();
        var (vm, store, _, dialogs) = Select("Three steps", confirm);
        vm.Handle(new StepListActionEventArgs(StepListAction.ToggleOpen, vm.Steps[1]));
        vm.Handle(new StepListActionEventArgs(StepListAction.ToggleOpen, vm.Steps[2]));

        vm.Handle(new StepListActionEventArgs(StepListAction.Delete, vm.Steps[0]));

        Assert.Empty(dialogs.Requests);
        Assert.Empty(confirm.Requests);
        Assert.Equal(["Wait 20 ms", "Wait 30 ms"], vm.Steps.Select(step => step.Summary));
        Assert.Equal(1, vm.SelectedStepIndex);
        Assert.Equal([0, 1], vm.OpenStepIndexes);
        Assert.Equal($"Deleted step 'Wait 10 ms'. {EditKeymap.Current.Undo} undoes it.", vm.Message);

        // Deleting an open step closes only that one.
        vm.Handle(new StepListActionEventArgs(StepListAction.Delete, vm.Steps[1]));
        Assert.Equal(["Wait 20 ms"], vm.Steps.Select(step => step.Summary));
        Assert.Equal(-1, vm.SelectedStepIndex);
        Assert.Equal([0], vm.OpenStepIndexes);

        vm.Handle(new StepListActionEventArgs(StepListAction.Undo));
        vm.Handle(new StepListActionEventArgs(StepListAction.Undo));

        vm.Handle(new StepListActionEventArgs(StepListAction.Undo));
        Assert.Equal(3, CommandsTestData.Find(store, "Three steps").Steps.Count);
        Assert.Equal(3, vm.Steps.Count);
    }

    [AvaloniaFact]
    public void ToggleActiveAndReorderUpdateTheCommandAndEachStepKeepsItsOpenState()
    {
        var (vm, store, _, _) = Select("Three steps");
        vm.Handle(new StepListActionEventArgs(StepListAction.ToggleOpen, vm.Steps[0]));
        vm.Handle(new StepListActionEventArgs(StepListAction.ToggleOpen, vm.Steps[1]));

        // The active box opens and closes nothing (Joel, 0.11.4).
        vm.Handle(new StepListActionEventArgs(StepListAction.ToggleActive, vm.Steps[0]));
        Assert.False(CommandsTestData.Find(store, "Three steps").Steps[0].IsActive);
        Assert.False(vm.Steps[0].IsActive);
        Assert.Equal(1, vm.SelectedStepIndex);
        Assert.Equal([0, 1], vm.OpenStepIndexes);

        // A reorder moves each open step with its step, the dragged one too.
        vm.Handle(new StepListActionEventArgs(StepListAction.Reorder, vm.Steps[0], targetIndex: 2));
        Assert.Equal(["Wait 20 ms", "Wait 30 ms", "Wait 10 ms"], vm.Steps.Select(step => step.Summary));
        Assert.Equal(0, vm.SelectedStepIndex);
        Assert.Equal([0, 2], vm.OpenStepIndexes);
        Assert.False(vm.Steps[2].IsActive);

        vm.Handle(new StepListActionEventArgs(StepListAction.Reorder, vm.Steps[1], targetIndex: 0));
        Assert.Equal(["Wait 30 ms", "Wait 20 ms", "Wait 10 ms"], vm.Steps.Select(step => step.Summary));
        Assert.Equal(1, vm.SelectedStepIndex);
        Assert.Equal([1, 2], vm.OpenStepIndexes);
    }

    /// <summary>Joel, 2026-10-12: more than one step can be open; selecting one opens nothing, a header click opens or closes its own.</summary>
    [AvaloniaFact]
    public void SelectOpensNothing_AndToggleOpenOpensOrClosesOnlyItsOwnStep()
    {
        var (vm, _, _, _) = Select("Three steps");
        vm.Handle(new StepListActionEventArgs(StepListAction.ToggleOpen, vm.Steps[2]));
        vm.Handle(new StepListActionEventArgs(StepListAction.ToggleOpen, vm.Steps[0]));
        Assert.Equal([0, 2], vm.OpenStepIndexes);

        vm.Handle(new StepListActionEventArgs(StepListAction.Select, vm.Steps[1]));
        Assert.Equal(1, vm.SelectedStepIndex);
        Assert.Equal([0, 2], vm.OpenStepIndexes);

        vm.Handle(new StepListActionEventArgs(StepListAction.ToggleOpen, vm.Steps[2]));

        Assert.Equal(2, vm.SelectedStepIndex);
        Assert.Equal([0], vm.OpenStepIndexes);
        Assert.Equal(3, vm.Steps.Count);
        Assert.False(vm.CanUndo);
    }

    /// <summary>"Change to…" (Joel, 2026-10-11): the step's place and on/off state stay, its settings go, and one undo gives it back.</summary>
    [AvaloniaFact]
    public void ChangeToPutsADefaultStepOfTheTypeInItsPlace_KeepsItsOnOffState_AndOneUndoGivesTheOldStepBack()
    {
        var (vm, store, _, _) = Select("Three steps");
        vm.Handle(new StepListActionEventArgs(StepListAction.ToggleActive, vm.Steps[1]));
        store.ClearHistory();
        vm.Handle(new StepListActionEventArgs(StepListAction.ToggleOpen, vm.Steps[0]));

        vm.Handle(new StepListActionEventArgs(StepListAction.ChangeType, vm.Steps[1], type: WindowOpStepType.Instance));

        var steps = CommandsTestData.Find(store, "Three steps").Steps;
        Assert.Equal(["Wait 10 ms", "Minimize window", "Wait 30 ms"], steps.Select(step => step.Step.Summary));
        Assert.Equal(WindowOpStepType.Instance.CreateDefault(), steps[1].Step);
        Assert.False(steps[1].IsActive);
        Assert.Equal(HostPlatform.Windows, steps[1].AuthoredOn);
        Assert.Equal(1, vm.SelectedStepIndex);
        Assert.Equal([0, 1], vm.OpenStepIndexes);
        Assert.Equal("Window", vm.Steps[1].TypeName);
        Assert.Equal($"Changed step 'Wait 20 ms' to Window. {EditKeymap.Current.Undo} undoes it.", vm.Message);

        vm.Handle(new StepListActionEventArgs(StepListAction.Undo));

        var restored = CommandsTestData.Find(store, "Three steps").Steps;
        Assert.Equal(new DelayStep(20), restored[1].Step);
        Assert.False(restored[1].IsActive);
        Assert.Equal(["Wait 10 ms", "Wait 20 ms", "Wait 30 ms"], vm.Steps.Select(step => step.Summary));
        Assert.False(vm.CanUndo);
    }

    /// <summary>A change Core refuses (judged as a replacement) stores nothing and says why; the header's item greys it for that step.</summary>
    [AvaloniaFact]
    public void ARefusedChangeStoresNothing_TheMessageLineSaysWhy_AndTheHeaderGreysItForThatStep()
    {
        var (vm, store, _, _) = Select("Three steps");
        var notHere = StepOffer.NotHere(RemapStepType.Instance);

        var refusals = vm.SelectedCommand!.ChangeRefusals;
        Assert.Equal(3, refusals.Count);
        Assert.Equal(2, refusals[1].Count);
        Assert.Equal("This step is Delay already.", refusals[1][DelayStepType.Instance]);
        Assert.Equal(notHere, refusals[1][RemapStepType.Instance]);

        vm.Handle(new StepListActionEventArgs(StepListAction.ChangeType, vm.Steps[1], type: DelayStepType.Instance));
        Assert.Equal("This step is Delay already.", vm.Message);
        vm.Handle(new StepListActionEventArgs(StepListAction.ChangeType, vm.Steps[1], type: RemapStepType.Instance));
        Assert.Equal(notHere, vm.Message);

        Assert.Equal(["Wait 10 ms", "Wait 20 ms", "Wait 30 ms"], CommandsTestData.Find(store, "Three steps").Steps.Select(step => step.Step.Summary));
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public void StepActionsNeedASelectedCommandAndSelectionResetsWhenTheCommandChanges()
    {
        var (vm, _, _, _) = CommandsTestData.Create(ViewModels.Commands.CommandsScope.Global);

        vm.Handle(new StepListActionEventArgs(StepListAction.Add, type: MediaKeyStepType.Instance));
        Assert.Equal("Select a command first.", vm.Message);

        var three = CommandsTestData.Item(vm, "Three steps");
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, CommandsTestData.Section(vm, "Uncategorized"), three));
        vm.Handle(new StepListActionEventArgs(StepListAction.ToggleOpen, vm.Steps[0]));
        vm.Handle(new StepListActionEventArgs(StepListAction.ToggleOpen, vm.Steps[2]));
        Assert.Equal(2, vm.SelectedStepIndex);
        Assert.Equal([0, 2], vm.OpenStepIndexes);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, CommandsTestData.Section(vm, "Media"), CommandsTestData.Item(vm, "Volume up")));
        Assert.Equal(-1, vm.SelectedStepIndex);
        Assert.Empty(vm.OpenStepIndexes);
        Assert.Equal("Volume up", Assert.Single(vm.Steps).Summary);
    }

    /// <summary>The Global tab with a Global command selected (its steps are what the step list shows).</summary>
    private static (ViewModels.Commands.CommandsViewModel Vm, Core.Mapping.MappingStore Store, FakeGesturePickerPresenter Picker, FakeFormDialogPresenter Dialogs) Select(string command, FakeConfirmPresenter? confirm = null)
    {
        var created = CommandsTestData.Create(ViewModels.Commands.CommandsScope.Global, confirm);
        var item = CommandsTestData.Item(created.Vm, command);
        created.Vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, created.Vm.Sections.Single(section => section.Id == item.Section), item));
        return created;
    }
}
