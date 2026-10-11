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
    private readonly SettingsFollower<Settings> _settings;
    private readonly MappingStore _mapping;
    private readonly EngineHost _host;

    public EngineSettingsLink(SettingsStore settings, MappingStore mapping, EngineHost host)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(host);
        _mapping = mapping;
        _host = host;
        _settings = new SettingsFollower<Settings>(settings, current => current, OnChanged);
        mapping.Changed += OnMappingChanged;
    }

    public void Dispose()
    {
        _settings.Dispose();
        _mapping.Changed -= OnMappingChanged;
    }

    private void OnMappingChanged(object? sender, EventArgs e) => _host.MappingChanged();

    private void OnChanged(Settings last, Settings next)
    {
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
            _host.IgnoreKey = next.General.IgnoreKey;
        }

        if (next.Capture != last.Capture)
        {
            _host.SetThresholds(next.Capture);
        }
    }
}
