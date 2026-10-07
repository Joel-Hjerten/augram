using Augram.App.Components.CommandTree;
using Augram.App.ViewModels.Commands;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xunit;
using static Augram.App.Tests.Commands.CommandsTestData;

namespace Augram.App.Tests.Commands;

public sealed class CommandTreeTests
{
    [AvaloniaFact]
    public void ShowsAHeaderPerSectionTheCommandsOfExpandedSectionsAndWhatEachHeaderOffers()
    {
        var (tree, _, _) = Show(CommandsScope.Global, collapse: "Window");

        var rows = tree.Rows;
        Assert.Equal(["Uncategorized", "Media", "Window"], rows.OfType<SectionRow>().Select(row => row.NameText));
        Assert.Equal(["Three steps", "Volume up"], rows.OfType<CommandRow>().Select(row => row.NameText));
        Assert.Equal("2 commands", rows.OfType<SectionRow>().Last().CountText);
        var uncategorized = rows.OfType<SectionRow>().First();
        Assert.Contains(":pinned", uncategorized.Classes);
        Assert.False(uncategorized.CanRename);
        Assert.False(uncategorized.CanToggleActive);
        Assert.False(uncategorized.GetVisualDescendants().OfType<CheckBox>().Single().IsVisible);
        Assert.DoesNotContain(":pinned", rows.OfType<SectionRow>().Last().Classes);
        Assert.Contains(":collapsed", rows.OfType<SectionRow>().Last().Classes);

        var volume = rows.OfType<CommandRow>().Last();
        Assert.False(volume.HasGlyph);
        Assert.Equal("Wheel up", volume.TriggerText);
        Assert.False(volume.HasCategory);

        var (apps, _, _) = Show();
        var chrome = apps.Rows.OfType<SectionRow>().Single(row => row.NameText == "Chrome");
        Assert.True(chrome.CanToggleActive);
        Assert.True(chrome.GetVisualDescendants().OfType<CheckBox>().Single().IsVisible);
        var brush = apps.Rows.OfType<CommandRow>().Single(row => row.NameText == "Brush");
        Assert.True(brush.HasGlyph);
        Assert.True(brush.HasCategory);
        Assert.Equal("General", brush.CategoryText);
        Assert.False(apps.Rows.OfType<CommandRow>().Single(row => row.NameText == "Plain").HasCategory);
    }

    [AvaloniaFact]
    public void ToolbarButtonsCarryTheHostsWordsAndRaiseTheirActions()
    {
        var (tree, actions, _) = Show();
        var buttons = tree.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("toolbar")).ToDictionary(button => (string)button.Content!);

