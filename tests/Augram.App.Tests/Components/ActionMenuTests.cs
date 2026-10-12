using Augram.App.Components.ListEditing;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Xunit;

namespace Augram.App.Tests.Components;

/// <summary><see cref="ActionMenu{TAction}"/>, the one menu and key builder of the editable lists: every entry and key ends in one action.</summary>
public sealed class ActionMenuTests
{
    private enum Act
    {
        First,
        Second,
    }

    [AvaloniaFact]
    public void TheMenuListsTheTableInOrder_AndAClickRequestsItsAction()
    {
        var requested = new List<Act>();
        var menu = ActionMenu<Act>.Build(requested.Add, ("One", Act.First), ("Two", Act.Second));

        var entries = ActionMenu<Act>.Entries(menu).ToList();
        Assert.Equal([("One", Act.First), ("Two", Act.Second)], entries.Select(entry => ((string)entry.Item.Header!, entry.Action)));
        entries[1].Item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        Assert.Equal([Act.Second], requested);
    }

    [AvaloniaFact]
    public void KeysSkipANullGesture_AndStandDownWhileTheListSaysNo()
    {
        var requested = new List<Act>();
        var allowed = true;
        var target = new Border();
        ActionMenu<Act>.BindKeys(target, requested.Add, () => allowed, ("F2", Act.First), (null, Act.First), ("Delete", Act.Second));

        Assert.Equal([KeyGesture.Parse("F2"), KeyGesture.Parse("Delete")], target.KeyBindings.Select(binding => binding.Gesture));
        target.KeyBindings[1].Command!.Execute(null);
        Assert.Equal([Act.Second], requested);

        allowed = false;
        Assert.False(target.KeyBindings[0].Command!.CanExecute(null));
    }
}
