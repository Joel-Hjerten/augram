using Augram.App.Hosting;
using Augram.App.Sync;
using Avalonia.Controls;

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

    /// <remarks>The item goes in <c>menu</c> just above its first separator. <c>marshal</c> runs the action on the UI thread; null is <see cref="UiThread.Post"/>.</remarks>
    public SyncTrayItem(SyncService service, NativeMenu menu, Action<Action>? marshal = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _menu = menu ?? throw new ArgumentNullException(nameof(menu));
        _marshal = marshal ?? UiThread.Post;
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

    /// <summary>For the tooltip (<see cref="SyncStatusText.Short"/>); null while sync is off or before the first run.</summary>
    public string? ShortStatus { get; private set; }

    /// <summary>Re-reads the service; the constructor and every service change call it.</summary>
    public void Refresh()
    {
        var status = _service.Snapshot;
        var configured = status.IsConfigured;
        if (configured && !IsShown)
        {
            var separator = _menu.Items.ToList().FindIndex(item => item is NativeMenuItemSeparator);
            _menu.Items.Insert(separator >= 0 ? separator : _menu.Items.Count, Item);
        }
        else if (!configured && IsShown)
        {
            _menu.Items.Remove(Item);
        }

        Item.IsEnabled = !status.IsRunning;
        ShortStatus = SyncStatusText.Short(status);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose() => _service.Changed -= OnServiceChanged;

    private void OnServiceChanged(object? sender, EventArgs e) => _marshal(Refresh);
}
