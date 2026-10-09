using Avalonia.Controls;

namespace Augram.App.Tray;

/// <summary>
/// The platform's tray or menu-bar item under <see cref="AppTray"/>: shows the icon and tooltip, offers the menu on the
/// platform's gesture for it (right click), and raises <see cref="Clicked"/> once per left click so the tray can tell a
/// single click (toggle) from a double click (open) the same way everywhere. <see cref="AvaloniaTrayHost"/> on Windows and
/// Linux, <see cref="MacTrayHost"/> on macOS, where Avalonia's item opens the menu on every click and reports none.
/// </summary>
public interface ITrayHost : IDisposable
{
    event EventHandler? Clicked;

    /// <summary>The icon, whether macOS should tint it as a template, and the tooltip; called whenever any of them changes.</summary>
    void Show(WindowIcon icon, bool isTemplate, string toolTip);

    /// <summary>The host for this platform, offering <paramref name="menu"/>.</summary>
    static ITrayHost Create(NativeMenu menu) => OperatingSystem.IsMacOS() ? new MacTrayHost(menu) : new AvaloniaTrayHost(menu);
}
