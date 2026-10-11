using Augram.Core.Config;

namespace Augram.App.Hosting;

/// <summary>
/// Follows one part of the settings (<c>select</c>) on <see cref="SettingsStore.Changed"/>: it remembers the last value, and when
/// a change leaves it different (record equality) hands <c>apply</c> the last and the new value, so the follower pushes only
/// the fields that moved. Runs on the thread that raised the change (the writer thread). Disposing unsubscribes.
/// Used by <see cref="EngineSettingsLink"/> (the engine), <see cref="AppState"/> (tray flags, start at login) and the
/// Commands view model (stroke button, drag distance).
/// </summary>
public sealed class SettingsFollower<T> : IDisposable
{
    private readonly SettingsStore _settings;
    private readonly Func<Settings, T> _select;
    private readonly Action<T, T> _apply;
    private T _last;

    /// <param name="settings">The store to follow.</param>
    /// <param name="select">The part followed; compared with <see cref="EqualityComparer{T}.Default"/>.</param>
    /// <param name="apply">Gets the last value and the new one; called only when they differ.</param>
    public SettingsFollower(SettingsStore settings, Func<Settings, T> select, Action<T, T> apply)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(select);
        ArgumentNullException.ThrowIfNull(apply);
        _settings = settings;
        _select = select;
        _apply = apply;
        _last = select(settings.Current);
        settings.Changed += OnChanged;
    }

    /// <summary>The value as last seen.</summary>
    public T Current => _last;

    public void Dispose() => _settings.Changed -= OnChanged;

    private void OnChanged(object? sender, EventArgs e)
    {
        var next = _select(_settings.Current);
        var last = _last;
        _last = next;
        if (!EqualityComparer<T>.Default.Equals(next, last))
        {
            _apply(last, next);
        }
    }
}
