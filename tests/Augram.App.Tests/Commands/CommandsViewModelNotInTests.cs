using Augram.App.Components.CommandTree;
using Augram.App.Components.FormDialog;
using Augram.App.Components.SectionForm;
using Augram.App.Components.WindowFinder;
using Augram.App.Declarations;
using Augram.App.Tests.Support;
using Augram.App.ViewModels.Commands;
using Augram.Core.Abstractions;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

/// <summary>
/// "Not in" (Joel, 2026-10-10, plan 0004 step 7): every command not under a hold remap, Global's and an app group's, names the
/// Ignored › Per command entries it is not used over. The header's row names them, Change… opens them by name as a check list
/// with Add app… and its magnifier, and Save stores what is ticked, and the entries Add app… made, as one undo step.
/// </summary>
public sealed class CommandsViewModelNotInTests
{
    private static readonly Point OverSpine = new(2000, 100);

    private static readonly IgnoredApp Spine = PerCommand("Spine", "Spine.exe");
    private static readonly IgnoredApp Eyeris = PerCommand("Eyeris", "Eyeris.exe");
    private static readonly IgnoredApp Krita = PerCommand("Krita", "krita.exe") with { IsActive = false };
    private static readonly IgnoredApp VMware = new(GroupId.New(), "VMware", IsActive: true, new AppMatcher { WindowsProcessNames = ["vmware.exe"] }, DisableEntirely: true);
    private static readonly IgnoredApp[] Ignored = [Spine, Eyeris, Krita, VMware];

