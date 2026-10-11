using Augram.App.Dialogs;
using Augram.App.Hosting;
using Augram.App.Hosting.Instance;
using Augram.App.Hosting.Launch;
using Augram.App.Hosting.Modules;
using Augram.App.Themes;
using Augram.App.Tray;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;

namespace Augram.App;

public sealed partial class App : Application
{
    private readonly IServiceProvider _services;
    private readonly InstanceStartup? _startup;
    private AppTray? _tray;
    private AppHealthContributor? _health;
    private MainWindowOpener? _opener;

    /// <summary>macOS: the launch event, observed from before the main loop (<see cref="ObserveLaunchEvent"/>); null elsewhere.</summary>
    private IDisposable? _launchEvent;

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
            if (OperatingSystem.IsMacOS())
            {
                ObserveLaunchEvent();
            }

            if (_startup is { NeedsChoice: true } startup)
            {
                ChooseThenStart(desktop, startup);
            }
            else
            {
                StartDesktop(desktop, lifetimeShowsWindow: true);
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
        if (await startup.ChooseAsync(_services.GetRequiredKeyedService<IConfirmPresenter>(ConfirmPresenter.AloneKey)).ConfigureAwait(true))
        {
            // The lifetime showed MainWindow when it started, while it was still null: this launch shows it itself (unless hidden).
            StartDesktop(desktop, lifetimeShowsWindow: false);
        }
        else
        {
            desktop.Shutdown();
        }
    }

    /// <param name="desktop">The lifetime.</param>
    /// <param name="lifetimeShowsWindow">True while the lifetime's own show of MainWindow is still ahead (a plain launch); false after a take-over choice.</param>
    private void StartDesktop(IClassicDesktopStyleApplicationLifetime desktop, bool lifetimeShowsWindow)
    {
        var log = _services.GetRequiredService<IEventLog>();
        var app = _services.GetRequiredService<AppInfo>();
        var arguments = _services.GetRequiredService<LaunchArguments>();
        // Windows: known now. macOS: --hidden is; a login-item launch only once launching has finished (LaunchVisibility.AtStart).
        var launch = LaunchVisibility.AtStart(arguments.Hidden, waitsForLaunchEvent: _launchEvent is not null);
        log.Info(LogSources.App, "App started",
        [
            ("version", app.Version),
            ("commit", app.Commit),
            ("channel", app.Channel),
            ("theme", ThemeSelector.Active),
            ("os", Environment.OSVersion.VersionString),
            .. launch?.LogProperties ?? [("hidden", "pending")],
        ]);
        // What happened before the log existed: a take-over of another build, a wait for a silent one.
        _startup?.LogNotes(log);
        _health = _services.GetRequiredService<AppHealthContributor>();
#if DEBUG
        if (arguments.Gallery)
        {
            _services.GetRequiredService<ViewModels.MainWindowViewModel>().InitialTabKey = DevGallery.GalleryNavigation.Key;
        }
#endif
        // The look follows Options › Appearance and the trail colour (plan 0006); set before the first window builds its glyphs.
        var (window, appearance) = MainWindowFactory.Create(_services, this);
        // Dialogs get the same backdrop (plan 0006 decision 14): every window with an owner, when it loads. Never the trail
        // overlay (CLAUDE.md invariant 6): it has no owner, and it is excluded by type as well.
        Control.LoadedEvent.AddClassHandler<Window>((dialog, _) =>
        {
            if (dialog.Owner is not null && dialog is not Overlay.TrailOverlayWindow)
            {
                appearance.Attach(dialog);
            }
        });
        HandOver(desktop, window, now: !lifetimeShowsWindow || (_launchEvent is null && launch is { Hidden: false }));
        if (OperatingSystem.IsMacOS())
        {
            ShowOwnDockIconWhenUnbundled();
            MacDockPresence.Follow(window);
        }

        // The Open app step's "This app (Augram)" opens the window as the tray does.
        _opener = _services.GetRequiredService<MainWindowOpener>();
        _opener.Attach();

        var engineRuns = EngineModule.Start(_services);
        FormServices.Publish(_services, this, engineRuns);
        // Sync starts also under --no-engine: it never touches input.
        SyncModule.Start(_services);
        _tray = _services.GetRequiredService<AppTray>();
        if (_startup?.Listener is { } listener)
        {
            ListenToOtherLaunches(listener, desktop, log);
        }

        ShowOnReopen(log);
        desktop.Exit += (_, _) =>
        {
            log.Info(LogSources.App, "App stopping");
            _tray?.Dispose();
            _health?.Dispose();
            _launchEvent?.Dispose();
        };

        if (OperatingSystem.IsMacOS() && _launchEvent is not null)
        {
            WaitForLaunchEvent(arguments.Hidden, log);
        }
        else
        {
            OpenAsLaunched(launch ?? LaunchVisibility.Shown, showsItself: !lifetimeShowsWindow);
        }
    }

    /// <summary>
    /// The running side of one Augram at a time: a second launch of this install shows the window, a different build that the
    /// user chose to start instead quits this one as the tray's Quit does (the single-instance name is released last, in
    /// <see cref="Program"/>). Raised on the listener's thread; logged there, acted on the UI thread.
    /// </summary>
    private void ListenToOtherLaunches(InstanceListener listener, IClassicDesktopStyleApplicationLifetime desktop, IEventLog log)
    {
        listener.Introduced += (_, e) => log.Info(LogSources.App, "Another Augram is starting", Who(e));
        listener.ShowRequested += (_, e) =>
        {
            log.Info(LogSources.App, "Asked to show the window by another launch", Who(e));
            Dispatcher.UIThread.Post(ShowMainWindow);
        };
        listener.QuitRequested += (_, e) =>
        {
            log.Info(LogSources.App, "Asked to quit by another Augram", Who(e));
            Dispatcher.UIThread.Post(() => desktop.Shutdown());
        };
    }

    private static LogProperty[] Who(InstanceRequestEventArgs e) => e.From is { } from
        ? [("from", from.App.Describe()), ("channel", from.App.Channel), ("version", from.App.InformationalVersion), ("path", from.ExecutablePath), ("reason", e.Reason)]
        : [("from", "an Augram from before identities")];

    /// <summary>A development build runs as dotnet, which the Dock shows as "exec"; it gets Augram's icon instead (Joel, 2026-10-09).</summary>
    [System.Runtime.Versioning.SupportedOSPlatform("macos")]
    private static void ShowOwnDockIconWhenUnbundled()
    {
        if (Platform.MacOS.MacDock.IsBundled(Environment.ProcessPath))
        {
            return;
        }

        using var icon = Avalonia.Platform.AssetLoader.Open(new Uri("avares://Augram/Icons/augram.icns"));
        using var bytes = new MemoryStream();
        icon.CopyTo(bytes);
        Platform.MacOS.MacDock.SetIcon(bytes.ToArray());
    }

    private void ShowMainWindow() => _opener?.ShowNow();
}
