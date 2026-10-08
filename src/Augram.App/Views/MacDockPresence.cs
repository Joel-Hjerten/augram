using System.Runtime.Versioning;
using Augram.Platform.MacOS;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Augram.App.Views;

/// <summary>
/// macOS (Joel, 2026-10-08): the Dock icon is there only while the main window is open. Closing or minimizing the window
/// hides it to the menu bar (F7: closed = keeps running in the tray), and the Dock icon goes with it; Open in the tray menu,
/// or a second launch, shows both again. Quit is the only way out. Windows is unchanged: minimize goes to the taskbar.
/// </summary>
[SupportedOSPlatform("macos")]
internal static class MacDockPresence
{
    public static void Follow(Window window)
    {
        ArgumentNullException.ThrowIfNull(window);
        window.PropertyChanged += (_, e) =>
        {
            if (e.Property == Visual.IsVisibleProperty)
            {
                MacDock.SetShown(window.IsVisible);
            }
            else if (e.Property == Window.WindowStateProperty && window.WindowState == WindowState.Minimized)
            {
                // After the state change has settled; showing the window again restores it (App.ShowMainWindow).
                Dispatcher.UIThread.Post(window.Hide);
            }
        };
    }
}
