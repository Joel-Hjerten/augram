using Augram.App.Components.Shell;
using Augram.App.Views;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Augram.App.Tests;

public sealed class MainWindowTests
{
    [AvaloniaFact]
    public void ShowsTheDeclaredTabs()
    {
        var window = TestAppBuilder.Services.GetRequiredService<MainWindow>();
        window.Show();

        var shell = window.GetVisualDescendants().OfType<Shell>().Single();
        var titles = shell.Tabs.Select(tab => (string)tab.Header!).ToList();

        Assert.Equal(["Commands", "Exclusions", "Gestures", "Options", "Diagnostics"], titles.Take(5));
    }

    [AvaloniaFact]
    public void ClosingHidesInsteadOfClosing()
    {
        var window = TestAppBuilder.Services.GetRequiredService<MainWindow>();
        window.Show();

        window.Close();

        Assert.False(window.IsVisible);
        window.Show();
        Assert.True(window.IsVisible);
    }
}
