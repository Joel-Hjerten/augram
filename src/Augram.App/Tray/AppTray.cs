using System.ComponentModel;
using Augram.App.Hosting;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Augram.App.Tray;

/// <summary>
/// The tray presence (F7): single click toggles <see cref="AppState.Enabled"/> (the persisted setting the engine follows), double click opens the
/// window, the menu has Open · Enabled · Start at login (disabled in a development build, <see cref="AppState.CanChangeStartAtLogin"/>)
/// · Sync now (only while a sync repository is set, F8) · Quit, and the tooltip starts with the build's name and ends with the
/// last sync ("Augram (Dev) (enabled) · synced 14:32", <see cref="ToolTipFor"/>); while a "disable while focused" app has focus
/// it says so instead of "enabled" ("Augram (paused: VMware is focused)", <see cref="EnginePauseState"/>) and the icon is the disabled one, while the Enabled check box keeps showing the setting. Single versus double is decided by
/// <see cref="ClickDiscriminator"/> with a <see cref="DispatcherTimer"/>, which means a single click
/// takes effect only after the double-click window (250 ms) has passed; Avalonia offers no better signal.
/// </summary>
public sealed class AppTray : IDisposable
{
    private const string LogSource = "app";

    private readonly AppState _state;
    private readonly Action _open;
    private readonly IEventLog _log;
    private readonly TrayIcon _icon;
    private TrayIconSet _icons;
    private readonly ClickDiscriminator _clicks = new();
    private readonly DispatcherTimer _timer;
    private readonly NativeMenuItem _enabledItem;
    private readonly NativeMenuItem _startAtLoginItem;
    private readonly SyncTrayItem? _sync;
    private readonly EnginePauseState? _pause;

    /// <remarks><c>sync</c> is the sync service, for Sync now and the tooltip; null leaves both out. <c>pause</c> is the
    /// ignore list's pause for the tooltip; null leaves it out.</remarks>
    public AppTray(AppState state, Action open, Action quit, IEventLog log, SyncService? sync = null, EnginePauseState? pause = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(quit);
        ArgumentNullException.ThrowIfNull(log);
        _state = state;
        _open = open;
        _log = log;
        _icons = TrayIconSet.Load(_state.ColourMenuBarIcon);

        _enabledItem = new NativeMenuItem("Enabled") { ToggleType = NativeMenuItemToggleType.CheckBox };
        _enabledItem.Click += (_, _) => _state.Toggle();
        _startAtLoginItem = new NativeMenuItem("Start at login") { ToggleType = NativeMenuItemToggleType.CheckBox, IsEnabled = _state.CanChangeStartAtLogin };
        _startAtLoginItem.Click += (_, _) => _state.StartAtLogin = !_state.StartAtLogin;
        var openItem = new NativeMenuItem("Open");
        openItem.Click += (_, _) => _open();
        var quitItem = new NativeMenuItem("Quit");
        quitItem.Click += (_, _) => quit();

        var menu = new NativeMenu();
        menu.Items.Add(openItem);
        menu.Items.Add(_enabledItem);
        menu.Items.Add(_startAtLoginItem);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(quitItem);
        if (sync is not null)
        {
            _sync = new SyncTrayItem(sync, menu);
            _sync.Changed += (_, _) => Sync();
        }

        _icon = new TrayIcon { Menu = menu, IsVisible = true };
        MacOSProperties.SetIsTemplateIcon(_icon, _icons.IsTemplate);
        _icon.Clicked += OnClicked;
        _timer = new DispatcherTimer { Interval = _clicks.Window };
        _timer.Tick += OnTick;
        _state.PropertyChanged += OnStateChanged;
        _pause = pause;
        if (_pause is not null)
        {
            _pause.PropertyChanged += OnPauseChanged;
        }

        Sync();

        var icons = new TrayIcons { _icon };
        TrayIcon.SetIcons(Application.Current!, icons);
    }

    public void Dispose()
    {
        _timer.Stop();
        _sync?.Dispose();
        _state.PropertyChanged -= OnStateChanged;
        if (_pause is not null)
        {
            _pause.PropertyChanged -= OnPauseChanged;
        }

        _icon.Clicked -= OnClicked;
        _icon.Dispose();
    }

    private void OnClicked(object? sender, EventArgs e)
    {
        if (_clicks.Click(DateTimeOffset.UtcNow) == ClickKind.Double)
        {
            _timer.Stop();
            _open();
            return;
        }

        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_clicks.Flush(DateTimeOffset.UtcNow) != ClickKind.Single)
        {
            return;
        }

        _timer.Stop();
        _state.Toggle();
    }

    private void OnStateChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The engine logs "Engine enabled" / "Engine disabled" itself; here only the icon follows the store.
        if (e.PropertyName == nameof(AppState.Enabled))
        {
            _log.Debug(LogSource, "Tray icon updated", ("enabled", _state.Enabled));
        }

        if (e.PropertyName == nameof(AppState.ColourMenuBarIcon))
        {
            // Template first, then the image: the menu bar reads the flag when the image is set.
            _icons = TrayIconSet.Load(_state.ColourMenuBarIcon);
            MacOSProperties.SetIsTemplateIcon(_icon, _icons.IsTemplate);
        }

        Sync();
    }

    private void OnPauseChanged(object? sender, PropertyChangedEventArgs e) => Sync();

    private void Sync()
    {
        _icon.Icon = ShowsEnabledIcon(_state.Enabled, _pause?.PausedBy) ? _icons.Enabled : _icons.Disabled;
        _icon.ToolTipText = ToolTipFor(_state.App, _state.Enabled, _sync?.ShortStatus, _pause?.PausedBy);
        _enabledItem.IsChecked = _state.Enabled;
        _startAtLoginItem.IsChecked = _state.StartAtLogin;
    }

    /// <summary>
    /// "Augram (enabled)", "Augram (Dev) (disabled) · synced 14:32", "Augram (paused: VMware is focused)": the build's name, the
    /// state (paused while enabled and a "disable while focused" app named by <paramref name="pausedBy"/> has focus), the last
    /// sync when there is one.
    /// </summary>
    /// <summary>
    /// The enabled icon only while Augram is enabled and not paused for a "disable while focused" app (Joel, 2026-10-09:
    /// a pause shows the disabled icon too); the Enabled menu check box keeps showing the setting itself.
    /// </summary>
    public static bool ShowsEnabledIcon(bool enabled, string? pausedBy) => enabled && pausedBy is null;

    public static string ToolTipFor(AppInfo app, bool enabled, string? syncStatus, string? pausedBy = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        var state = !enabled ? "disabled" : pausedBy is { Length: > 0 } paused ? $"paused: {paused} is focused" : "enabled";
        var tip = $"{app.Label} ({state})";
        return syncStatus is { Length: > 0 } sync ? $"{tip} · {sync}" : tip;
    }
}
