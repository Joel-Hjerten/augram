using Augram.App.Components.CommandTree;
using Augram.App.ViewModels.Commands;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
        var buttons = tree.GetVisualDescendants().OfType<Button>()
            .Where(button => button.Classes.Contains("toolbar") && button.FindAncestorOfType<SectionRow>() is null)
            .ToDictionary(button => (string)button.Content!);

        Click(buttons["New group"]);
        Click(buttons["Show other platforms"]);
        Click(tree.Rows.OfType<SectionRow>().Last().GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("expander")));

        Assert.Equal([CommandTreeAction.NewSection, CommandTreeAction.ToggleOtherPlatforms, CommandTreeAction.ToggleExpanded], actions.Select(action => action.Action));

        // New command left the toolbar (Joel, 2026-10-11): it sits on the targeted section's header.
        Assert.Equal(["New group", "Show other platforms"], buttons.Keys);
        Assert.Equal("Photoshop", actions[^1].Section!.Name);

        var (global, _, _) = Show(CommandsScope.Global);
        Assert.Contains(global.GetVisualDescendants().OfType<Button>(), button => Equals(button.Content, "New category"));
        Assert.Contains(global.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Global commands");
        Assert.True(global.GetVisualDescendants().OfType<ToggleButton>().Single(button => button.Name == "PART_OtherPlatforms").IsVisible);
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

        // Uncategorized deletes too since Joel's 2026-10-11 report (its commands; the host asks first).
        var (global, globalActions, globalVm) = Show(CommandsScope.Global);
        var globalWindow = (Window)TopLevel.GetTopLevel(global)!;
        global.SelectedSectionId = SectionId.Uncategorized;
        FocusSelectedRow(global);
        globalWindow.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Assert.Equal(SectionId.Uncategorized, Assert.Single(globalActions).Section!.Id);

        globalActions.Clear();
        global.SelectedSectionId = Section(globalVm, "Media").Id;
        FocusSelectedRow(global);
        globalWindow.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Assert.Equal("Media", Assert.Single(globalActions).Section!.Name);
    }

    [AvaloniaFact]
    public void NewCommandShowsOnTheTargetedSectionsHeaderOnly_NoneWithNothingSelected()
    {
        var (tree, _, vm) = Show();
        var window = (Window)TopLevel.GetTopLevel(tree)!;

        Assert.Empty(ShowingNewCommand(tree));
        Assert.All(tree.Rows.OfType<SectionRow>(), row => Assert.False(NewCommandButton(row).IsVisible));
        var chrome = tree.Rows.OfType<SectionRow>().Single(row => row.NameText == "Chrome");
        var height = chrome.Bounds.Height;

        tree.SelectedSectionId = Section(vm, "Chrome").Id;
        window.UpdateLayout();
        Assert.Equal(["Chrome"], ShowingNewCommand(tree));
        Assert.True(NewCommandButton(chrome).IsVisible);

        // Inside the row line at the row's control size (the theme fixes its height, as the expander's), so the header is no
        // taller with it (the Row rule). Headless text is small, so the fixed height is what proves it, not the bounds alone.
        Assert.Equal(Application.Current!.FindResource("Row.ControlSize"), NewCommandButton(chrome).Height);
        Assert.Equal(height, chrome.Bounds.Height);

        // A selected command's section, which a rebuild keeps.
        tree.SelectedSectionId = Section(vm, "Photoshop").Id;
        tree.SelectedCommandId = Item(vm, "Brush").Id;
        Assert.Equal(["Photoshop"], ShowingNewCommand(tree));
        tree.Sections = vm.Sections.ToList();
        window.UpdateLayout();
        Assert.Equal(["Photoshop"], ShowingNewCommand(tree));
        Assert.Equal(["Photoshop"], tree.Rows.OfType<SectionRow>().Where(row => NewCommandButton(row).IsVisible).Select(row => row.NameText));

        tree.SelectedCommandId = null;
        tree.SelectedSectionId = null;
        Assert.Empty(ShowingNewCommand(tree));

        // The context menu's New command needs a section too.
        var menu = tree.GetVisualDescendants().OfType<ListBox>().Single().ContextMenu!;
        var entry = menu.Items.OfType<MenuItem>().Single(item => Equals(item.Tag, CommandTreeAction.NewCommand));
        CommandTreeMenu.Refresh(menu, tree.SelectedSection, tree.SelectedCommand, vm.NewSectionLabel);
        Assert.False(entry.IsEnabled);
        tree.SelectedSectionId = Section(vm, "Chrome").Id;
        CommandTreeMenu.Refresh(menu, tree.SelectedSection, tree.SelectedCommand, vm.NewSectionLabel);
        Assert.True(entry.IsEnabled);
    }

    [AvaloniaFact]
    public void ClickingTheHeadersNewCommandRaisesItForThatSection_NeitherAToggleNorARename()
    {
        var (tree, actions, vm) = Show();
        var window = (Window)TopLevel.GetTopLevel(tree)!;
        tree.SelectedSectionId = Section(vm, "Chrome").Id;
        window.UpdateLayout();
        var chrome = tree.Rows.OfType<SectionRow>().Single(row => row.NameText == "Chrome");

        ClickAt(window, NewCommandButton(chrome));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var request = Assert.Single(actions, action => action.Action == CommandTreeAction.NewCommand);
        Assert.Equal("Chrome", request.Section!.Name);
        Assert.Null(request.Command);
        Assert.DoesNotContain(actions, action => action.Action is CommandTreeAction.ToggleExpanded or CommandTreeAction.Select);

        actions.Clear();
        DoubleClickAt(window, NewCommandButton(chrome));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.All(actions, action => Assert.Equal(CommandTreeAction.NewCommand, action.Action));
        Assert.False(chrome.IsEditing);
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

    [AvaloniaFact]
    public void DoubleClickingARowRenamesIt_AHeaderEndsExpandedOrCollapsedAsItWas()
    {
        var (tree, actions, _) = Show();
        var window = (Window)TopLevel.GetTopLevel(tree)!;
        var closeTab = tree.Rows.OfType<CommandRow>().Single(row => row.NameText == "Close tab");

        DoubleClickAt(window, closeTab.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "Close tab"));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(closeTab.IsEditing);
        closeTab.GetVisualDescendants().OfType<TextBox>().Single().RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        actions.Clear();

        var chrome = tree.Rows.OfType<SectionRow>().Single(row => row.NameText == "Chrome");
        DoubleClickAt(window, chrome.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "Chrome"));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // The first click toggles, the double click puts it back: two toggles, net nothing.
        Assert.Equal(2, actions.Count(action => action is { Action: CommandTreeAction.ToggleExpanded, Section.Name: "Chrome" }));
        Assert.True(chrome.IsEditing);
    }

    [AvaloniaFact]
    public void ARebuildKeepsTheKeyboardFocusOnTheSelectedRow_SoTheRenameKeyStillReachesAHeader()
    {
        var (tree, _, vm) = Show();
        var window = (Window)TopLevel.GetTopLevel(tree)!;
        tree.SelectedSectionId = Section(vm, "Chrome").Id;
        FocusSelectedRow(tree);

        // What a click on a header does: the toggle comes back as new sections and every row is rebuilt.
        tree.Sections = vm.Sections.ToList();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.KeyPressQwerty(CommandsKeymap.Current.Rename == "F2" ? PhysicalKey.F2 : PhysicalKey.Enter, RawInputModifiers.None);

        Assert.True(tree.Rows.OfType<SectionRow>().Single(row => row.NameText == "Chrome").IsEditing);
    }

    private static List<string> ShowingNewCommand(CommandTree tree)
        => [.. tree.Rows.OfType<SectionRow>().Where(row => row.ShowsNewCommand).Select(row => row.NameText)];

    private static Button NewCommandButton(SectionRow row)
        => row.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_NewCommand");

    private static void DoubleClickAt(Window window, Visual target)
    {
        ClickAt(window, target);
        ClickAt(window, target);
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
        var tree = new CommandTree { Sections = vm.Sections, Heading = vm.Heading, NewSectionLabel = vm.NewSectionLabel, PlatformFilterLabel = vm.PlatformFilterLabel, HelpText = vm.Help };
        tree.ActionRequested += (_, e) => actions.Add(e);
        var window = new Window { Content = tree, Width = 800, Height = 600 };
        window.Show();
        return (tree, actions, vm);
    }
}
