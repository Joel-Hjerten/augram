using Augram.App.Components.CommandsWorkbench;
using Augram.App.Components.CommandTree;
using Augram.App.Components.ListEditing;
using Augram.App.Declarations;
using Augram.App.Hosting;
using Augram.App.Screens;
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
        Assert.Equal(["Media", "Uncategorized", "Window"], rows.OfType<SectionRow>().Select(row => row.NameText));
        Assert.Equal(["Volume up", "Three steps"], rows.OfType<CommandRow>().Select(row => row.NameText));
        Assert.Equal("2 commands", rows.OfType<SectionRow>().Last().CountText);

        // Uncategorized is an ordinary category (Joel, 2026-10-11): renamed in place like the others, no active box.
        Assert.All(rows.OfType<SectionRow>(), row => Assert.True(row is { CanRename: true, CanToggleActive: false }));
        var uncategorized = rows.OfType<SectionRow>().Single(row => row.NameText == "Uncategorized");
        Assert.False(uncategorized.GetVisualDescendants().OfType<CheckBox>().Single().IsVisible);
        Assert.Equal(uncategorized.Classes.Where(name => name.StartsWith(':')), rows.OfType<SectionRow>().First().Classes.Where(name => name.StartsWith(':')));
        Assert.Contains(":collapsed", rows.OfType<SectionRow>().Last().Classes);

        var volume = rows.OfType<CommandRow>().First();
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

        window.KeyPressQwerty(EditKeymap.Current.Rename == "F2" ? PhysicalKey.F2 : PhysicalKey.Enter, RawInputModifiers.None);

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

    /// <summary>Plan 0009 decision 6: the find key focuses the search box from a selected row; Delete and Enter there edit the text, not the list; the empty state names the text.</summary>
    [AvaloniaFact]
    public void TheFindKeyFocusesTheSearchBoxAndTheEditKeysStandDownThere()
    {
        var (tree, actions, vm) = Show();
        var window = (Window)TopLevel.GetTopLevel(tree)!;
        var modifier = EditKeymap.Current.IsMacOS ? RawInputModifiers.Meta : RawInputModifiers.Control;
        tree.SelectedSectionId = Section(vm, "Chrome").Id;
        tree.SelectedCommandId = Item(vm, "Close tab").Id;
        FocusSelectedRow(tree);

        window.KeyPressQwerty(PhysicalKey.F, modifier);

        var box = tree.GetVisualDescendants().OfType<Augram.App.Components.SearchBox.SearchBox>().Single();
        Assert.True(box.IsTyping);
        Assert.True(tree.IsEditing);
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Empty(actions);

        tree.SearchText = "zzz";
        tree.Sections = [];
        Assert.Equal("No commands match 'zzz'", tree.EmptyText);
        Assert.True(tree.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "No commands match 'zzz'").IsVisible);
    }

    /// <summary>Escape in two steps (Joel, 0.13.13): the first clears the text and stays, the next leaves for the selected row.</summary>
    [AvaloniaFact]
    public void EscapeClearsTheSearchFirst_ThenLeavesForTheSelectedRow()
    {
        var (tree, _, vm) = Show();
        var window = (Window)TopLevel.GetTopLevel(tree)!;
        tree.SelectedSectionId = Section(vm, "Chrome").Id;
        tree.SelectedCommandId = Item(vm, "Close tab").Id;
        var box = tree.GetVisualDescendants().OfType<Augram.App.Components.SearchBox.SearchBox>().Single();

        box.Focus();
        tree.SearchText = "close";
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Assert.Equal(string.Empty, tree.SearchText);
        Assert.True(box.IsTyping);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Assert.False(box.IsTyping);
        Assert.True(tree.GetVisualDescendants().OfType<ListBox>().First().IsKeyboardFocusWithin);
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

        ClickAwayFocus.Register();
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
        var modifier = EditKeymap.Current.IsMacOS ? RawInputModifiers.Meta : RawInputModifiers.Control;
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

        // Uncategorized deletes like any category (with its commands; the host asks first).
        var (global, globalActions, globalVm) = Show(CommandsScope.Global);
        var globalWindow = (Window)TopLevel.GetTopLevel(global)!;
        global.SelectedSectionId = Section(globalVm, "Uncategorized").Id;
        FocusSelectedRow(global);
        globalWindow.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Assert.Equal(Section(globalVm, "Uncategorized").Id, Assert.Single(globalActions).Section!.Id);

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
    public void DoubleClickingACommandRowRenamesIt_OnItsNameOrAnywhereElseOnTheRow()
    {
        var (tree, _, _) = Show();
        var window = (Window)TopLevel.GetTopLevel(tree)!;
        var closeTab = tree.Rows.OfType<CommandRow>().Single(row => row.NameText == "Close tab");

        DoubleClickAt(window, closeTab.GetVisualDescendants().OfType<TextBlock>().First(text => text.Text == "Close tab"));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(closeTab.IsEditing);
        closeTab.GetVisualDescendants().OfType<TextBox>().Single().RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Assert.False(closeTab.IsEditing);

        // The row's body, well right of its name and short of the active box: a command row does not toggle, so all of it renames.
        var body = new Point(closeTab.Bounds.Width * 0.6, closeTab.Bounds.Height / 2);
        Assert.False(BoundsIn(closeTab, NamePart(closeTab)).Contains(body));
        DoubleClickAt(window, PointIn(window, closeTab, body));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.True(closeTab.IsEditing);
    }

    [AvaloniaFact]
    public void DoubleClickingAHeadersNameRenamesIt_EndingExpandedOrCollapsedAsItWas()
    {
        var (tree, actions, _) = Show();
        var window = (Window)TopLevel.GetTopLevel(tree)!;
        var chrome = tree.Rows.OfType<SectionRow>().Single(row => row.NameText == "Chrome");

        DoubleClickAt(window, NamePart(chrome));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // The first click toggles, the double click puts it back: two toggles, net nothing.
        Assert.Equal(2, Toggles(actions, "Chrome"));
        Assert.True(chrome.IsEditing);
    }

    [AvaloniaFact]
    public void DoubleClickingAHeaderAwayFromItsNameTogglesItOnEachClick_NoRename()
    {
        var (tree, actions, _) = Show();
        var window = (Window)TopLevel.GetTopLevel(tree)!;
        var chrome = tree.Rows.OfType<SectionRow>().Single(row => row.NameText == "Chrome");
        var count = CountPart(chrome);
        var name = BoundsIn(chrome, NamePart(chrome));

        // On the count, the empty space right of it, and the header's own line above the name: none of them is the name text.
        var places = new[]
        {
            PointIn(window, count, new Point(3, count.Bounds.Height / 2)),
            PointIn(window, chrome, new Point(chrome.Bounds.Width * 0.6, chrome.Bounds.Height / 2)),
            PointIn(window, chrome, new Point(name.Center.X, name.Top / 2)),
        };
        Assert.True(name.Top >= 2, "The header line is taller than its name, so there is room above it.");
        foreach (var place in places)
        {
            actions.Clear();

            Click(window, place);
            Assert.Equal(1, Toggles(actions, "Chrome"));

            Click(window, place);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, Toggles(actions, "Chrome"));
            Assert.False(chrome.IsEditing);
        }
    }

    [AvaloniaFact]
    public void DoubleClickingUncategorizedsName_RenamesItLikeAnyCategory()
    {
        var (tree, actions, _) = Show(CommandsScope.Global);
        var window = (Window)TopLevel.GetTopLevel(tree)!;
        var uncategorized = tree.Rows.OfType<SectionRow>().Single(row => row.NameText == "Uncategorized");

        DoubleClickAt(window, NamePart(uncategorized));
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        // Joel, 2026-10-11: an ordinary category, so the double click on its name renames it, as on Chrome's.
        Assert.Equal(2, Toggles(actions, "Uncategorized"));
        Assert.True(uncategorized.IsEditing);
    }

    [AvaloniaFact]
    public void ThroughTheViewModel_AQuickDoubleClickOpensAndClosesAHeader_OnItsNameItRenamesIt()
    {
        // As the app wires it: each toggle comes back from the view model as new sections, and every row is rebuilt under the
        // pointer between the two clicks.
        var (vm, _, _, _) = Create(CommandsScope.Apps);
        var bench = Assert.IsType<CommandsWorkbench>(Assert.IsType<ComponentScreen>(CommandsScreen.Declare(vm)).Build());
        var window = new Window { Content = bench, Width = 1400, Height = 900 };
        window.Show();
        Render();
        SectionRow Chrome() => bench.TreePart!.Rows.OfType<SectionRow>().Single(row => row.NameText == "Chrome");
        Assert.False(Section(vm, "Chrome").IsExpanded);

        var count = CountPart(Chrome());
        var onCount = PointIn(window, count, new Point(3, count.Bounds.Height / 2));
        Click(window, onCount);
        Assert.True(Section(vm, "Chrome").IsExpanded);
        Render();
        Click(window, onCount);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(Section(vm, "Chrome").IsExpanded);
        Assert.False(bench.TreePart!.IsEditing);

        Render();
        var onName = PointIn(window, NamePart(Chrome()), new Point(NamePart(Chrome()).Bounds.Width / 2, NamePart(Chrome()).Bounds.Height / 2));
        Click(window, onName);
        Render();
        Click(window, onName);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        Assert.False(Section(vm, "Chrome").IsExpanded);
        Assert.True(Chrome().IsEditing);
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
        window.KeyPressQwerty(EditKeymap.Current.Rename == "F2" ? PhysicalKey.F2 : PhysicalKey.Enter, RawInputModifiers.None);

        Assert.True(tree.Rows.OfType<SectionRow>().Single(row => row.NameText == "Chrome").IsEditing);
    }

    [AvaloniaFact]
    public void AfterEnterEndsAGroupsRename_AndTheRowsAreRebuilt_TheRenameKeyStartsAnother()
    {
        // Joel, 2026-10-12: renaming an app group a second time needed a click on it first.
        var (tree, actions, vm) = Show();
        var window = (Window)TopLevel.GetTopLevel(tree)!;
        var renameKey = EditKeymap.Current.Rename == "F2" ? PhysicalKey.F2 : PhysicalKey.Enter;
        tree.SelectedSectionId = Section(vm, "Chrome").Id;
        FocusSelectedRow(tree);

        window.KeyPressQwerty(renameKey, RawInputModifiers.None);
        Assert.True(tree.IsEditing);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);

        // The store change the rename makes comes back as new sections, and every row is rebuilt.
        tree.Sections = vm.Sections.ToList();
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        window.KeyPressQwerty(renameKey, RawInputModifiers.None);

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

    private static void DoubleClickAt(Window window, Point point)
    {
        Click(window, point);
        Click(window, point);
    }

    private static void ClickAt(Window window, Visual target)
        => Click(window, PointIn(window, target, new Point(target.Bounds.Width / 2, target.Bounds.Height / 2)));

    private static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }

    /// <summary>A point given in <paramref name="target"/>'s own coordinates, in the window's.</summary>
    private static Point PointIn(Window window, Visual target, Point local) => target.TranslatePoint(local, window)!.Value;

    /// <summary>Where <paramref name="part"/> lies inside <paramref name="row"/>.</summary>
    private static Rect BoundsIn(Visual row, Visual part) => new(part.TranslatePoint(default, row)!.Value, part.Bounds.Size);

    /// <summary>The row's name text, as the template lays it out (both themes name it <c>PART_Name</c>).</summary>
    private static TextBlock NamePart(ItemRow row) => row.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PART_Name");

    private static TextBlock CountPart(SectionRow row) => row.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == row.CountText);

    private static int Toggles(List<CommandTreeActionEventArgs> actions, string section)
        => actions.Count(action => action.Action == CommandTreeAction.ToggleExpanded && action.Section?.Name == section);

    /// <summary>Runs what is posted and renders a frame: headless hit testing uses the last rendered frame.</summary>
    private static void Render()
    {
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
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
        Render();
        return (tree, actions, vm);
    }
}
