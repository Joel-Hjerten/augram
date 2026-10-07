using Augram.App.Components.CommandTree;
using Augram.App.ViewModels.Commands;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Commands;

public sealed class CommandTreeTests
{
    [AvaloniaFact]
    public void ShowsAHeaderPerGroupAndTheCommandsOfExpandedGroupsOnly()
    {
        var (tree, _, _) = Show(collapse: "Chrome");

        var rows = tree.Rows;
        Assert.Equal(["Global", "Apple", "Chrome"], rows.OfType<GroupRow>().Select(row => row.NameText));
        Assert.Equal(["Close window", "Three steps", "Volume up"], rows.OfType<CommandRow>().Select(row => row.NameText));
        Assert.Equal("3 commands", rows.OfType<GroupRow>().First().CountText);
        Assert.Contains(":global", rows.OfType<GroupRow>().First().Classes);
        Assert.Contains(":collapsed", rows.OfType<GroupRow>().Last().Classes);
        Assert.False(rows.OfType<GroupRow>().First().CanRename);

        var close = rows.OfType<CommandRow>().First();
        Assert.True(close.HasGlyph);
        Assert.Equal("Close window", close.SummaryText);
        var volume = rows.OfType<CommandRow>().Last();
        Assert.False(volume.HasGlyph);
        Assert.Equal("Wheel up", volume.TriggerText);
    }

    [AvaloniaFact]
    public void ToolbarButtonsAndTheExpanderRaiseTheirActions()
    {
        var (tree, actions, _) = Show();
        var buttons = tree.GetVisualDescendants().OfType<Button>().Where(button => button.Classes.Contains("toolbar")).ToDictionary(button => (string)button.Content!);

        Click(buttons["New group…"]);
        Click(buttons["New command"]);
        Click(buttons["Undo"]);
        Click(buttons["Redo"]);
        Click(tree.Rows.OfType<GroupRow>().Last().GetVisualDescendants().OfType<Button>().Single(button => button.Classes.Contains("expander")));

        Assert.Equal([CommandTreeAction.NewGroup, CommandTreeAction.NewCommand, CommandTreeAction.Undo, CommandTreeAction.Redo, CommandTreeAction.ToggleExpanded], actions.Select(action => action.Action));
        Assert.Equal("Chrome", actions[^1].Group!.Name);
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
        Assert.Equal("Chrome", select.Group!.Name);
        Assert.Equal("Close tab", select.Command!.Name);

        list.SelectedItem = tree.Rows.OfType<GroupRow>().Single(row => row.NameText == "Apple");
        Assert.Equal("Apple", actions[^1].Group!.Name);
        Assert.Null(actions[^1].Command);

        actions.Clear();
        tree.SelectedGroupId = vm.Groups[0].Id;
        tree.SelectedCommandId = vm.Groups[0].Commands[1].Id;
        Assert.Equal("Three steps", tree.SelectedCommand!.Name);
        Assert.Empty(actions);

        tree.Groups = vm.Groups.ToList();
        Assert.Equal("Three steps", tree.SelectedCommand!.Name);
        Assert.Empty(actions);
    }

    [AvaloniaFact]
    public void RenameEditsInPlaceAndKeyBindingsStandDownMeanwhile()
    {
        var (tree, actions, vm) = Show();
        var window = (Window)TopLevel.GetTopLevel(tree)!;
        tree.SelectedGroupId = vm.Groups[0].Id;
        tree.SelectedCommandId = vm.Groups[0].Commands[0].Id;
        FocusSelectedRow(tree);

        window.KeyPressQwerty(CommandsKeymap.Current.Rename == "F2" ? PhysicalKey.F2 : PhysicalKey.Enter, RawInputModifiers.None);

        Assert.True(tree.IsEditing);
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Assert.Empty(actions);

        var row = tree.Rows.OfType<CommandRow>().First();
        var editor = row.GetVisualDescendants().OfType<TextBox>().Single();
        editor.Text = " Close it ";
        editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

        var rename = Assert.Single(actions);
        Assert.Equal(CommandTreeAction.Rename, rename.Action);
        Assert.Equal("Close it", rename.Name);
        Assert.Equal("Close window", rename.Command!.Name);
        Assert.Equal("Global", rename.Group!.Name);
        Assert.False(tree.IsEditing);
    }

