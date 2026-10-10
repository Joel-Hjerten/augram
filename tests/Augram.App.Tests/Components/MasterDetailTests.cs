using Augram.App.Components.CommandTree;
using Augram.App.Components.MasterDetail;
using Augram.App.Components.SectionForm;
using Augram.App.Declarations;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Components;

/// <summary>The list-beside-form component the Ignored tab uses: rows from items, the host's selection, the detail form, and one intent per user action.</summary>
public sealed class MasterDetailTests
{
    private static readonly MasterItem Blender = new(Guid.NewGuid(), "Blender", true, "Gestures off over this app · blender.exe");
    private static readonly MasterItem Spine = new(Guid.NewGuid(), "Spine", false, "Gestures off over this app · Spine.exe");
    private static readonly MasterItem VMware = new(Guid.NewGuid(), "VMware", true, "Disable while focused · vmware.exe");

    [AvaloniaFact]
    public void ShowsARowPerItem_InTheSharedRowFrame_GreyingInactiveOnes()
    {
        var (view, _) = Show();

        var rows = view.Rows.OfType<MasterRow>().ToList();
        Assert.Equal(["Blender", "Spine", "VMware"], rows.Select(row => row.NameText));
        Assert.Equal(VMware.Summary, rows[2].SummaryText);
        Assert.Contains(":inactive", rows[1].Classes);
        Assert.False(rows[1].GetVisualDescendants().OfType<CheckBox>().Single().IsChecked);
        Assert.All(rows, row => Assert.Contains(row.GetVisualDescendants().OfType<Border>(), border => border.Classes.Contains("row")));
        Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Ignored apps");
    }

    [AvaloniaFact]
    public void TheHostsSelectionIsShownWithoutAnEcho_AndAUserSelectionRaisesSelect()
    {
        var (view, actions) = Show();
        var list = view.GetVisualDescendants().OfType<ListBox>().Single();

        view.SelectedId = Spine.Id;
        Assert.Same(Spine, view.SelectedItem);
        view.Items = [Blender, Spine, VMware];
        Assert.Same(Spine, view.SelectedItem);
        Assert.Empty(actions);

        list.SelectedItem = view.Rows[2];
        var select = Assert.Single(actions);
        Assert.Equal(MasterDetailAction.Select, select.Action);
        Assert.Same(VMware, select.Item);
    }

    [AvaloniaFact]
    public void TheDetailFormShowsBesideTheList_AndTheEmptyTextWithoutOne()
    {
        var (view, _) = Show();
        var form = view.GetVisualDescendants().OfType<SectionForm>().Single();
        var empty = view.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "Select one.");

        Assert.False(form.IsVisible);
        Assert.True(empty.IsVisible);

        view.Detail = new FormScreen("Detail", [new Section("Ignored app", [new NoteField("Name", "Blender")])]);
        TopLevel.GetTopLevel(view)!.UpdateLayout();

