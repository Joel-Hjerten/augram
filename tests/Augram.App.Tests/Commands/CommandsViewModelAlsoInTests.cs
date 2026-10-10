using Augram.App.Components.CommandTree;
using Augram.App.Components.FormDialog;
using Augram.App.Declarations;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Augram.Core.Steps.Delay;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

/// <summary>
/// "Also in" (Joel, 2026-10-10, plan 0005 decision 7): a command whose trigger holds no stroke button (a button trigger, Right +
/// wheel) names the Exclusions › Global entries it still works over. The header's row names them, Change… opens the Global
/// entries without the disable-while-focused mode by name as a check list, and Save stores what is ticked as one undo step.
/// </summary>
public sealed class CommandsViewModelAlsoInTests
{
    private static readonly IgnoredApp Blender = Excluded("Blender", "blender.exe");
    private static readonly IgnoredApp Resolve = Excluded("DaVinci Resolve", "Resolve.exe");
    private static readonly IgnoredApp Game = Excluded("A Plague Tale", "APlagueTaleRequiem_x64.exe") with { IsActive = false };
    private static readonly IgnoredApp VMware = new(GroupId.New(), "VMware", IsActive: true, new AppMatcher { WindowsProcessNames = ["vmware.exe"] }, DisableEntirely: true);
    private static readonly IgnoredApp Spine = new(GroupId.New(), "Spine", IsActive: true, new AppMatcher { WindowsProcessNames = ["Spine.exe"] }, DisableEntirely: false) { Scope = IgnoreScope.PerCommand };
    private static readonly IgnoredApp[] Ignored = [Blender, Resolve, Game, VMware, Spine];

    private static readonly TriggerHold Right = new(HeldButtons.Right);

    [AvaloniaFact]
    public void AlsoInIsOnlyOnATriggerThatHoldsNoStrokeButton_NeverUnderAHoldRemap()
    {
        var (vm, _, _) = GlobalWithChords();

        Assert.Equal((true, true, false, false), (Item(vm, "Magnifier").CanSetAlsoIn, Item(vm, "Zoom in").CanSetAlsoIn, Item(vm, "Close window").CanSetAlsoIn, Item(vm, "Volume up").CanSetAlsoIn));
        Assert.Equal(CommandItem.NoneNotIn, Item(vm, "Magnifier").AlsoInText);

        var (blender, _, _) = HoldRemapTestData.CreateBlender();
        Assert.All(blender.Sections.SelectMany(section => section.Commands), item => Assert.False(item.CanSetAlsoIn));
    }

