#if DEBUG
using Augram.App.Declarations;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;

namespace Augram.App.DevGallery;

/// <summary>
/// A gallery page in both themes, side by side (plan 0006 step 6): the page is declared twice and each copy sits in a
/// <see cref="ThemeVariantScope"/> on that theme's opaque window colour (<c>Layer.Solid</c>), so a component that reads
/// badly in one theme shows at a glance, whatever the app's own theme is. Only the Default theme follows the scope:
/// its colours are dynamic resources per theme variant; Wireframe's are fixed.
/// </summary>
internal static class ThemePair
{
    public static ScreenDeclaration Declare(string title, Func<ScreenDeclaration> page) =>
        new ComponentScreen(title, () => Build(page));

    private static Grid Build(Func<ScreenDeclaration> page)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
        grid.Children.Add(Half(page, ThemeVariant.Dark, "Dark", 0));
        grid.Children.Add(Half(page, ThemeVariant.Light, "Light", 1));
        return grid;
    }

    private static ThemeVariantScope Half(Func<ScreenDeclaration> page, ThemeVariant variant, string label, int column)
    {
        var caption = new TextBlock { Text = label, Margin = new Thickness(12, 8, 12, 4), HorizontalAlignment = HorizontalAlignment.Left };
        caption.Classes.Add("help");
        var content = new DockPanel();
        DockPanel.SetDock(caption, Dock.Top);
        content.Children.Add(caption);
        content.Children.Add(new Components.ScreenHost.ScreenHost { Screen = page() });
        var surface = new Border { Child = content };
        surface.Bind(Border.BackgroundProperty, surface.GetResourceObservable("Layer.Solid"));
        var scope = new ThemeVariantScope { RequestedThemeVariant = variant, Child = surface };
        Grid.SetColumn(scope, column);
        return scope;
    }
}
#endif
