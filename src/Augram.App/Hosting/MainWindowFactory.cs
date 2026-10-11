using Augram.App.Themes;
using Augram.App.Views;
using Augram.Core.Config;
using Avalonia;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App.Hosting;

/// <summary>
/// The main window as both the app and <c>tools/UiShots</c> make it: the look follows Options › Appearance and the trail
/// colour (<see cref="AppearanceLink"/>, plan 0006), linked before the window builds its glyphs, then the window from the
/// service provider with that look attached. The app goes on to attach the same look to its dialogs.
/// </summary>
public static class MainWindowFactory
{
    public static (MainWindow Window, AppearanceLink Appearance) Create(IServiceProvider services, Application app)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(app);
        var appearance = AppearanceLink.Follow(services.GetRequiredService<SettingsStore>(), app);
        var window = services.GetRequiredService<MainWindow>();
        appearance.Attach(window);
        return (window, appearance);
    }
}
