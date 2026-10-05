using Augram.App.Hosting;
using Augram.App.Themes;
using Augram.App.Tray;
using Augram.App.Views;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App;

public sealed class App : Application
{
    private const string LogSource = "app";
    private readonly IServiceProvider _services;
    private AppTray? _tray;
    private AppHealthContributor? _health;

    public App(IServiceProvider services)
    {
        _services = services;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Styles.Add(ThemeSelector.Load(ThemeSelector.Active));
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            StartDesktop(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void StartDesktop(IClassicDesktopStyleApplicationLifetime desktop)
    {
        // Closing the window hides it (F7); only Quit ends the process.
        desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var log = _services.GetRequiredService<IEventLog>();
        _health = _services.GetRequiredService<AppHealthContributor>();
#if DEBUG
        if (desktop.Args?.Contains("--gallery", StringComparer.Ordinal) == true)
        {
            _services.GetRequiredService<ViewModels.MainWindowViewModel>().InitialTabKey = DevGallery.GalleryNavigation.Key;
        }
#endif
        desktop.MainWindow = _services.GetRequiredService<MainWindow>();
        _tray = new AppTray(_services.GetRequiredService<AppState>(), ShowMainWindow, () => desktop.Shutdown(), log);
        if (_services.GetService<SingleInstanceGuard>() is { } guard)
        {
            guard.ShowRequested += (_, _) => Dispatcher.UIThread.Post(ShowMainWindow);
        }

        desktop.Exit += (_, _) =>
        {
            log.Info(LogSource, "App stopping");
            _tray?.Dispose();
            _health?.Dispose();
        };
        log.Info(LogSource, "App started", ("theme", ThemeSelector.Active), ("os", Environment.OSVersion.VersionString));
    }

    private void ShowMainWindow()
    {
        if ((ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is not { } window)
        {
            return;
        }

        window.Show();
        if (window.WindowState == WindowState.Minimized)
        {
            window.WindowState = WindowState.Normal;
        }

        window.Activate();
    }
}