        Assert.True(view.HasDetail);
        Assert.True(form.IsVisible);
        Assert.False(empty.IsVisible);
        Assert.Contains(form.GetVisualDescendants().OfType<FieldRow>(), row => row.Label == "Name");
    }

    [AvaloniaFact]
    public void TheButtonTheActiveBoxAndTheKeysRaiseTheirIntents()
    {
        var (view, actions) = Show();
        var window = (Window)TopLevel.GetTopLevel(view)!;
        var modifier = CommandsKeymap.Current.IsMacOS ? RawInputModifiers.Meta : RawInputModifiers.Control;

        var newButton = view.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, "New ignored app"));
        newButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
        view.Rows.OfType<MasterRow>().First().GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = false;

        // Delete without a selection asks nothing.
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        view.SelectedId = Blender.Id;
        FocusSelectedRow(view);
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Z, modifier);
        window.KeyPressQwerty(PhysicalKey.N, modifier);

        Assert.Equal(
            [MasterDetailAction.New, MasterDetailAction.ToggleActive, MasterDetailAction.Delete, MasterDetailAction.Undo, MasterDetailAction.New],
            actions.Select(action => action.Action));
        Assert.Same(Blender, actions[1].Item);
        Assert.Same(Blender, actions[2].Item);
    }

    [AvaloniaFact]
    public void TheRenameKeyEditsInPlace_TheOtherKeysStandDown_AndEnterRaisesTheName()
    {
        var (view, actions) = Show();
        var window = (Window)TopLevel.GetTopLevel(view)!;
        view.SelectedId = VMware.Id;
        FocusSelectedRow(view);

        window.KeyPressQwerty(CommandsKeymap.Current.Rename == "F2" ? PhysicalKey.F2 : PhysicalKey.Enter, RawInputModifiers.None);
        Assert.True(view.IsEditing);
        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Assert.Empty(actions);

        var editor = view.Rows.OfType<MasterRow>().Single(row => row.NameText == "VMware").GetVisualDescendants().OfType<TextBox>().Single();
        editor.Text = " VMware Workstation ";
        editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter });

        var rename = Assert.Single(actions);
        Assert.Equal(MasterDetailAction.Rename, rename.Action);
        Assert.Equal("VMware Workstation", rename.Name);
        Assert.Same(VMware, rename.Item);
        Assert.False(view.IsEditing);
    }

    [AvaloniaFact]
    public void KeysTypedInTheDetailForm_BelongToItsText_NotToTheList()
    {
        var (view, actions) = Show();
        var window = (Window)TopLevel.GetTopLevel(view)!;
        var modifier = CommandsKeymap.Current.IsMacOS ? RawInputModifiers.Meta : RawInputModifiers.Control;
        var name = "Blender";
        view.SelectedId = Blender.Id;
        view.Detail = new FormScreen("Detail", [new Section("Ignored app", [new TextField("Name", new DelegateBinding<string>(() => name, value => name = value))])]);
        window.UpdateLayout();
        var editor = view.GetVisualDescendants().OfType<FieldRow>().Single().GetVisualDescendants().OfType<TextBox>().Single();
        Assert.True(editor.Focus());

        window.KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Z, modifier);
        window.KeyPressQwerty(PhysicalKey.N, modifier);
        window.KeyPressQwerty(PhysicalKey.F2, RawInputModifiers.None);

        Assert.Empty(actions);
        Assert.False(view.IsEditing);
    }

    /// <summary>The Ignored tab's move between its lists (plan 0004): a menu entry only while the host labels it, raising Move for the selected item.</summary>
    [AvaloniaFact]
    public void TheMenusMoveEntry_FollowsTheHostsLabel_AndRaisesMoveForTheSelectedItem()
    {
        var (view, actions) = Show();
        var list = view.GetVisualDescendants().OfType<ListBox>().Single();
        var move = list.ContextMenu!.Items.OfType<MenuItem>().Single(item => item.Tag is MasterDetailAction.Move);

        list.RaiseEvent(new ContextRequestedEventArgs());
        Assert.False(move.IsVisible);
        list.ContextMenu.Close();

        view.MoveLabel = "Move to Per command";
        view.SelectedId = Spine.Id;
        list.RaiseEvent(new ContextRequestedEventArgs());
        Assert.Equal(("Move to Per command", true, true), (move.Header as string, move.IsVisible, move.IsEnabled));

        move.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
        var raised = Assert.Single(actions);
        Assert.Equal(MasterDetailAction.Move, raised.Action);
        Assert.Same(Spine, raised.Item);
    }

    private static void FocusSelectedRow(MasterDetail view)
    {
        var list = view.GetVisualDescendants().OfType<ListBox>().Single();
        Assert.True(list.ContainerFromItem(list.SelectedItem!)!.Focus());
    }

    private static (MasterDetail View, List<MasterDetailActionEventArgs> Actions) Show()
    {
        var actions = new List<MasterDetailActionEventArgs>();
        var view = new MasterDetail
        {
            Heading = "Ignored apps",
            NewLabel = "New ignored app",
            HelpText = "Help.",
            EmptyDetailText = "Select one.",
            Items = [Blender, Spine, VMware],
        };
        view.ActionRequested += (_, e) => actions.Add(e);
        var window = new Window { Content = view, Width = 900, Height = 600 };
        window.Show();
        return (view, actions);
    }
}
