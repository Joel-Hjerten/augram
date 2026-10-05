using Augram.App.Views;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Augram.App.Tests;

public sealed class MainWindowTests
{
    [AvaloniaFact]
    public void ShowsMilestoneHeading()
    {
        var window = new MainWindow();
        window.Show();

        var heading = window.FindControl<TextBlock>("Heading");

        Assert.Equal("Augram M0", heading?.Text);
    }
}
