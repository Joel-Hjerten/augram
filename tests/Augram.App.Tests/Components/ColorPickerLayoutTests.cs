using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Xunit;

namespace Augram.App.Tests.Components;

/// <summary>
/// The colour picker behind a colour field's swatch (Joel, 2026-10-08) opens at its designed size inside Augram's themes:
/// no padding around it, its tabs at their own height (the app-wide TabItem style made them 8 px taller than their
/// background), and the brightness slider inside the popup (a hand-sized 300 px flyout cut it off on the left).
/// </summary>
public sealed class ColorPickerLayoutTests
{
    [AvaloniaFact]
    public void ThePickerOpensAtItsDesignedSize()
    {
        var picker = new ColorPicker { IsAlphaEnabled = false, IsAlphaVisible = false, IsColorPaletteVisible = false };
        new Window { Content = new StackPanel { Children = { picker } }, Width = 800, Height = 700 }.Show();
        var button = picker.GetVisualDescendants().OfType<Button>().First();
        var flyout = (Flyout)button.Flyout!;

        flyout.ShowAt(button);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var popup = (Popup)typeof(PopupFlyoutBase)
            .GetProperty("Popup", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(flyout)!;
        var presenter = (FlyoutPresenter)popup.Child!;
        Assert.Equal(default, presenter.Padding);
        Assert.All(presenter.GetVisualDescendants().OfType<TabItem>().Where(tab => tab.IsVisible), tab => Assert.Equal(48, tab.Bounds.Height));
        var slider = presenter.GetVisualDescendants().OfType<Control>().Single(control => control.Name == "ColorSpectrumThirdComponentSlider");
        var origin = slider.TranslatePoint(new Point(0, 0), presenter)!.Value;
        Assert.True(origin.X >= 0 && origin.X + slider.Bounds.Width <= presenter.Bounds.Width, $"slider at {origin.X} in a {presenter.Bounds.Width} px popup");
        flyout.Hide();
    }
}
