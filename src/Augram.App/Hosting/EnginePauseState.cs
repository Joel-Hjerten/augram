using Augram.Core.Mapping;
using Augram.Engine.Hosting;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Augram.App.Hosting;

/// <summary>
/// Which "disable while focused" app Augram is paused for (F5 ignore list; SP.net's "Disable S+ if this App Gains Focus",
/// Joel's VMware), for the tray tooltip: the engine's <see cref="EngineHost.PauseChanged"/>, marshalled to the UI thread.
/// Runtime state, not a setting: null at every launch, set only once <see cref="Follow"/> links a started engine (never
/// under <c>--no-engine</c>). The engine logs the pause itself.
/// </summary>
public sealed partial class EnginePauseState : ObservableObject, IDisposable
{
    private readonly Action<Action> _marshal;
    private EngineHost? _host;

    /// <param name="marshal">Posts to the UI thread (<c>EngineModuleOptions.Marshal</c>).</param>
    public EnginePauseState(Action<Action> marshal)
    {
        ArgumentNullException.ThrowIfNull(marshal);
        _marshal = marshal;
    }

    /// <summary>The paused-for app's name, or null while Augram is not paused.</summary>
    [ObservableProperty]
    public partial string? PausedBy { get; private set; }

    /// <summary>Follows <paramref name="host"/> from now on (EngineModule.Start, after the host started). Once.</summary>
    public void Follow(EngineHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (_host is not null)
        {
            return;
        }

        _host = host;
        host.PauseChanged += OnPauseChanged;
        var current = host.PausedBy?.Name;
        _marshal(() => PausedBy = current);
    }

    public void Dispose()
    {
        if (_host is not null)
        {
            _host.PauseChanged -= OnPauseChanged;
        }
    }

    private void OnPauseChanged(object? sender, IgnoredApp? app)
    {
        var name = app?.Name;
        _marshal(() => PausedBy = name);
    }
}
