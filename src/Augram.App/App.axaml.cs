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
    private readonly InstanceStartup? _startup;
    private AppTray? _tray;
    private AppHealthContributor? _health;

    /// <summary>The headless tests' app. Keeps the one-argument public signature the XAML runtime loader looks for (AVLN3001).</summary>
    public App(IServiceProvider services)
        : this(services, null)
    {
    }

    /// <param name="services">The composition root's provider; nothing in it is resolved before this launch may start.</param>
    /// <param name="startup">The launch's single-instance state (<see cref="Program"/>); null in the headless tests.</param>
    internal App(IServiceProvider services, InstanceStartup? startup)
    {
        _services = services;
        _startup = startup;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Styles.Add(ThemeSelector.Load(ThemeSelector.Active));
        ClickAwayFocus.Register();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Closing the window hides it (F7); only Quit ends the process. Also keeps the process alive while a take-over dialog closes.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            if (_startup is { NeedsChoice: true } startup)
            {
                ChooseThenStart(desktop, startup);
            }
            else
            {
                StartDesktop(desktop);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// A different build is running (<see cref="InstanceStartup.Begin"/>): ask, and start only once this launch holds the
    /// single-instance name. async void on purpose: an exception here is an unhandled one on the UI thread and ends the
    /// process like any other startup failure, instead of leaving a launch with no window and no tray.
    /// </summary>
    private async void ChooseThenStart(IClassicDesktopStyleApplicationLifetime desktop, InstanceStartup startup)
    {
        if (await startup.ChooseAsync(_services.GetRequiredService<ITakeOverPresenter>()).ConfigureAwait(true))
        {
            StartDesktop(desktop);
            // The lifetime showed MainWindow when it started, while it was still null: show it now, as a plain launch would.
            ShowMainWindow();
        }
        else
        {
            desktop.Shutdown();
        }
    }

    private void StartDesktop(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var log = _services.GetRequiredService<IEventLog>();
        var app = _services.GetRequiredService<AppInfo>();
        log.Info(LogSource, "App started",
            ("version", app.Version),
            ("commit", app.Commit),
            ("channel", app.Channel),
            ("theme", ThemeSelector.Active),
            ("os", Environment.OSVersion.VersionString));
        // What happened before the log existed: a take-over of another build, a wait for a silent one.
        _startup?.LogNotes(log);
        _health = _services.GetRequiredService<AppHealthContributor>();
#if DEBUG
        if (desktop.Args?.Contains("--gallery", StringComparer.Ordinal) == true)
        {
            _services.GetRequiredService<ViewModels.MainWindowViewModel>().InitialTabKey = DevGallery.GalleryNavigation.Key;
        }
#endif
        // Gesture pictures follow the trail colour; set before the first window builds its glyphs.
        GlyphColourLink.Follow(_services.GetRequiredService<Core.Config.SettingsStore>(), Resources);
        desktop.MainWindow = _services.GetRequiredService<MainWindow>();
        if (OperatingSystem.IsMacOS())
        {
            MacDockPresence.Follow(desktop.MainWindow);
        }

        EngineModule.Start(_services);
        // Sync starts also under --no-engine: it never touches input.
        SyncModule.Start(_services);
        _tray = new AppTray(_services.GetRequiredService<AppState>(), ShowMainWindow, () => desktop.Shutdown(), log, _services.GetService<SyncService>(), _services.GetService<EnginePauseState>());
        if (_startup?.Guard is { } guard)
        {
            ListenToOtherLaunches(guard, desktop, log);
        }

        desktop.Exit += (_, _) =>
        {
            log.Info(LogSource, "App stopping");
            _tray?.Dispose();
            _health?.Dispose();
        };
    }

    /// <summary>
    /// The running side of one Augram at a time: a second launch of this install shows the window, a different build that the
    /// user chose to start instead quits this one as the tray's Quit does (the single-instance name is released last, in
    /// <see cref="Program"/>). Raised on the guard's listener thread; logged there, acted on the UI thread.
    /// </summary>
    private void ListenToOtherLaunches(SingleInstanceGuard guard, IClassicDesktopStyleApplicationLifetime desktop, IEventLog log)
    {
        guard.Introduced += (_, e) => log.Info(LogSource, "Another Augram is starting", Who(e));
        guard.ShowRequested += (_, e) =>
        {
            log.Info(LogSource, "Asked to show the window by another launch", Who(e));
            Dispatcher.UIThread.Post(ShowMainWindow);
        };
        guard.QuitRequested += (_, e) =>
        {
            log.Info(LogSource, "Asked to quit by another Augram", Who(e));
            Dispatcher.UIThread.Post(() => desktop.Shutdown());
        };
    }

    private static LogProperty[] Who(InstanceRequestEventArgs e) => e.From is { } from
        ? [("from", from.App.Describe()), ("channel", from.App.Channel), ("version", from.App.InformationalVersion), ("path", from.ExecutablePath), ("reason", e.Reason)]
        : [("from", "an Augram from before identities")];

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
