using Augram.Core.Capture;
using Augram.Engine.Hosting;

namespace Augram.App.Components.ButtonDetect;

/// <summary>
/// <see cref="IButtonCapture"/> over <see cref="EngineHost.CaptureNextButtonPress"/> (the stroke button's Detect uses the same
/// call): the press arrives on the engine worker and is marshalled to the UI thread through <c>marshal</c>. Answers null while
/// the engine is not running (<c>--no-engine</c>: the hook was never installed).
/// </summary>
public sealed class EngineButtonCapture : IButtonCapture
{
    private readonly EngineHost _host;
    private readonly Action<Action> _marshal;

    public EngineButtonCapture(EngineHost host, Action<Action> marshal)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(marshal);
        _host = host;
        _marshal = marshal;
    }

    public IDisposable? Begin(Action<MouseButton> onPress)
    {
        ArgumentNullException.ThrowIfNull(onPress);
        return _host.IsRunning ? _host.CaptureNextButtonPress(button => _marshal(() => onPress(button))) : null;
    }
}