    [AvaloniaFact]
    public void NotInIsOnEveryCommandNotUnderAHoldRemap()
    {
        var (global, _, _, _) = Create(CommandsScope.Global, ignored: Ignored);
        Assert.All(global.Sections.SelectMany(section => section.Commands), item => Assert.True(item.CanSetNotIn));
        Assert.Equal(CommandItem.NoneNotIn, Item(global, "Close window").NotInText);

        var (apps, _, _, _) = Create(ignored: Ignored);
        Assert.All(apps.Sections.SelectMany(section => section.Commands), item => Assert.True(item.CanSetNotIn));

        var (blender, _, _) = HoldRemapTestData.CreateBlender();
        var commands = blender.Sections.SelectMany(section => section.Commands).ToList();
        Assert.All(commands.Where(item => item.IsUnderHoldRemap), item => Assert.False(item.CanSetNotIn));
        Assert.Contains(commands, item => !item.IsUnderHoldRemap && item.CanSetNotIn);

        // A command under a hold remap asking anyway opens nothing.
        var dialogs = new FakeFormDialogPresenter();
        var blenderVm = new CommandsViewModel(CommandsScope.Apps, HoldRemapTestData.Store(), new GestureLibrary(StarterGestures.All()), new FakeGesturePickerPresenter(), dialogs, new FakeConfirmPresenter(), new CommandClipboard(), HostPlatform.Windows);
        var orbit = blenderVm.Sections.SelectMany(section => section.Commands).First(item => item.IsUnderHoldRemap);
        blenderVm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditNotIn, command: orbit));
        Assert.Empty(dialogs.Requests);
    }

    [AvaloniaFact]
    public void ChangeListsThePerCommandEntriesByName_TickingOneStoresItsIdAsOneUndoStep()
    {
        var (vm, store, _, dialogs) = Create(CommandsScope.Global, ignored: Ignored);
        IReadOnlyList<(string, string?)>? items = null;
        dialogs.Answer = request =>
        {
            var list = CheckList(request);
            items = [.. list.Items.Select(item => (item.Caption, item.Detail))];
            Tick(list, "Spine");
            return true;
        };

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditNotIn, command: Item(vm, "Close window")));

        // VMware is on Ignored › Global: never offered.
        Assert.Equal([("Eyeris", null), ("Krita", "inactive"), ("Spine", null)], items);
        Assert.Equal((NotInEditViewModel.Title, NotInEditViewModel.ConfirmLabel), (dialogs.Last.Title, dialogs.Last.ConfirmLabel));
        Assert.Equal([Spine.Id], Find(store, "Close window").NotIn);
        Assert.Equal("Spine", Item(vm, "Close window").NotInText);
        Assert.Equal([Spine.Id], Item(vm, "Close window").NotIn);

        Assert.True(vm.CanUndo);
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.Undo));
        Assert.Empty(Find(store, "Close window").NotIn);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public void AnAppGroupsCommand_HasItsNotInToo()
    {
        var (vm, store, _, dialogs) = Create(ignored: Ignored);
        dialogs.Answer = request =>
        {
            Tick(CheckList(request), "Eyeris");
            return true;
        };

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditNotIn, command: Item(vm, "Close tab")));

        Assert.Equal([Eyeris.Id], Find(store, "Close tab").NotIn);
        Assert.Equal("Eyeris", Item(vm, "Close tab").NotInText);
    }

    [AvaloniaFact]
    public void TheDialogStartsOnWhatIsStored_AndTheRowNamesTheEntriesByName()
    {
        var (vm, store, _, dialogs) = Create(CommandsScope.Global, ignored: Ignored);
        dialogs.Answer = request =>
        {
            Tick(CheckList(request), "Spine");
            Tick(CheckList(request), "Eyeris");
            return true;
        };
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditNotIn, command: Item(vm, "Minimize")));
        Assert.Equal("Eyeris, Spine", Item(vm, "Minimize").NotInText);

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
    public void WithNoPerCommandEntries_TheDialogSaysSo()
    {
        var (vm, _, _, dialogs) = Create(CommandsScope.Global, ignored: [VMware]);
        FormDialogRequest? asked = null;
        dialogs.Answer = request =>
        {
            asked = request;
            return false;
        };

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditNotIn, command: Item(vm, "Close window")));

        var fields = asked!.Screen!.Sections.SelectMany(section => section.Fields).ToList();
        Assert.False(fields.OfType<CheckListField>().Single().Visible!.Get());
        var note = fields.OfType<NoteField>().Single(field => field.Label == NotInEditViewModel.ListLabel);
        Assert.Equal(NotInEditViewModel.NoAppsText, note.Text.Get());
        Assert.True(note.Visible!.Get());
        Assert.NotNull(fields.Single(field => field.Label == NotInEditViewModel.AddLabel).Accessory);
    }

    [AvaloniaFact]
    public void CancelChangesNothing()
    {
        var (vm, store, _, dialogs) = Create(CommandsScope.Global, ignored: Ignored);
        var before = store.Current;
        dialogs.Answer = request =>
        {
            Tick(CheckList(request), "Eyeris");
            return false;
        };

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditNotIn, command: Item(vm, "Close window")));

        Assert.Same(before, store.Current);
        Assert.False(vm.CanUndo);
    }

    [AvaloniaFact]
    public void ADeletedEntryLeavesTheRow_AndSoDoesOneMovedToGlobal()
    {
        var (vm, store, _, dialogs) = Create(CommandsScope.Global, ignored: Ignored);
        dialogs.Answer = request =>
        {
            Tick(CheckList(request), "Eyeris");
            Tick(CheckList(request), "Spine");
            return true;
        };
        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditNotIn, command: Item(vm, "Close window")));
        Assert.Equal("Eyeris, Spine", Item(vm, "Close window").NotInText);

        store.RemoveIgnored(Eyeris.Id);
        Assert.Equal("Spine", Item(vm, "Close window").NotInText);

        store.UpdateIgnored(store.FindIgnored(Spine.Id)! with { Scope = IgnoreScope.Global });
        Assert.Equal(CommandItem.NoneNotIn, Item(vm, "Close window").NotInText);
        Assert.Empty(Find(store, "Close window").NotIn);
    }

    /// <summary>Add app…: the magnifier dropped on Spine's window makes a Per command entry, listed and ticked at once; Save stores both as one step.</summary>
    [AvaloniaFact]
    public void AddApp_MakesAPerCommandEntryFromThePickedWindow_TickedAndSavedWithTheCommandAsOneUndoStep()
    {
        var (vm, store, _, dialogs) = Create(CommandsScope.Global, ignored: [Krita, VMware]);
        IReadOnlyList<(string, bool)>? listed = null;
        dialogs.Answer = request =>
        {
            var (window, form) = Show(request.Screen!);
            Drag(window, AddAppFinder(form));
            listed = [.. form.GetVisualDescendants().OfType<CheckBox>().Select(box => (Caption(box), box.IsChecked == true))];
            return true;
        };

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditNotIn, command: Item(vm, "Close window")));

        Assert.Equal([("Krita", false), ("Spine", true)], listed);
        var spine = Assert.Single(store.Current.Ignored, app => app.Name == "Spine");
        Assert.True(spine.IsPerCommand);
        Assert.True(spine.IsActive);
        Assert.Equal(["Spine.exe"], spine.Matcher.WindowsProcessNames);
        Assert.Equal([spine.Id], Find(store, "Close window").NotIn);
        Assert.Equal("Spine", Item(vm, "Close window").NotInText);

        Assert.Single(UndoSteps(vm, store));
        Assert.DoesNotContain(store.Current.Ignored, app => app.Name == "Spine");
        Assert.Empty(Find(store, "Close window").NotIn);
    }

    [AvaloniaFact]
    public void AddApp_TicksAnEntryThatClaimsTheWindowAlready_AndCancelAddsNothing()
    {
        var (vm, store, _, dialogs) = Create(CommandsScope.Global, ignored: Ignored);
        var before = store.Current;
        string? status = null;
        dialogs.Answer = request =>
        {
            var (window, form) = Show(request.Screen!);
            Drag(window, AddAppFinder(form));
            status = form.GetVisualDescendants().OfType<FieldRow>().Single(row => row.Label == NotInEditViewModel.AddLabel).GetVisualDescendants().OfType<TextBlock>().Last().Text;
            return false;
        };

        vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.EditNotIn, command: Item(vm, "Close window")));

        Assert.Equal("'Spine' is on Per command already: ticked.", status);
        Assert.Same(before, store.Current);
    }

    [AvaloniaFact]
    public void TheHeaderShowsTheRowForAnyCommandNotUnderAHoldRemap_AndChangeAsks()
    {
        var (vm, _, _, _) = Create(CommandsScope.Global);
        var actions = new List<CommandTreeActionEventArgs>();
        var header = new CommandHeader { Item = Item(vm, "Close window") with { NotInText = "Eyeris, Spine" } };
        header.ActionRequested += (_, e) => actions.Add(e);
        new Window { Content = header }.Show();

        Assert.True(header.CanSetNotIn);
        Assert.Equal("Eyeris, Spine", header.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PART_NotInText").Text);
        var change = header.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_NotInEdit");
        Assert.True(change.IsEffectivelyVisible);
        change.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

        var asked = Assert.Single(actions);
        Assert.Equal((CommandTreeAction.EditNotIn, "Close window"), (asked.Action, asked.Command!.Name));

        var (apps, _, _, _) = Create();
        header.Item = Item(apps, "Close tab");
        Assert.True(header.CanSetNotIn);

        var (blender, _, _) = HoldRemapTestData.CreateBlender();
        header.Item = blender.Sections.SelectMany(section => section.Commands).First(item => item.IsUnderHoldRemap);
        Assert.False(header.CanSetNotIn);
        Assert.False(change.IsEffectivelyVisible);
    }

    private static IgnoredApp PerCommand(string name, string executable)
        => new(GroupId.New(), name, IsActive: true, new AppMatcher { WindowsProcessNames = [executable] }, DisableEntirely: false) { Scope = IgnoreScope.PerCommand };

    private static CheckListField CheckList(FormDialogRequest request)
        => request.Screen!.Sections.SelectMany(section => section.Fields).OfType<CheckListField>().Single();

    private static void Tick(CheckListField list, string caption)
        => list.Items.Single(item => item.Caption == caption).Value.Set(true);

    private static string Caption(CheckBox box) => box.GetVisualDescendants().OfType<TextBlock>().First().Text ?? string.Empty;

    /// <summary>The dialog's form in a window, with Spine's window on screen outside it for the magnifier.</summary>
    private static (Window Window, SectionForm Form) Show(FormScreen screen)
    {
        var form = new SectionForm { Screen = screen };
        var window = new Window { Width = 700, Height = 900, Content = form };
        window.Show();
        var at = window.PointToScreen(OverSpine);
        window.Resources[WindowFinder.WindowSystemResourceKey] = new FakeWindowSystem().Around(at.X, at.Y, FakeWindowSystem.Window("Spine.exe", "Spine 4.2"));
        return (window, form);
    }

    private static WindowFinder AddAppFinder(SectionForm form)
        => form.GetVisualDescendants().OfType<FieldRow>().Single(row => row.Label == NotInEditViewModel.AddLabel).GetVisualDescendants().OfType<WindowFinder>().Single();

    /// <summary>Left press on the magnifier, a move onto Spine outside the window, release there.</summary>
    private static void Drag(Window window, WindowFinder finder)
    {
        window.MouseDown(finder.TranslatePoint(new Point(finder.Bounds.Width / 2, finder.Bounds.Height / 2), window)!.Value, MouseButton.Left);
        window.MouseMove(OverSpine);
        window.MouseUp(OverSpine, MouseButton.Left);
    }

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
