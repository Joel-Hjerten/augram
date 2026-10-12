using Augram.App.Components.ItemList;
using Augram.App.Tests.Support;
using Augram.App.UsedBy;
using Augram.Core.Gestures;
using Augram.Core.Mapping;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.UsedBy;

public sealed class UsedByWindowTests
{
    [AvaloniaFact]
    public void ListsEveryCommandBoundToTheGestureInDocumentOrderWithItsSteps()
    {
        var (mapping, gesture) = Mapping();
        var window = new UsedByWindow(gesture, "Down", mapping, new FakeCommandLocator());
        window.Show();

        Assert.Equal("Used by: Down", window.Title);
        Assert.False(window.IsEmpty);
        Assert.Equal(["Global › Minimize", "Chrome › Close tab", "Steam › Nothing"], window.Rows.Select(row => row.Label));
        Assert.Equal(["Wait 30 ms", "Wait 30 ms", "(override to nothing)"], window.Rows.Select(row => row.Steps));
        Assert.Equal("Close tab (inactive)", window.Rows[1].CommandText);
        var list = window.GetVisualDescendants().OfType<ItemList>().Single();
        Assert.True(list.IsVisible);
        Assert.Equal("Go to command", ((Button)Assert.Single(list.ToolbarItems)).Content);
        window.Close();
    }

    [AvaloniaFact]
    public void FollowsTheStoreWhileOpenAndSaysSoWhenNoCommandUsesTheGesture()
    {
        var (mapping, gesture) = Mapping();
        var window = new UsedByWindow(gesture, "Down", mapping, null);
        window.Show();
        var list = window.GetVisualDescendants().OfType<ItemList>().Single();
        Assert.Empty(list.ToolbarItems);

        foreach (var row in window.Rows)
        {
            mapping.RemoveCommand(row.CommandId);
        }

        Assert.True(window.IsEmpty);
        Assert.Empty(window.Rows);
        Assert.False(list.IsVisible);
        var note = window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Classes.Contains("note"));
        Assert.True(note.IsVisible);
        Assert.Equal("No command uses this gesture.", note.Text);
        window.Close();
    }

    [AvaloniaFact]
    public void GoToHandsTheCommandToTheLocatorAndCloses()
    {
        var (mapping, gesture) = Mapping();
        var locator = new FakeCommandLocator();
        var window = new UsedByWindow(gesture, "Down", mapping, locator);
        window.Show();
        var closed = false;
        window.Closed += (_, _) => closed = true;

        window.GoTo(null);
        Assert.Empty(locator.Shown);
        Assert.False(closed);

        window.GoTo(window.Rows[1]);

        Assert.Equal([window.Rows[1].CommandId], locator.Shown);
        Assert.True(closed);
    }

    private static (MappingStore Mapping, GestureId Gesture) Mapping()
    {
        var gesture = GestureId.New();
        var mapping = new MappingStore();
        mapping.AddCommand(GroupId.Global, MappingFixture.Command("Minimize", gesture));
        mapping.AddCommand(GroupId.Global, MappingFixture.Unbound("Other"));
        mapping.AddGroup(MappingFixture.Group("Chrome", MappingFixture.Command("Close tab", gesture, isActive: false)));
        mapping.AddGroup(MappingFixture.Group("Steam", MappingFixture.Command("Nothing", gesture, withStep: false)));
        return (mapping, gesture);
    }
}
