using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using SharpHook;
using SharpHook.Data;
using SharpHook.Providers;

namespace Augram.Engine.Input;

/// <summary>
/// <see cref="IInputSource"/> over SharpHook's <see cref="SimpleGlobalHook"/>, the only hook type whose
/// handlers run synchronously and can therefore set <c>SuppressEvent</c> (CLAUDE.md invariant 2). Mouse
/// and keyboard, one hook per <see cref="Start"/> on its own named thread (<c>augram-hook-gN</c>). A
/// handler translates the event to a <see cref="RawInput"/>, calls the <see cref="InputHandler"/>, copies
/// its answer to <c>SuppressEvent</c>, returns; nothing else. Simulated events (<c>IsEventSimulated</c>, true for input injected
/// by any process) are Augram's own when <see cref="OwnInjections"/> claims them, and dropped; the rest is other programs' input,
/// handled by the <see cref="ForeignInput"/> policy chosen at construction (<see cref="ForeignInputRules"/>, the tested decision).
/// Under <see cref="ForeignInput.Drop"/> (Windows) other utilities' synthetic presses and moves are ignored (learnings 0001, B4) and
/// an unclaimed release is another program's (plan 0005 decision 10): it reaches the handler as
/// <see cref="RawInputKind.ButtonReleasedElsewhere"/> and is never suppressed. Under <see cref="ForeignInput.Take"/> (macOS,
/// 2026-10-19: libuiohook marks everything the Wacom driver posts for the pen as simulated) an unclaimed press, release or move
/// is an ordinary <see cref="RawInput"/>, suppressible like any other. Wheel events are taken on both unless claimed as one of
/// Augram's own Scroll step notches, because a vendor tool (Logi Options+) re-posts every wheel turn of its mouse and wheel
/// triggers would otherwise never fire (2026-10-09); simulated key events are always dropped. Without an
/// <see cref="OwnInjections"/> every simulated event is dropped, whatever the policy. A dropped button press or release
/// (simulated, a release elsewhere, or a button number past 5) and a dropped wheel event (one of our own, horizontal, or no
/// whole-line rotation) are logged by <see cref="IgnoredInputLog"/>, rate-limited; a claimed press or move, and foreign input
/// taken as real, are not logged. Key events take the same path; the handler
/// suppresses them only while a hotkey capture is armed (<c>EngineHost.CaptureKeys</c>, decided in
/// <c>InputGate</c>). Thin and untested on purpose: it needs a desktop, and everything above it is
/// driven through a fake source in tests.
/// </summary>
public sealed class SharpHookInputSource : IInputSource
{
    private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(5);

    private readonly IClock _clock;
    private readonly OwnInjections? _own;
    private readonly ForeignInput _foreign;

    // Hook thread only.
    private readonly IgnoredInputLog _ignored;
    private readonly object _gate = new();
    private Generation? _current;
    private InputHandler? _handler;
    private int _generation;

