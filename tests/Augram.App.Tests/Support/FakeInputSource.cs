using Augram.Core.Abstractions;

namespace Augram.App.Tests.Support;

/// <summary>An <see cref="IInputSource"/> driven by hand: <see cref="Deliver"/> calls the handler on the calling thread, as the hook thread would. No hook is installed.</summary>
internal sealed class FakeInputSource : IInputSource
{
    private InputHandler? _handler;
    private int _generation;

    public event EventHandler<HookHealth>? HookHealthChanged;

    public bool IsRunning { get; private set; }

    public int StartCount { get; private set; }

    public int StopCount { get; private set; }

    public void Start(InputHandler handler)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("already started");
        }

        StartCount++;
        _handler = handler;
        IsRunning = true;
        HookHealthChanged?.Invoke(this, new HookHealth(HookHealthKind.Installed, ++_generation));
    }

    public void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        StopCount++;
        IsRunning = false;
        HookHealthChanged?.Invoke(this, new HookHealth(HookHealthKind.Stopped, _generation, "stopped"));
    }

    public void Dispose() => Stop();

    public bool Deliver(in RawInput input)
    {
        if (_handler is null || !IsRunning)
        {
            throw new InvalidOperationException("not started");
        }

        return _handler(in input);
    }
}
