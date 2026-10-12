using System.Runtime.CompilerServices;
using Augram.App.Components.ListEditing;
using Augram.App.Components.MasterDetail;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Xunit;

namespace Augram.App.Tests.Components;

/// <summary>The list plumbing every editable list shares (S14b): the host's selection without an echo, focus after a rebuild, right-click select, the keys standing down.</summary>
public sealed class RowListHostTests
{
    [AvaloniaFact]
    public void ARebuildKeepsTheHostsSelection_WithoutRaisingAUserSelection()
    {
        var (host, list, _, selections) = Show(FocusAfterRebuild.None);
        var wanted = Guid.NewGuid();
        var rows = Rows(Guid.NewGuid(), wanted);

        host.Replace(() => list.ItemsSource = rows, () => host.SelectQuietly(rows.Single(row => row.Item!.Id == wanted)));

        Assert.Same(rows[1], list.SelectedItem);
        Assert.Equal(0, selections.Value);

        list.SelectedItem = rows[0];
        Assert.Equal(1, selections.Value);
    }

    [AvaloniaFact]
    public void ARebuildOfAFocusedList_GivesTheFocusToTheSelectedRowsReplacement()
    {
        var (host, list, window, _) = Show(FocusAfterRebuild.SelectedRow);
        Assert.True(list.ContainerFromItem(list.SelectedItem!)!.Focus());

        var rows = Rows(Guid.NewGuid(), Guid.NewGuid());
        host.Replace(() => list.ItemsSource = rows, () => host.SelectQuietly(rows[1]));
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.True(list.ContainerFromItem(rows[1])!.IsKeyboardFocusWithin);
    }

    [AvaloniaFact]
    public void ARightPressSelectsTheRowUnderThePointer()
    {
        var (host, list, window, selections) = Show(FocusAfterRebuild.None);
        var rows = (IReadOnlyList<MasterRow>)list.ItemsSource!;
        foreach (var row in rows)
        {
            row.PointerPressed += (sender, e) => host.SelectOnRightPress((Control)sender!, e);
        }

        var point = rows[1].TranslatePoint(new Point(4, 4), window)!.Value;
        window.MouseDown(point, MouseButton.Right);
        window.MouseUp(point, MouseButton.Right);

        Assert.Same(rows[1], list.SelectedItem);
        Assert.Equal(1, selections.Value);
    }

    [AvaloniaFact]
    public void TheKeysStandDownWhileANameIsBeingEdited()
    {
        var (host, list, window, _) = Show(FocusAfterRebuild.None);
        var row = (MasterRow)list.SelectedItem!;
        Assert.True(host.KeysEnabled());

        host.BeginRename(row);
        window.UpdateLayout();

        Assert.True(row.IsEditing);
        Assert.False(host.KeysEnabled());
        row.CancelEdit();
        Assert.True(host.KeysEnabled());
    }

    private static List<MasterRow> Rows(params Guid[] ids)
        => [.. ids.Select((id, i) => new MasterRow { Item = new MasterItem(id, $"Item {i}", true, string.Empty) })];

    private static (RowListHost Host, ListBox List, Window Window, StrongBox<int> Selections) Show(FocusAfterRebuild focus)
    {
        var list = new ListBox();
        var selections = new StrongBox<int>();
        var host = new RowListHost(() => RowListHost.AnyEditing(list.Items.OfType<Control>()), focus);
        host.Attach(list, () => selections.Value++);
        var rows = Rows(Guid.NewGuid(), Guid.NewGuid());
        host.Replace(() => list.ItemsSource = rows, () => host.SelectQuietly(rows[0]));
        var window = new Window { Content = list, Width = 400, Height = 300 };
        window.Show();
        window.UpdateLayout();
        return (host, list, window, selections);
    }
}
