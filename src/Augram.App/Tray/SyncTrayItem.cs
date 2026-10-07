using System.Globalization;
using Augram.App.Hosting;
using Augram.Core.Sync;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Augram.App.Tray;

/// <summary>
/// The tray menu's "Sync now" (F8 sync): in the menu only while a repository is set (added and removed, not hidden,
/// so every platform's tray menu agrees), disabled while a run is going, and a short status for the tray tooltip
/// ("synced 14:32", "sync failed 14:32", "sync paused"). Follows <see cref="SyncService.Changed"/>, marshalled to the
/// UI thread; <see cref="Changed"/> tells the tray to refresh its tooltip.
/// </summary>
public sealed class SyncTrayItem : IDisposable
{
    private readonly SyncService _service;
    private readonly NativeMenu _menu;
    private readonly Action<Action> _marshal;

    /// <remarks>The item goes in <c>menu</c> just above its first separator. <c>marshal</c> runs the action on the UI thread; null is <c>Dispatcher.UIThread.Post</c>.</remarks>
    public SyncTrayItem(SyncService service, NativeMenu menu, Action<Action>? marshal = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _menu = menu ?? throw new ArgumentNullException(nameof(menu));
        _marshal = marshal ?? (action => Dispatcher.UIThread.Post(action));
        Item = new NativeMenuItem("Sync now");
        Item.Click += (_, _) => _service.SyncNow();
        _service.Changed += OnServiceChanged;
        Refresh();
    }

    /// <summary>Raised on the UI thread after the item changed.</summary>
    public event EventHandler? Changed;

    public NativeMenuItem Item { get; }

    /// <summary>True while the item is in the menu.</summary>
    public bool IsShown => _menu.Items.Contains(Item);

    /// <summary>For the tooltip; null while sync is off.</summary>
    public string? ShortStatus { get; private set; }

    /// <summary>Re-reads the service; the constructor and every service change call it.</summary>
    public void Refresh()
    {
        var configured = _service.IsConfigured;
        if (configured && !IsShown)
        {
            var separator = _menu.Items.ToList().FindIndex(item => item is NativeMenuItemSeparator);
            _menu.Items.Insert(separator >= 0 ? separator : _menu.Items.Count, Item);
        }
        else if (!configured && IsShown)
        {
            _menu.Items.Remove(Item);
        }

        Item.IsEnabled = !_service.IsRunning;
        ShortStatus = Short(configured, _service.IsRunning, _service.IsPaused, _service.LastReport);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => _service.Changed -= OnServiceChanged;

    /// <summary>"synced 14:32", "sync failed 14:32", "sync paused", "syncing…"; null while off or before the first run.</summary>
    public static string? Short(bool configured, bool running, bool paused, SyncReport? report)
    {
        if (!configured)
        {
            return null;
        }

        if (running)
        {
            return "syncing…";
        }

        if (paused || report?.Status == SyncStatus.NeedsJoinChoice)
        {
            return "sync paused";
        }

        if (report is null || report.Status == SyncStatus.Off)
        {
            return null;
        }

        var at = report.When.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
        return report.Status == SyncStatus.Failed ? $"sync failed {at}" : $"synced {at}";
    }

    private void OnServiceChanged(object? sender, EventArgs e) => _marshal(Refresh);
}
