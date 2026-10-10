using Augram.Core.Abstractions;
using Augram.Core.Config;
using Augram.Core.Mapping;
using Augram.Engine.Hosting;

namespace Augram.App.Hosting;

/// <summary>
/// Pushes settings changes into the running engine (ADR-0002 §5a: the store is the state, the engine
/// follows it). Subscribed on the writer thread; each changed field goes through the host's own
/// channel: <c>Enabled</c> and <c>IgnoreKey</c> are volatile writes, the stroke button and the capture
/// thresholds ride the worker queue. Recognition options need no push: the host reads them through
/// a delegate on every stroke. Undo and redo arrive here like any other change. A mapping change (an edit,
/// undo, a sync) tells the host too (<see cref="EngineHost.MappingChanged"/>), so the ignore list's answer
/// follows an edit on the Exclusions tab without waiting for the pointer to move; the executor needs no push, it
/// reads the snapshot per stroke.
/// </summary>
public sealed class EngineSettingsLink : IDisposable
{
    private readonly SettingsStore _settings;
    private readonly MappingStore _mapping;
    private readonly EngineHost _host;
    private Settings _last;

    public EngineSettingsLink(SettingsStore settings, MappingStore mapping, EngineHost host)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(host);
        _settings = settings;
        _mapping = mapping;
        _host = host;
        _last = settings.Current;
        settings.Changed += OnChanged;
        mapping.Changed += OnMappingChanged;
    }

    /// <summary>The ignore-key set as the engine's modifier mask; the two enums share their bits.</summary>
    public static KeyModifiers ToModifiers(IgnoreKeys keys) => (KeyModifiers)(int)keys;

    public void Dispose()
    {
        _settings.Changed -= OnChanged;
        _mapping.Changed -= OnMappingChanged;
    }

    private void OnMappingChanged(object? sender, EventArgs e) => _host.MappingChanged();

    private void OnChanged(object? sender, EventArgs e)
    {
        var next = _settings.Current;
        var last = _last;
        _last = next;

        if (next.General.Enabled != last.General.Enabled)
        {
            _host.Enabled = next.General.Enabled;
        }

        if (next.General.StrokeButton != last.General.StrokeButton)
        {
            _host.StrokeButton = next.General.StrokeButton;
        }

        if (next.General.IgnoreKey != last.General.IgnoreKey)
        {
            _host.IgnoreKey = ToModifiers(next.General.IgnoreKey);
        }

        if (next.Capture != last.Capture)
        {
            _host.SetThresholds(next.Capture);
        }
    }
}
