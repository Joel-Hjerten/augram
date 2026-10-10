using Augram.App.Components.ButtonDetect;
using Augram.Core.Capture;

namespace Augram.App.Tests.Support;

/// <summary>
/// An <see cref="IButtonCapture"/> a test drives by hand: <see cref="Begin"/> records the callback and hands out a handle;
/// <see cref="Press"/> calls it on the test thread, as the engine's does after marshalling. <see cref="Available"/> false plays
/// <c>--no-engine</c>.
/// </summary>
internal sealed class FakeButtonCapture : IButtonCapture
{
    private Action<MouseButton>? _callback;
    private Handle? _current;

    public bool Available { get; set; } = true;

    public int BeginCount { get; private set; }

    /// <summary>True while the latest handle has not been disposed: a press would be reported.</summary>
    public bool IsArmed => _current is { Disposed: false };

    public IDisposable? Begin(Action<MouseButton> onPress)
    {
        if (!Available)
        {
            return null;
        }

        BeginCount++;
        _callback = onPress;
        _current = new Handle();
        return _current;
    }

    /// <summary>A physical press, reported only while armed (the engine forgets a disposed observer).</summary>
    public void Press(MouseButton button)
    {
        if (IsArmed)
        {
            _callback?.Invoke(button);
        }
    }

    private sealed class Handle : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }
}
