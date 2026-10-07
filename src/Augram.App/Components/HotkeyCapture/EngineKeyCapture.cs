using Augram.Engine.Hosting;

namespace Augram.App.Components.HotkeyCapture;

/// <summary>
/// <see cref="IKeyCapture"/> over <see cref="EngineHost.CaptureKeys"/>: events arrive on the engine worker
/// and are marshalled to the UI thread through <c>marshal</c> (the composition root passes
/// <c>Dispatcher.UIThread.Post</c>). Answers null while the engine is not running, which is the
/// <c>--no-engine</c> case: the hook was never installed, so nothing could be captured.
/// </summary>
public sealed class EngineKeyCapture : IKeyCapture
{
    private readonly EngineHost _host;
    private readonly Action<Action> _marshal;

    public EngineKeyCapture(EngineHost host, Action<Action> marshal)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(marshal);
        _host = host;
        _marshal = marshal;
    }

    public IDisposable? Begin(Action<KeyCaptureEvent> onEvent, TimeSpan idleTimeout)
    {
        ArgumentNullException.ThrowIfNull(onEvent);
        return _host.IsRunning ? _host.CaptureKeys(captured => _marshal(() => onEvent(captured)), idleTimeout) : null;
    }
}
