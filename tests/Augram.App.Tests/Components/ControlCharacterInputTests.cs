using Augram.App.Hosting;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Xunit;

namespace Augram.App.Tests.Components;

/// <summary>
/// Text input of control characters only never reaches a text box (Joel, Mac, 0.13.11: Escape in an empty search box typed a
/// square, since macOS passes an unhandled key on as text and Escape's is U+001B); ordinary text still does.
/// </summary>
public sealed class ControlCharacterInputTests
{
    [AvaloniaFact]
    public void AControlCharacterIsDroppedBeforeTheTextBox_OrdinaryTextGetsThrough()
    {
        ControlCharacterInput.Register();
        var field = new TextBox { Width = 200 };
        var window = new Window { Width = 400, Height = 300, Content = field };
        window.Show();
        Assert.True(field.Focus());

        field.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "\u001b" });
        Assert.Equal("", field.Text ?? "");

        field.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "\u007f\u0001" });
        Assert.Equal("", field.Text ?? "");

        field.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = "ab" });
        Assert.Equal("ab", field.Text);
    }
}
