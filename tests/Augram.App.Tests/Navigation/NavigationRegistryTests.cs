using Augram.App.Components.ScreenHost;
using Augram.App.Components.Shell;
using Augram.App.Declarations;
using Augram.App.Navigation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Navigation;

public sealed class NavigationRegistryTests
{
    [Fact]
    public void RejectsDuplicateKeysAcrossLevels()
    {
        var entries = new List<NavEntry>
        {
            new("A", "a", () => new TextScreen("A", "a")),
            new("B", "b", SubEntries: [new NavEntry("A again", "a", () => new TextScreen("A", "a"))]),
        };

        Assert.Throws<ArgumentException>(() => new NavigationRegistry(entries));
    }

    [Fact]
    public void FindsEntriesAtAnyLevel()
    {
        var registry = Registry();

        Assert.Equal("Sub B", registry.Find("second.b")?.Title);
        Assert.Null(registry.Find("missing"));
    }

    [AvaloniaFact]
    public void ShellRendersTabsAndSubTabsFromTheRegistry()
    {
        var shell = new Shell { Registry = Registry() };
        var window = new Window { Content = shell };
        window.Show();

        Assert.Equal(["First", "Second"], shell.Tabs.Select(tab => (string)tab.Header!).ToList());
        Assert.IsType<ScreenHost>(shell.Tabs[0].Content);
        var sub = Assert.IsType<TabControl>(shell.Tabs[1].Content);
        Assert.Equal(["Sub A", "Sub B"], ((IEnumerable<TabItem>)sub.ItemsSource!).Select(tab => (string)tab.Header!).ToList());
    }

    [AvaloniaFact]
    public void ShellSelectsATabByKey()
    {
        var shell = new Shell { Registry = Registry(), SelectedKey = "second.b" };
        var window = new Window { Content = shell };
        window.Show();

        var tabs = shell.GetVisualDescendants().OfType<TabControl>().First();
        var sub = (TabControl)shell.Tabs[1].Content!;

        Assert.Same(shell.Tabs[1], tabs.SelectedItem);
        Assert.Equal("Sub B", ((TabItem)sub.SelectedItem!).Header);
    }

    private static NavigationRegistry Registry() => new(
    [
        new NavEntry("First", "first", () => new TextScreen("First", "first")),
        new NavEntry("Second", "second", SubEntries:
        [
            new NavEntry("Sub A", "second.a", () => new TextScreen("Sub A", "a")),
            new NavEntry("Sub B", "second.b", () => new TextScreen("Sub B", "b")),
        ]),
    ]);
}
