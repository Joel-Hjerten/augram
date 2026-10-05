using Augram.App.Components.ItemList;
using Augram.App.Declarations;
using Augram.App.Tests.Support;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Augram.App.Tests.Components;

public sealed class ItemListTests
{
    [AvaloniaFact]
    public void ShowsEveryRowAndTheColumnHeaders()
    {
        var (list, _, _) = Show();

        Assert.Equal(3, list.Rows.Count);
        var headers = ((Grid)list.HeaderRow!).Children.OfType<TextBlock>().Select(t => t.Text).ToList();
        Assert.Equal(["#", "Level", "Message"], headers);
        Assert.Equal("List", list.Title);
    }

    [AvaloniaFact]
    public void AppliesTheFilterWhenItsBindingChanges()
    {
        var (list, _, filter) = Show();

        filter.Set("warn");
        list.Refresh();

        Assert.Single(list.Rows);
        Assert.Equal("warn", ((FakeRow)list.Rows[0]).Level);
    }

    [AvaloniaFact]
    public void FollowsTheSourceWhenItChanges()
    {
        var (list, rows, _) = Show();
        var source = (ListSource<FakeRow>)list.Spec!.Source;

        rows.Add(new FakeRow(4, "info", "late"));
        source.NotifyChanged();

        Assert.Equal(4, list.Rows.Count);
    }

    [AvaloniaFact]
    public void BuildsOneToolbarItemPerFilterAndAction()
    {
        var (list, _, _) = Show();

        Assert.Equal(2, list.ToolbarItems.Count);
        Assert.IsType<StackPanel>(list.ToolbarItems[0]);
        Assert.Equal("Clear", ((Button)list.ToolbarItems[1]).Content);
    }

    private static (ItemList List, List<FakeRow> Rows, DelegateBinding<string> Filter) Show()
    {
        var rows = new List<FakeRow>
        {
            new(1, "info", "one"),
            new(2, "warn", "two"),
            new(3, "info", "three"),
        };
        var selected = "All";
        var filter = new DelegateBinding<string>(() => selected, v => selected = v);
        var spec = new ListSpec(
            "List",
            Columns:
            [
                new ListColumn("#", row => ((FakeRow)row).Number.ToString(System.Globalization.CultureInfo.InvariantCulture), 40),
                new ListColumn("Level", row => ((FakeRow)row).Level, 60),
                new ListColumn("Message", row => ((FakeRow)row).Message),
            ],
            Source: new ListSource<FakeRow>(() => rows),
            Toolbar: [new ListAction("Clear", rows.Clear)],
            Filters: [new ListFilter("Level", ["All", "info", "warn"], filter, (row, level) => level == "All" || ((FakeRow)row).Level == level)]);
        var list = new ItemList { Spec = spec };
        var window = new Window { Content = list };
        window.Show();
        return (list, rows, filter);
    }
}
