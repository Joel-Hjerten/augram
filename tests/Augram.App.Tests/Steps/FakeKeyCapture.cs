using Augram.App.Components.HotkeyCapture;
using Augram.Core.Abstractions;
using Augram.Engine.Hosting;

namespace Augram.App.Tests.Steps;

/// <summary>
/// An <see cref="IKeyCapture"/> a test drives by hand: <see cref="Begin"/> records the callback and hands
/// out a handle; <see cref="Press"/>, <see cref="Release"/> and <see cref="EngineReleases"/> call it on the
/// test thread, as the real one does after marshalling. <see cref="Available"/> false plays <c>--no-engine</c>.
/// </summary>
internal sealed class FakeKeyCapture : IKeyCapture
{
    private readonly List<Action<KeyCaptureEvent>> _callbacks = [];
    private Handle? _current;

    public bool Available { get; set; } = true;

    public int BeginCount => _callbacks.Count;

    public int DisposeCount { get; private set; }

    public TimeSpan? LastIdleTimeout { get; private set; }

    /// <summary>True while the latest handle has not been disposed: the keyboard is "taken".</summary>
    public bool IsArmed => _current is { Disposed: false };

    public IDisposable? Begin(Action<KeyCaptureEvent> onEvent, TimeSpan idleTimeout)
    {
        if (!Available)
        {
            return null;
        }

        _callbacks.Add(onEvent);
        LastIdleTimeout = idleTimeout;
        _current = new Handle(this);
        return _current;
    }

    public void Press(KeyCode key, KeyModifiers modifiers = KeyModifiers.None) => _callbacks[^1](KeyCaptureEvent.KeyDown(key, modifiers));

    public void Release(KeyCode key, KeyModifiers modifiers = KeyModifiers.None) => _callbacks[^1](KeyCaptureEvent.KeyUp(key, modifiers));

    /// <summary>The engine ending capture number <paramref name="session"/> (0-based; default the latest) on its own.</summary>
    public void EngineReleases(string reason, int? session = null) => _callbacks[session ?? _callbacks.Count - 1](KeyCaptureEvent.Released(reason));

    private sealed class Handle(FakeKeyCapture owner) : IDisposable
    {
        public bool Disposed { get; private set; }

        public void Dispose()
        {
            if (!Disposed)
            {
                Disposed = true;
                owner.DisposeCount++;
            }
        }
    }
}