    /// <param name="clock">Stamps every <see cref="RawInput"/>.</param>
    /// <param name="log">Where <see cref="IgnoredInputLog"/>'s lines go.</param>
    /// <param name="own">The ledger of Augram's own injections; without one every simulated event is dropped.</param>
    /// <param name="foreign">What becomes of simulated mouse input the ledger does not claim: dropped (Windows) or taken as real (macOS).</param>
    public SharpHookInputSource(IClock clock, IEventLog? log = null, OwnInjections? own = null, ForeignInput foreign = ForeignInput.Drop)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
        _own = own;
        _foreign = foreign;
        _ignored = new IgnoredInputLog(clock, log ?? NullEventLog.Instance, ownCounted: own is not null);
    }

    public event EventHandler<HookHealth>? HookHealthChanged;

    public bool IsRunning => _current?.Hook.IsRunning ?? false;

    public void Start(InputHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate)
        {
            if (_current is not null)
            {
                throw new InvalidOperationException("The hook is already installed; Stop it first.");
            }

            _handler = handler;
            if (OperatingSystem.IsMacOS())
            {
                // libuiohook turns every key press into a typed character with a synchronous trip to the main thread (the
                // keyboard-layout calls are main-thread only), which would make every key on the machine wait for our UI
                // thread (invariant 1). Nothing here listens to KeyTyped.
                ((IGlobalHookProvider)UioHookProvider.Instance).KeyTypedEnabled = false;
            }

            var generation = new Generation(new SimpleGlobalHook(GlobalHookType.All), ++_generation);
            var hook = generation.Hook;
            hook.HookEnabled += (_, _) => Raise(HookHealthKind.Installed, generation, null);
            hook.HookDisabled += (_, _) => OnDisabled(generation, "HookDisabled");
            hook.MousePressed += OnButton;
            hook.MouseReleased += OnButton;
            hook.MouseMoved += OnMove;
            hook.MouseDragged += OnMove;
            hook.MouseWheel += OnWheel;
            hook.KeyPressed += OnKey;
            hook.KeyReleased += OnKey;
            generation.Thread = new Thread(() => Run(generation)) { IsBackground = true, Name = $"augram-hook-g{generation.Number}" };
            _current = generation;
            generation.Thread.Start();
        }
    }

    public void Stop()
    {
        Generation? generation;
        lock (_gate)
        {
            generation = _current;
            _current = null;
        }

        if (generation is null)
        {
            return;
        }

        generation.Stopping = true;
        try
        {
            generation.Hook.Dispose();
        }
        catch (Exception)
        {
            // A hook that already failed may throw on dispose; the thread is joined below regardless.
        }

        if (generation.Thread is { } thread && thread != Thread.CurrentThread)
        {
            thread.Join(JoinTimeout);
        }
    }

    public void Dispose() => Stop();

    private static KeyModifiers Modifiers(EventMask mask)
    {
        var modifiers = KeyModifiers.None;
        if ((mask & EventMask.Ctrl) != 0)
        {
            modifiers |= KeyModifiers.Control;
        }

        if ((mask & EventMask.Alt) != 0)
        {
            modifiers |= KeyModifiers.Alt;
        }

        if ((mask & EventMask.Shift) != 0)
        {
            modifiers |= KeyModifiers.Shift;
        }

        if ((mask & EventMask.Meta) != 0)
        {
            modifiers |= KeyModifiers.Meta;
        }

        return modifiers;
    }

    private void Run(Generation generation)
    {
        try
        {
            generation.Hook.Run();
        }
        catch (HookException e)
        {
            OnDisabled(generation, $"{e.Result}: {e.Message}");
        }
        catch (Exception e)
        {
            OnDisabled(generation, $"{e.GetType().Name}: {e.Message}");
        }
    }

    private void OnDisabled(Generation generation, string detail)
    {
        if (Interlocked.Exchange(ref generation.EndReported, 1) != 0)
        {
            return;
        }

        Raise(generation.Stopping ? HookHealthKind.Stopped : HookHealthKind.Lost, generation, detail);
    }

    private void Raise(HookHealthKind kind, Generation generation, string? detail) =>
        HookHealthChanged?.Invoke(this, new HookHealth(kind, generation.Number, detail));

    private void OnButton(object? sender, MouseHookEventArgs e)
    {
        if (!MouseButtonMap.TryToCore(e.Data.Button, out var button))
        {
            _ignored.Button(e, "unknown button");
            return;
        }

        var pressed = e.RawEvent.Type == EventType.MousePressed;
        if (e.IsEventSimulated)
        {
            switch (ForeignInputRules.Button(_foreign, _own, button, pressed))
            {
                case SimulatedButtonVerdict.Take:
                    break;
                case SimulatedButtonVerdict.ReleasedElsewhere:
                    // The OS has the button up; the handler's answer is ignored (never suppressed).
                    _ignored.Button(e, "released elsewhere");
                    var elsewhere = RawInput.ButtonReleasedElsewhere(button, e.Data.X, e.Data.Y, _clock.MonotonicMs);
                    _handler!(in elsewhere);
                    return;
                default:
                    // One of Augram's own (claimed: a release, or under Take a press) is dropped silently; the rest is logged.
                    if (_own is null || (pressed && _foreign == ForeignInput.Drop))
                    {
                        _ignored.Button(e, "simulated");
                    }

                    return;
            }
        }

        var raw = pressed
            ? RawInput.ButtonDown(button, e.Data.X, e.Data.Y, _clock.MonotonicMs, Modifiers(e.RawEvent.Mask))
            : RawInput.ButtonUp(button, e.Data.X, e.Data.Y, _clock.MonotonicMs, Modifiers(e.RawEvent.Mask));
        e.SuppressEvent = _handler!(in raw);
    }

    private void OnMove(object? sender, MouseHookEventArgs e)
    {
        if (e.IsEventSimulated && !ForeignInputRules.TakesMove(_foreign, _own))
        {
            return;
        }

        var raw = RawInput.Move(e.Data.X, e.Data.Y, _clock.MonotonicMs);
        // Swallowed only on macOS while a hold remap holds a button output: the worker re-posts it as a drag of that output.
        e.SuppressEvent = _handler!(in raw);
    }

    private void OnWheel(object? sender, MouseWheelHookEventArgs e)
    {
        if (e.Data.Direction != MouseWheelScrollDirection.Vertical || e.Data.Rotation == 0 || (e.IsEventSimulated && (_own is null || _own.TryClaim(InjectionSlot.Wheel))))
        {
            _ignored.Wheel(e);
            return;
        }

        // SharpHook: positive rotation is up (away from the user).
        var direction = e.Data.Rotation > 0 ? WheelDirection.Up : WheelDirection.Down;
        var raw = RawInput.WheelTick(direction, e.Data.X, e.Data.Y, _clock.MonotonicMs, Modifiers(e.RawEvent.Mask));
        e.SuppressEvent = _handler!(in raw);
    }

    private void OnKey(object? sender, KeyboardHookEventArgs e)
    {
        if (e.IsEventSimulated)
        {
            return;
        }

        var key = KeyCodeMap.ToCore(e.Data.KeyCode);
        var raw = e.RawEvent.Type == EventType.KeyPressed
            ? RawInput.KeyDown(key, _clock.MonotonicMs, Modifiers(e.RawEvent.Mask))
            : RawInput.KeyUp(key, _clock.MonotonicMs, Modifiers(e.RawEvent.Mask));
        e.SuppressEvent = _handler!(in raw);
    }

    private sealed class Generation(SimpleGlobalHook hook, int number)
    {
        public int EndReported;

        public SimpleGlobalHook Hook { get; } = hook;

        public int Number { get; } = number;

        public Thread? Thread { get; set; }

        public volatile bool Stopping;
    }
}