    [AvaloniaFact]
    public void ChangeListsThePlainGlobalEntriesByName_TickingOneStoresItsIdAsOneUndoStep()
    {
        var (vm, store, dialogs) = GlobalWithChords();
        IReadOnlyList<(string, string?)>? items = null;
        dialogs.Answer = request =>
        {
            var list = CheckList(request);
            items = [.. list.Items.Select(item => (item.Caption, item.Detail))];
            Tick(list, "Blender");
            return true;
        };

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditAlsoIn, command: Item(vm, "Magnifier")));

        // VMware disables Augram while focused and Spine is on Per command: never offered.
        Assert.Equal([("A Plague Tale", "inactive"), ("Blender", null), ("DaVinci Resolve", null)], items);
        Assert.Equal((AlsoInEditViewModel.Title, AlsoInEditViewModel.ConfirmLabel), (dialogs.Last.Title, dialogs.Last.ConfirmLabel));
        Assert.Equal("Where 'Magnifier' still works", dialogs.Last.Screen!.Sections.Single().Title);
        Assert.Equal([Blender.Id], CommandsTestData.Find(store, "Magnifier").AlsoIn);
        Assert.Equal("Blender", Item(vm, "Magnifier").AlsoInText);

        Assert.True(vm.CanUndo);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.Empty(CommandsTestData.Find(store, "Magnifier").AlsoIn);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public void TheDialogStartsOnWhatIsStored_AndUnchangedOrCancelledStoresNothing()
    {
        var (vm, store, dialogs) = GlobalWithChords();
        store.UpdateCommand(GroupId.Global, CommandsTestData.Find(store, "Zoom in") with { AlsoIn = [Resolve.Id, Blender.Id] });
        store.ClearHistory();
        Assert.Equal("Blender, DaVinci Resolve", Item(vm, "Zoom in").AlsoInText);
        var before = store.Current;

        bool[]? ticks = null;
        dialogs.Answer = request =>
        {
            ticks = [.. CheckList(request).Items.Select(item => item.Value.Get())];
            return true;
        };
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditAlsoIn, command: Item(vm, "Zoom in")));
        Assert.Equal([false, true, true], ticks!);
        Assert.Same(before, store.Current);

        dialogs.Answer = request =>
        {
            Tick(CheckList(request), "A Plague Tale");
            return false;
        };
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditAlsoIn, command: Item(vm, "Zoom in")));
        Assert.Same(before, store.Current);
        Assert.False(store.CanUndo);
    }

    [AvaloniaFact]
    public void WithNoPlainGlobalEntry_TheDialogSaysSo_AndAGestureAskingOpensNothing()
    {
        var (vm, _, dialogs) = GlobalWithChords(ignored: [VMware, Spine]);
        FormDialogRequest? asked = null;
        dialogs.Answer = request =>
        {
            asked = request;
            return false;
        };

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditAlsoIn, command: Item(vm, "Magnifier")));

        var note = Assert.IsType<NoteField>(asked!.Screen!.Sections.Single().Fields.Single());
        Assert.Equal((AlsoInEditViewModel.ListLabel, AlsoInEditViewModel.NoAppsText), (note.Label, note.Text.Get()));

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditAlsoIn, command: Item(vm, "Close window")));
        Assert.Single(dialogs.Requests);
    }

    [AvaloniaFact]
    public void AnEntryDeletedOrSwitchedToDisableWhileFocused_LeavesTheRow()
    {
        var (vm, store, _) = GlobalWithChords();
        store.UpdateCommand(GroupId.Global, CommandsTestData.Find(store, "Magnifier") with { AlsoIn = [Blender.Id, Resolve.Id] });
        Assert.Equal("Blender, DaVinci Resolve", Item(vm, "Magnifier").AlsoInText);

        store.RemoveIgnored(Resolve.Id);
        Assert.Equal("Blender", Item(vm, "Magnifier").AlsoInText);

        store.UpdateIgnored(store.FindIgnored(Blender.Id)! with { DisableEntirely = true });
        Assert.Equal(CommandItem.NoneNotIn, Item(vm, "Magnifier").AlsoInText);
        Assert.Empty(CommandsTestData.Find(store, "Magnifier").AlsoIn);
    }

    [AvaloniaFact]
    public void ATriggerThatHoldsTheStrokeButton_ClearsTheAlsoIn_AndTheMessageSaysSo_InTheSameUndoStep()
    {
        var (vm, store, _) = GlobalWithChords();
        store.UpdateCommand(GroupId.Global, CommandsTestData.Find(store, "Zoom in") with { AlsoIn = [Blender.Id] });
        store.ClearHistory();
        Select(vm, "Zoom in");

        // Ticking the stroke button: Stroke + Right + wheel up, which an excluded app's stroke button would have to draw.
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetTriggerHold, command: vm.SelectedCommand, hold: new TriggerHold(HeldButtons.Stroke | HeldButtons.Right)));

        Assert.Empty(CommandsTestData.Find(store, "Zoom in").AlsoIn);
        Assert.False(vm.SelectedCommand!.CanSetAlsoIn);
        Assert.Equal($"Cleared the Also in of 'Zoom in': only a trigger that holds a button other than the stroke button works over an excluded app. {CommandsKeymap.Current.Undo} undoes it.", vm.Message);

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.Equal([Blender.Id], CommandsTestData.Find(store, "Zoom in").AlsoIn);
        Assert.False(store.CanUndo);
    }

    [AvaloniaFact]
    public void TheHeaderShowsTheRowForACommandWithoutTheStrokeButton_AndChangeAsks()
    {
        var (vm, _, _) = GlobalWithChords();
        var actions = new List<CommandTreeActionEventArgs>();
        var header = new CommandHeader { Item = Item(vm, "Magnifier") with { AlsoInText = "Blender" } };
        header.ActionRequested += (_, e) => actions.Add(e);
        new Window { Content = header, Width = 900 }.Show();

        Assert.True(header.CanSetAlsoIn);
        Assert.Equal("Blender", header.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PART_AlsoInText").Text);
        var change = header.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_AlsoInEdit");
        Assert.True(change.IsEffectivelyVisible);
        change.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        var asked = Assert.Single(actions);
        Assert.Equal((CommandTreeAction.EditAlsoIn, "Magnifier"), (asked.Action, asked.Command!.Name));

        header.Item = Item(vm, "Close window");
        Assert.False(header.CanSetAlsoIn);
        Assert.False(change.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void TheRowFollowsTheStoredTrigger_AndHidesWhereTheHeldButtonIsThisMachinesStrokeButton()
    {
        var (vm, _, _) = GlobalWithChords();
        Select(vm, "Zoom in");
        Assert.True(vm.SelectedCommand!.CanSetAlsoIn);

        // A draft holding the stroke button changes nothing stored: the row stays.
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.SetTriggerHold, command: vm.SelectedCommand, hold: new TriggerHold(HeldButtons.None)));
        Assert.NotNull(vm.SelectedCommand!.DraftNote);
        Assert.True(vm.SelectedCommand.CanSetAlsoIn);

        Select(vm, "Magnifier");
        vm.StrokeButton = MouseButton.Right;
        Assert.False(vm.SelectedCommand!.CanSetAlsoIn);
        vm.StrokeButton = MouseButton.Middle;
        Assert.True(vm.SelectedCommand!.CanSetAlsoIn);
    }

    /// <summary>
    /// The test mapping's Global group with Magnifier (Right + Left) and Zoom in (Right + wheel up) in Media, beside its gesture and
    /// stroke-button wheel commands; <paramref name="ignored"/> (by default Blender, DaVinci Resolve and an inactive game on
    /// Exclusions › Global, VMware disabling Augram while focused, and Spine on Per command); no undo history.
    /// </summary>
    private static (CommandsViewModel Vm, MappingStore Store, FakeFormDialogPresenter Dialogs) GlobalWithChords(IgnoredApp[]? ignored = null)
    {
        var (vm, store, _, dialogs) = Create(CommandsScope.Global, ignored: ignored ?? Ignored);
        var media = Category(store, "Media").Id;
        store.AddCommand(GroupId.Global, Cmd("Magnifier", Trigger.ForButton(MouseButton.Left, Right)) with { CategoryId = media });
        store.AddCommand(GroupId.Global, Cmd("Zoom in", Trigger.ForWheel(WheelDirection.Up, Right)) with { CategoryId = media });
        store.ClearHistory();
        return (vm, store, dialogs);
    }

    private static Command Cmd(string name, Trigger trigger)
        => new(CommandId.New(), name, trigger, IsActive: true, [new CommandStep(new DelayStep(10), HostPlatform.Windows)]);

    private static IgnoredApp Excluded(string name, string executable)
        => new(GroupId.New(), name, IsActive: true, new AppMatcher { WindowsProcessNames = [executable] }, DisableEntirely: false);

    private static void Select(CommandsViewModel vm, string name)
    {
        var item = Item(vm, name);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Select, vm.Sections.Single(section => section.Id == item.Section), item));
    }

    private static CheckListField CheckList(FormDialogRequest request)
        => request.Screen!.Sections.SelectMany(section => section.Fields).OfType<CheckListField>().Single();

    private static void Tick(CheckListField list, string caption)
        => list.Items.Single(item => item.Caption == caption).Value.Set(true);
}
