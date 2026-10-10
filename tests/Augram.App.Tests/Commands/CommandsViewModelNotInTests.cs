using Augram.App.Components.CommandTree;
using Augram.App.Components.FormDialog;
using Augram.App.Declarations;
using Augram.App.ViewModels.Commands;
using Augram.Core.Mapping;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

/// <summary>
/// "Not in" on a Global command (Joel, 2026-10-10, plan 0004 step 3): the header's row names the app groups it is not used in,
/// Change… opens the app groups by name as a check list in the form dialog, and Save stores what is ticked as one undo step.
/// App group commands and commands under a hold remap have no such row.
/// </summary>
public sealed class CommandsViewModelNotInTests
{
    [AvaloniaFact]
    public void NotInIsOnGlobalCommandsOnly()
    {
        var (global, _, _, _) = Create(CommandsScope.Global);
        Assert.All(global.Sections.SelectMany(section => section.Commands), item => Assert.True(item.CanSetNotIn));
        Assert.Equal(CommandItem.NoneNotIn, Item(global, "Close window").NotInText);

        var (apps, _, _, _) = Create();
        Assert.All(apps.Sections.SelectMany(section => section.Commands), item => Assert.False(item.CanSetNotIn));

        var (blender, _, _) = HoldRemapTestData.CreateBlender();
        Assert.All(blender.Sections.SelectMany(section => section.Commands), item => Assert.False(item.CanSetNotIn));

        // An app command asking anyway opens nothing.
        var (appsVm, _, _, dialogs) = Create();
        appsVm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditNotIn, command: Item(appsVm, "Close tab")));
        Assert.Empty(dialogs.Requests);
    }

    [AvaloniaFact]
    public void ChangeListsTheAppGroupsByName_TickingOneStoresItsIdAsOneUndoStep()
    {
        var (vm, store, _, dialogs) = Create(CommandsScope.Global);
        IReadOnlyList<string>? captions = null;
        dialogs.Answer = request =>
        {
            var list = CheckList(request);
            captions = [.. list.Items.Select(item => item.Caption)];
            Tick(list, "Photoshop");
            return true;
        };

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditNotIn, command: Item(vm, "Close window")));

        Assert.Equal(["Apple", "Chrome", "Photoshop"], captions);
        Assert.Equal((NotInEditViewModel.Title, NotInEditViewModel.ConfirmLabel), (dialogs.Last.Title, dialogs.Last.ConfirmLabel));
        Assert.Equal([Group(store, "Photoshop").Id], Find(store, "Close window").NotIn);
        Assert.Equal("Photoshop", Item(vm, "Close window").NotInText);
        Assert.Equal([Group(store, "Photoshop").Id], Item(vm, "Close window").NotIn);

        Assert.True(vm.CanUndo);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.Empty(Find(store, "Close window").NotIn);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public void TheDialogStartsOnWhatIsStored_AndTheRowNamesTheGroupsByName()
    {
        var (vm, store, _, dialogs) = Create(CommandsScope.Global);
        dialogs.Answer = request =>
        {
            Tick(CheckList(request), "Photoshop");
            Tick(CheckList(request), "Apple");
            return true;
        };
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditNotIn, command: Item(vm, "Minimize")));
        Assert.Equal("Apple, Photoshop", Item(vm, "Minimize").NotInText);

        bool[]? ticks = null;
        dialogs.Answer = request =>
        {
            ticks = [.. CheckList(request).Items.Select(item => item.Value.Get())];
            return true;
        };
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditNotIn, command: Item(vm, "Minimize")));

        Assert.Equal([true, false, true], ticks!);
        Assert.Single(UndoSteps(vm, store));
    }

    [AvaloniaFact]
    public void CancelChangesNothing()
    {
        var (vm, store, _, dialogs) = Create(CommandsScope.Global);
        var before = store.Current;
        dialogs.Answer = request =>
        {
            Tick(CheckList(request), "Chrome");
            return false;
        };

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditNotIn, command: Item(vm, "Close window")));

        Assert.Same(before, store.Current);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public void ADeletedGroupLeavesTheRow()
    {
        var (vm, store, _, dialogs) = Create(CommandsScope.Global);
        dialogs.Answer = request =>
        {
            Tick(CheckList(request), "Chrome");
            return true;
        };
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditNotIn, command: Item(vm, "Close window")));
        Assert.Equal("Chrome", Item(vm, "Close window").NotInText);

        store.RemoveGroup(Group(store, "Chrome").Id);

        Assert.Equal(CommandItem.NoneNotIn, Item(vm, "Close window").NotInText);
        Assert.Empty(Find(store, "Close window").NotIn);
    }

    [AvaloniaFact]
    public void TheHeaderShowsTheRowForAGlobalCommand_AndChangeAsks()
    {
        var (vm, _, _, _) = Create(CommandsScope.Global);
        var actions = new List<CommandTreeActionEventArgs>();
        var header = new CommandHeader { Item = Item(vm, "Close window") with { NotInText = "Apple, Photoshop" } };
        header.ActionRequested += (_, e) => actions.Add(e);
        new Window { Content = header }.Show();

        Assert.True(header.CanSetNotIn);
        Assert.Equal("Apple, Photoshop", header.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PART_NotInText").Text);
        var change = header.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_NotInEdit");
        Assert.True(change.IsEffectivelyVisible);
        change.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        var asked = Assert.Single(actions);
        Assert.Equal((CommandTreeAction.EditNotIn, "Close window"), (asked.Action, asked.Command!.Name));

        var (apps, _, _, _) = Create();
        header.Item = Item(apps, "Close tab");
        Assert.False(header.CanSetNotIn);
        Assert.False(change.IsEffectivelyVisible);
    }

    private static CheckListField CheckList(FormDialogRequest request)
        => request.Screen!.Sections.SelectMany(section => section.Fields).OfType<CheckListField>().Single();

    private static void Tick(CheckListField list, string caption)
        => list.Items.Single(item => item.Caption == caption).Value.Set(true);

    private static List<MappingDocument> UndoSteps(CommandsViewModel vm, MappingStore store)
    {
        var steps = new List<MappingDocument>();
        while (vm.CanUndo)
        {
            vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
            steps.Add(store.Current);
        }

        return steps;
    }
}
