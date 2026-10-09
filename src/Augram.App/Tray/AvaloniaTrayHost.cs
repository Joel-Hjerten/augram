using Avalonia;
using Avalonia.Controls;

namespace Augram.App.Tray;

/// <summary>Avalonia's <see cref="TrayIcon"/> (Windows, Linux): its menu opens on a right click and every left click raises <see cref="Clicked"/>.</summary>
public sealed class AvaloniaTrayHost : ITrayHost
{
    private readonly TrayIcon _icon;

    public AvaloniaTrayHost(NativeMenu menu)
    {
        ArgumentNullException.ThrowIfNull(menu);
        _icon = new TrayIcon { Menu = menu, IsVisible = true };
        _icon.Clicked += OnClicked;
        TrayIcon.SetIcons(Application.Current!, [_icon]);
    }

    public event EventHandler? Clicked;

    public void Show(WindowIcon icon, bool isTemplate, string toolTip)
    {
        // Template first, then the image: the menu bar reads the flag when the image is set.
        MacOSProperties.SetIsTemplateIcon(_icon, isTemplate);
        _icon.Icon = icon;
        _icon.ToolTipText = toolTip;
    }

    public void Dispose()
    {
        _icon.Clicked -= OnClicked;
        _icon.Dispose();
    }

    private void OnClicked(object? sender, EventArgs e) => Clicked?.Invoke(this, e);
}