        Click(buttons["New group…"]);
        Click(buttons["New command"]);
        Click(buttons["Undo"]);
        Click(buttons["Redo"]);
        Click(tree.Rows.OfType<SectionRow>().Last().GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("expander")));

        Assert.Equal([CommandTreeAction.NewSection, CommandTreeAction.NewCommand, CommandTreeAction.Undo, CommandTreeAction.Redo, CommandTreeAction.ToggleExpanded], actions.Select(action => action.Action));
        Assert.Equal("Photoshop", actions[^1].Section!.Name);

        var (global, _, _) = Show(CommandsScope.Global);
        Assert.Contains(global.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, "New category…"));
        Assert.Contains(global.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Global commands");
    }

    [AvaloniaFact]
    public void SelectingARowRaisesSelectAndTheHostsSelectionIsAppliedWithoutAnEcho()
    {
        var (tree, actions, vm) = Show();
        var list = tree.GetVisualDescendants().OfType<ListBox>().Single();
        var closeTab = tree.Rows.OfType<CommandRow>().Single(row => row.NameText == "Close tab");

        list.SelectedItem = closeTab;

        var select = Assert.Single(actions);
        Assert.Equal(CommandTreeAction.Select, select.Action);
        Assert.Equal("Chrome", select.Section!.Name);
        Assert.Equal("Close tab", select.Command!.Name);

        list.SelectedItem = tree.Rows.OfType<SectionRow>().Single(row => row.NameText == "Apple");
        Assert.Equal("Apple", actions[^1].Section!.Name);
        Assert.Null(actions[^1].Command);

        actions.Clear();
        tree.SelectedSectionId = Section(vm, "Chrome").Id;
        tree.SelectedCommandId = Item(vm, "Nothing on Up").Id;
        Assert.Equal("Nothing on Up", tree.SelectedCommand!.Name);
        Assert.Equal("Chrome", tree.SelectedSection!.Name);
        Assert.Empty(actions);

        tree.Sections = vm.Sections.ToList();
        Assert.Equal("Nothing on Up", tree.SelectedCommand!.Name);
        Assert.Empty(actions);
    }

    [AvaloniaFact]
    public void RenameEditsInPlaceAndKeyBindingsStandDownMeanwhile()
    {
        var (tree, actions, vm) = Show();
        var window = (Window)TopLevel.GetTopLevel(tree)!;
        tree.SelectedSectionId = Section(vm, "Chrome").Id;
        tree.SelectedCommandId = Item(vm, "Close tab").Id;
        FocusSelectedRow(tree);

        window.KeyPressQwerty(CommandsKeymap.Current.Rename == "F2" ? PhysicalKey.F2 : PhysicalKey.Enter, RawInputModifiers.None);

        Assert.True(tree.IsEditing);
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Assert.Empty(actions);

        var row = tree.Rows.OfType<CommandRow>().Single(candidate => candidate.NameText == "Close tab");
        var editor = row.GetVisualDescendants().OfType<TextBox>().Single();
        editor.Text = " Close it ";
        editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

        var rename = Assert.Single(actions);
        Assert.Equal(CommandTreeAction.Rename, rename.Action);
        Assert.Equal("Close it", rename.Name);
        Assert.Equal("Close tab", rename.Command!.Name);
        Assert.Equal("Chrome", rename.Section!.Name);
        Assert.False(tree.IsEditing);
    }

    [AvaloniaFact]
    public void ClickingAwayFromTheRenameEditorKeepsTheNameAndEscapeStillReverts()
    {
        var (tree, actions, vm) = Show();
        var window = (Window)TopLevel.GetTopLevel(tree)!;
        tree.SelectedSectionId = Section(vm, "Chrome").Id;
        tree.SelectedCommandId = Item(vm, "Close tab").Id;
        tree.BeginRename(Item(vm, "Close tab").Id);
        var editor = tree.Rows.OfType<CommandRow>().Single(row => row.NameText == "Close tab").GetVisualDescendants().OfType<TextBox>().Single();
        editor.Text = "Close it";

        Views.ClickAwayFocus.Register();
        var heading = tree.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == vm.Heading);
        ClickAt(window, heading);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(tree.IsEditing);
        var rename = Assert.Single(actions, action => action.Action == CommandTreeAction.Rename);
        Assert.Equal("Close it", rename.Name);

        actions.Clear();
        tree.BeginRename(Item(vm, "Close tab").Id);
        editor.Text = "Not this";
        editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        ClickAt(window, heading);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(actions, action => action.Action == CommandTreeAction.Rename);
    }

    [AvaloniaFact]
    public void ARowRenamedBeforeItHasATemplateStartsTheEditorWhenItGetsOne()
    {
        var (tree, _, vm) = Show(CommandsScope.Global);
        var window = (Window)TopLevel.GetTopLevel(tree)!;

        // A store change rebuilds every row; the view model asks for the rename right after (New category).
        tree.Sections = vm.Sections.ToList();
        tree.BeginRename(Section(vm, "Media").Id);
        window.UpdateLayout();

        var row = tree.Rows.OfType<SectionRow>().Single(candidate => candidate.NameText == "Media");
        Assert.True(row.IsEditing);
        Assert.Equal("Media", row.GetVisualDescendants().OfType<TextBox>().Single().Text);
        Assert.Same(row, tree.GetVisualDescendants().OfType<ListBox>().Single().SelectedItem);
    }

    [AvaloniaFact]
    public void DeleteKeyCarriesTheCommandOrOnlyTheSectionWhenItAllowsAndTheCheckBoxTogglesActive()
    {
        var (tree, actions, vm) = Show();
        var window = (Window)TopLevel.GetTopLevel(tree)!;
        var modifier = CommandsKeymap.Current.IsMacOS ? RawInputModifiers.Meta : RawInputModifiers.Control;
        tree.SelectedSectionId = Section(vm, "Chrome").Id;
        tree.SelectedCommandId = Item(vm, "Close tab").Id;
        FocusSelectedRow(tree);

        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.C, modifier);
        window.KeyPressQwerty(PhysicalKey.V, modifier);
        window.KeyPressQwerty(PhysicalKey.N, modifier);

        Assert.Equal([CommandTreeAction.Delete, CommandTreeAction.Copy, CommandTreeAction.Paste, CommandTreeAction.NewCommand], actions.Select(action => action.Action));
        Assert.All(actions, action => Assert.Equal("Close tab", action.Command!.Name));

        actions.Clear();
        tree.SelectedCommandId = null;
        tree.SelectedSectionId = Section(vm, "Chrome").Id;
        FocusSelectedRow(tree);
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);

        var delete = Assert.Single(actions);
        Assert.Equal("Chrome", delete.Section!.Name);
        Assert.Null(delete.Command);

        actions.Clear();
        tree.Rows.OfType<CommandRow>().First().GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = false;
        var toggle = Assert.Single(actions);
        Assert.Equal(CommandTreeAction.ToggleActive, toggle.Action);
        Assert.Equal("Close tab", toggle.Command!.Name);

        var (global, globalActions, globalVm) = Show(CommandsScope.Global);
        var globalWindow = (Window)TopLevel.GetTopLevel(global)!;
        global.SelectedSectionId = SectionId.Uncategorized;
        FocusSelectedRow(global);
        globalWindow.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Assert.Empty(globalActions);

        global.SelectedSectionId = Section(globalVm, "Media").Id;
        FocusSelectedRow(global);
        globalWindow.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Assert.Equal("Media", Assert.Single(globalActions).Section!.Name);
    }

    [AvaloniaFact]
    public void ClickingAnywhereOnAHeaderTogglesItAndSelectsIt_TheActiveBoxStaysItsOwn()
    {
        var (tree, actions, _) = Show();
        var window = (Window)TopLevel.GetTopLevel(tree)!;
        var chrome = tree.Rows.OfType<SectionRow>().Single(row => row.NameText == "Chrome");
        var name = chrome.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "Chrome");

        ClickAt(window, name);

        Assert.Contains(actions, action => action is { Action: CommandTreeAction.Select, Section.Name: "Chrome", Command: null });
        Assert.Equal("Chrome", Assert.Single(actions, action => action.Action == CommandTreeAction.ToggleExpanded).Section!.Name);

        actions.Clear();
        var active = tree.Rows.OfType<SectionRow>().Single(row => row.NameText == "Chrome").GetVisualDescendants().OfType<CheckBox>().Single();
        ClickAt(window, active);

        Assert.DoesNotContain(actions, action => action.Action == CommandTreeAction.ToggleExpanded);
        Assert.Contains(actions, action => action.Action == CommandTreeAction.ToggleActive);

        actions.Clear();
        var expander = tree.Rows.OfType<SectionRow>().Single(row => row.NameText == "Chrome").GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("expander"));
        ClickAt(window, expander);

        Assert.Single(actions, action => action.Action == CommandTreeAction.ToggleExpanded);
    }

    private static void ClickAt(Window window, Visual target)
    {
        var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    private static void Click(Button button) => button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    private static void FocusSelectedRow(CommandTree tree)
    {
        var list = tree.GetVisualDescendants().OfType<ListBox>().Single();
        Assert.True(list.ContainerFromItem(list.SelectedItem!)!.Focus());
    }

    /// <summary>Sections start collapsed; the tree tests need commands on screen, so every section but <paramref name="collapse"/> is expanded first.</summary>
    private static (CommandTree Tree, List<CommandTreeActionEventArgs> Actions, CommandsViewModel Vm) Show(CommandsScope scope = CommandsScope.Apps, string? collapse = null)
    {
        var (vm, _, _, _) = Create(scope);
        foreach (var name in Names(vm).Where(name => name != collapse).ToList())
        {
            vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleExpanded, Section(vm, name)));
        }

        var actions = new List<CommandTreeActionEventArgs>();
        var tree = new CommandTree { Sections = vm.Sections, Heading = vm.Heading, NewSectionLabel = vm.NewSectionLabel, HelpText = vm.Help };
        tree.ActionRequested += (_, e) => actions.Add(e);
        var window = new Window { Content = tree, Width = 800, Height = 600 };
        window.Show();
        return (tree, actions, vm);
    }
}
