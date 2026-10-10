using System.Runtime.Versioning;
using Augram.App.Hosting;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Platform.MacOS;
using Augram.Platform.MacOS.Startup;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

namespace Augram.App;

/// <summary>
/// A launch's first window (F7 start at login, 2026-10-10): the main window, or none with Augram in the tray and menu bar only
/// (<see cref="LaunchVisibility"/>: <c>--hidden</c>, or a macOS login-item launch). Everything else starts either way.
/// </summary>
public sealed partial class App
{
    /// <summary>How long a macOS launch waits for its launch event before it shows the window anyway.</summary>
    internal static readonly TimeSpan LaunchEventTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Avalonia 11.3's <c>ClassicDesktopStyleApplicationLifetime</c> shows <c>MainWindow</c> once, in <c>Start</c>, after this
    /// framework initialisation and right before its main loop (<c>StartCore</c>: the <c>Startup</c> event, then
    /// <c>MainWindow?.Show()</c>, then <c>Dispatcher.UIThread.MainLoop</c>), and has no switch to skip it. A launch that opens
    /// with its window hands it over now, as always. One that starts hidden, or does not know yet (macOS until its launch
    /// event), hands it over as the main loop's first job instead, so that one show finds no window and nothing flashes.
    /// Nothing reads <c>MainWindow</c> in between (and the presenters that use it as an owner cope with none, as with a hidden one).
    /// </summary>
    private static void HandOver(IClassicDesktopStyleApplicationLifetime desktop, Window window, bool now)
    {
        if (now)
        {
            desktop.MainWindow = window;
        }
        else
        {
            Dispatcher.UIThread.Post(() => desktop.MainWindow = window, DispatcherPriority.Send);
        }
    }

    /// <summary>Shows the window when the launch wants it and the lifetime did not show it; a hidden launch leaves the tray (and on macOS the menu bar) only.</summary>
    private void OpenAsLaunched(LaunchVisibility launch, bool showsItself)
    {
        if (!launch.Hidden)
        {
            if (showsItself)
            {
                ShowMainWindow();
            }

            return;
        }

        // macOS: the bundle starts with a Dock icon (LSUIElement unset); with no window it goes, as when the window closes
        // (MacDockPresence). Not when a reopen has shown the window meanwhile.
        if (OperatingSystem.IsMacOS() && (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow is not { IsVisible: true })
        {
            MacDock.SetShown(false);
        }
    }

    /// <summary>macOS, before the main loop: the event that tells a login-item launch arrives inside it (<see cref="MacLaunchEvent"/>).</summary>
    [SupportedOSPlatform("macos")]
    private void ObserveLaunchEvent() => _launchEvent = MacLaunchEvent.Observe();

    /// <summary>
    /// macOS: decides once launching has finished, then shows the window or drops the Dock icon (<see cref="OpenAsLaunched"/>;
    /// the lifetime never showed it, <see cref="HandOver"/>). <see cref="LaunchEventTimeout"/> is a safety net, not a path:
    /// without an answer a user's launch would have no window. Its timer starts from the main loop, so a slow start before the
    /// loop cannot use it up before the launch event had its chance.
    /// </summary>
    [SupportedOSPlatform("macos")]
    private void WaitForLaunchEvent(IReadOnlyList<string> args, IEventLog log)
    {
        var decided = false;
        void Decide(bool loginItem, string launchEvent)
        {
            if (decided)
            {
                return;
            }

            decided = true;
            var launch = LaunchVisibility.Decide(args, loginItem);
            log.Info(LogSource, "Launch window decided", [.. launch.LogProperties, ("event", launchEvent)]);
            OpenAsLaunched(launch, showsItself: true);
        }

        ((MacLaunchEvent)_launchEvent!).WhenKnown(known => Dispatcher.UIThread.Post(() => Decide(known.IsLoginItem, known.Event)));
        Dispatcher.UIThread.Post(() => DispatcherTimer.RunOnce(
            () =>
            {
                if (!decided)
                {
                    log.Warning(LogSource, "No launch event arrived; the window opens", ("waitedMs", (int)LaunchEventTimeout.TotalMilliseconds));
                    Decide(loginItem: false, "none");
                }
            },
            LaunchEventTimeout));
    }

    /// <summary>
    /// macOS: opening Augram again from Finder, Launchpad, Spotlight or the Dock reaches the running Augram as a reopen, not as a
    /// second launch (the single-instance guard never hears of it): show the window, as a second launch does. Never raised elsewhere.
    /// </summary>
    private void ShowOnReopen(IEventLog log)
    {
        if (TryGetFeature(typeof(IActivatableLifetime)) is not IActivatableLifetime activatable)
        {
            return;
        }

        activatable.Activated += (_, e) =>
        {
            if (e.Kind == ActivationKind.Reopen)
            {
                // Raised from inside AppKit's handling of the reopen event; shown from the loop, as a second launch's request is.
                log.Info(LogSource, "Asked to show the window by a reopen");
                Dispatcher.UIThread.Post(ShowMainWindow);
            }
        };
    }
}
