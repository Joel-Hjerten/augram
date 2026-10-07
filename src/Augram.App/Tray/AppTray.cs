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
/// window, the menu has Open · Enabled · Start at login · Sync now (only while a sync repository is set, F8) · Quit,
/// and the tooltip ends with the last sync ("synced 14:32"). Single versus double is decided by
/// <see cref="ClickDiscriminator"/> with a <see cref="DispatcherTimer"/>, which means a single click
/// takes effect only after the double-click window (250 ms) has passed; Avalonia offers no better signal.
/// </summary>
public sealed class AppTray : IDisposable
{
    private const string LogSource = "app";

    private readonly AppState _state;
    private readonly Action _open;
    private readonly IEventLog _log;
    private readonly TrayIconSet _icons;
    private readonly TrayIcon _icon;
    private readonly ClickDiscriminator _clicks = new();
    private readonly DispatcherTimer _timer;
    private readonly NativeMenuItem _enabledItem;
    private readonly NativeMenuItem _startAtLoginItem;
    private readonly SyncTrayItem? _sync;

    /// <remarks><c>sync</c> is the sync service, for Sync now and the tooltip; null leaves both out.</remarks>
    public AppTray(AppState state, Action open, Action quit, IEventLog log, SyncService? sync = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(quit);
        ArgumentNullException.ThrowIfNull(log);
        _state = state;
        _open = open;
        _log = log;
        _icons = TrayIconSet.Load();

        _enabledItem = new NativeMenuItem("Enabled") { ToggleType = NativeMenuItemToggleType.CheckBox };
        _enabledItem.Click += (_, _) => _state.Toggle();
        _startAtLoginItem = new NativeMenuItem("Start at login") { ToggleType = NativeMenuItemToggleType.CheckBox };
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
        Sync();

        var icons = new TrayIcons { _icon };
        TrayIcon.SetIcons(Application.Current!, icons);
    }

    public void Dispose()
    {
        _timer.Stop();
        _sync?.Dispose();
        _state.PropertyChanged -= OnStateChanged;
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

        Sync();
    }

    private void Sync()
    {
        _icon.Icon = _state.Enabled ? _icons.Enabled : _icons.Disabled;
        var tip = _state.Enabled ? "Augram (enabled)" : "Augram (disabled)";
        _icon.ToolTipText = _sync?.ShortStatus is { } sync ? $"{tip} · {sync}" : tip;
        _enabledItem.IsChecked = _state.Enabled;
        _startAtLoginItem.IsChecked = _state.StartAtLogin;
    }
}
