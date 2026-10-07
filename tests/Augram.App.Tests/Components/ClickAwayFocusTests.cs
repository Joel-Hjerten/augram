using Augram.App.Views;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Xunit;

namespace Augram.App.Tests.Components;

/// <summary>A click outside the focused text box takes its focus away, so commit-on-leave editors commit (Joel, 2026-10-07).</summary>
public sealed class ClickAwayFocusTests
{
    [AvaloniaFact]
    public void AClickOnEmptySpaceDefocusesTheTextBox_AClickInsideItDoesNot()
    {
        ClickAwayFocus.Register();
        var field = new TextBox { Width = 200, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
        var window = new Window { Width = 400, Height = 300, Content = new Panel { Background = Avalonia.Media.Brushes.Transparent, Children = { field } } };
        window.Show();
        Assert.True(field.Focus());

        Click(window, field.TranslatePoint(new Point(10, 10), window)!.Value);
        Assert.True(field.IsFocused);

        Click(window, new Point(300, 250));
        Assert.False(field.IsFocused);
    }

    [AvaloniaFact]
    public void AClickOnAnotherTextBoxMovesTheFocusThere()
    {
        ClickAwayFocus.Register();
        var first = new TextBox { Width = 150 };
        var second = new TextBox { Width = 150 };
        var window = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { first, second } } };
        window.Show();
        Assert.True(first.Focus());

        Click(window, second.TranslatePoint(new Point(10, 5), window)!.Value);

        Assert.False(first.IsFocused);
        Assert.True(second.IsFocused);
    }

    private static void Click(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
    }
}
