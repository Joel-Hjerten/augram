using Augram.Core.Abstractions;

namespace Augram.Engine.Tests.Fakes;

/// <summary>
/// An <see cref="IInputSource"/> a test drives by hand: <see cref="Deliver"/> calls the handler on the
/// calling thread and returns its decision, exactly as the hook thread would. Raises Installed on
/// <see cref="Start"/>, Stopped on <see cref="Stop"/>, and <see cref="ReportLost"/> fakes the OS killing the hook.
/// </summary>
internal sealed class FakeInputSource : IInputSource
{
    private InputHandler? _handler;
    private int _generation;

    public event EventHandler<HookHealth>? HookHealthChanged;

    public bool IsRunning { get; private set; }

    public int StartCount { get; private set; }

    public int StopCount { get; private set; }

    /// <summary>When set, the next <see cref="Start"/> throws it once.</summary>
    public Exception? FailNextStart { get; set; }

    public void Start(InputHandler handler)
    {
        if (IsRunning)
        {
            throw new InvalidOperationException("already started");
        }

        if (FailNextStart is { } failure)
        {
            FailNextStart = null;
            throw failure;
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

    public void ReportLost(string detail = "HookDisabled")
    {
        IsRunning = false;
        HookHealthChanged?.Invoke(this, new HookHealth(HookHealthKind.Lost, _generation, detail));
    }
}