    [AvaloniaFact]
    public void DeleteKeyCarriesTheCommandOrOnlyTheGroupAndTheCheckBoxTogglesActive()
    {
        var (tree, actions, vm) = Show();
        var window = (Window)TopLevel.GetTopLevel(tree)!;
        var modifier = CommandsKeymap.Current.IsMacOS ? RawInputModifiers.Meta : RawInputModifiers.Control;
        tree.SelectedGroupId = vm.Groups[0].Id;
        tree.SelectedCommandId = vm.Groups[0].Commands[0].Id;
        FocusSelectedRow(tree);

        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.C, modifier);
        window.KeyPressQwerty(PhysicalKey.V, modifier);
        window.KeyPressQwerty(PhysicalKey.N, modifier);

        Assert.Equal([CommandTreeAction.Delete, CommandTreeAction.Copy, CommandTreeAction.Paste, CommandTreeAction.NewCommand], actions.Select(action => action.Action));
        Assert.All(actions, action => Assert.Equal("Close window", action.Command!.Name));

        actions.Clear();
        tree.SelectedCommandId = null;
        tree.SelectedGroupId = vm.Groups[2].Id;
        FocusSelectedRow(tree);
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);

        var delete = Assert.Single(actions);
        Assert.Equal("Chrome", delete.Group!.Name);
        Assert.Null(delete.Command);

        actions.Clear();
        tree.Rows.OfType<CommandRow>().First().GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = false;
        var toggle = Assert.Single(actions);
        Assert.Equal(CommandTreeAction.ToggleActive, toggle.Action);
        Assert.Equal("Close window", toggle.Command!.Name);
    }

    [AvaloniaFact]
    public void HeaderDropdownAsksForTheKindAndFallsBackToTheCommandsRealKind()
    {
        var (tree, _, vm) = Show();
        var actions = new List<CommandTreeActionEventArgs>();
        var header = new CommandHeader { Item = vm.Groups[0].Commands[0] };
        header.ActionRequested += (_, e) => actions.Add(e);
        var window = new Window { Content = header };
        window.Show();
        Assert.True(header.HasCommand);
        Assert.True(header.IsGestureKind);
        Assert.Equal((int)TriggerKind.Gesture, header.KindIndex);

        header.GetVisualDescendants().OfType<ComboBox>().Single().SelectedIndex = (int)TriggerKind.WheelDown;

        var kind = Assert.Single(actions);
        Assert.Equal(CommandTreeAction.SetTriggerKind, kind.Action);
        Assert.Equal(TriggerKind.WheelDown, kind.Kind);
        Assert.Equal("Close window", kind.Command!.Name);
        Assert.Equal((int)TriggerKind.Gesture, header.KindIndex);

        Click(header.GetVisualDescendants().OfType<Button>().Single());
        Assert.Equal(CommandTreeAction.PickGesture, actions[^1].Action);

        header.Item = null;
        Assert.False(header.HasCommand);
        Assert.Equal(-1, header.KindIndex);
        Assert.NotNull(tree);
    }

    private static void Click(Button button) => button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    private static void FocusSelectedRow(CommandTree tree)
    {
        var list = tree.GetVisualDescendants().OfType<ListBox>().Single();
        Assert.True(list.ContainerFromItem(list.SelectedItem!)!.Focus());
    }

    private static (CommandTree Tree, List<CommandTreeActionEventArgs> Actions, CommandsViewModel Vm) Show(string? collapse = null)
    {
        var (vm, _, _, _) = CommandsTestData.Create();
        if (collapse is not null)
        {
            vm.Handle(new CommandTreeActionEventArgs(CommandTreeAction.ToggleExpanded, vm.Groups.Single(group => group.Name == collapse)));
        }

        var actions = new List<CommandTreeActionEventArgs>();
        var tree = new CommandTree { Groups = vm.Groups };
        tree.ActionRequested += (_, e) => actions.Add(e);
        var window = new Window { Content = tree, Width = 800, Height = 600 };
        window.Show();
        return (tree, actions, vm);
    }
}
