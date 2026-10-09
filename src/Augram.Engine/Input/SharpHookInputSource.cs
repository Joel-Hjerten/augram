using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Engine.Hosting;
using SharpHook;
using SharpHook.Data;
using SharpHook.Providers;

namespace Augram.Engine.Input;

/// <summary>
/// <see cref="IInputSource"/> over SharpHook's <see cref="SimpleGlobalHook"/>, the only hook type whose
/// handlers run synchronously and can therefore set <c>SuppressEvent</c> (CLAUDE.md invariant 2). Mouse
/// and keyboard, one hook per <see cref="Start"/> on its own named thread (<c>augram-hook-gN</c>). A
/// handler translates the event to a <see cref="RawInput"/>, calls the <see cref="InputHandler"/>, copies
/// its answer to <c>SuppressEvent</c>, returns; nothing else. Simulated events are dropped before the
/// handler: <c>IsEventSimulated</c> is true for input injected by any process, so other utilities'
/// synthetic input is ignored too (learnings 0001, B4), except wheel events: a vertical simulated wheel event is taken unless
/// <see cref="OwnWheelInjections"/> claims it as one of Augram's own Scroll step notches, because a vendor tool (Logi Options+)
/// re-posts every wheel turn of its mouse and wheel triggers would otherwise never fire (2026-10-09). Without an
/// <see cref="OwnWheelInjections"/> every simulated wheel event is dropped, as before. A dropped button press or release (simulated, or a
/// button number past 5) is logged as "Button ignored" with the raw number, at most once per
/// <see cref="IgnoredLogInterval"/> for each button, reason and direction, so a remapped mouse button that
/// never reaches the engine (a vendor tool injecting it) shows what it arrives as. A dropped wheel event (one of our own, horizontal,
/// or no whole-line rotation) is logged the same way as "Wheel ignored", once per interval and reason: a scrolling utility that
/// re-posts every wheel event (smooth or reversed scrolling) leaves wheel triggers nothing to see. Key events take the same path; the handler
/// suppresses them only while a hotkey capture is armed (<c>EngineHost.CaptureKeys</c>, decided in
/// <c>InputGate</c>). Thin and untested on purpose: it needs a desktop, and everything above it is
/// driven through a fake source in tests.
/// </summary>
public sealed class SharpHookInputSource : IInputSource
{
    private static readonly TimeSpan JoinTimeout = TimeSpan.FromSeconds(5);

    /// <summary>The minimum gap between two "Button ignored" lines for the same button, reason and direction.</summary>
    public static readonly TimeSpan IgnoredLogInterval = TimeSpan.FromSeconds(30);

    private const int IgnoredButtonSlots = 16;

    private readonly IClock _clock;
    private readonly IEventLog _log;
    private readonly OwnWheelInjections? _ownWheel;

    // Hook thread only: last "Button ignored" time per (button, simulated, pressed); 0 = never.
    private readonly long[] _ignoredLoggedAt = new long[IgnoredButtonSlots * 4];

    // Hook thread only: last "Wheel ignored" time per reason (simulated: ours, or any without an OwnWheelInjections; horizontal; no rotation); 0 = never.
    private readonly long[] _wheelIgnoredLoggedAt = new long[3];
    private readonly object _gate = new();
    private Generation? _current;
    private InputHandler? _handler;
    private int _generation;

    public SharpHookInputSource(IClock clock, IEventLog? log = null, OwnWheelInjections? ownWheel = null)
    {
        ArgumentNullException.ThrowIfNull(clock);
        _clock = clock;
        _log = log ?? NullEventLog.Instance;
        _ownWheel = ownWheel;
    }

    public event EventHandler<HookHealth>? HookHealthChanged;

    public bool IsRunning => _current?.Hook.IsRunning ?? false;

    /// <summary>Installs since construction; the thread name carries it.</summary>
    public int GenerationCount => Volatile.Read(ref _generation);

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
        if (e.IsEventSimulated || !MouseButtonMap.TryToCore(e.Data.Button, out var button))
        {
            LogIgnoredButton(e);
            return;
        }

        var raw = e.RawEvent.Type == EventType.MousePressed
            ? RawInput.ButtonDown(button, e.Data.X, e.Data.Y, _clock.MonotonicMs, Modifiers(e.RawEvent.Mask))
            : RawInput.ButtonUp(button, e.Data.X, e.Data.Y, _clock.MonotonicMs, Modifiers(e.RawEvent.Mask));
        e.SuppressEvent = _handler!(in raw);
    }

    private void LogIgnoredButton(MouseHookEventArgs e)
    {
        var number = (int)e.Data.Button;
        var pressed = e.RawEvent.Type == EventType.MousePressed;
        var slot = ((Math.Min(number, IgnoredButtonSlots - 1) * 2) + (e.IsEventSimulated ? 1 : 0)) * 2 + (pressed ? 1 : 0);
        var now = _clock.MonotonicMs;
        var last = _ignoredLoggedAt[slot];
        if ((last != 0 && now - last < (long)IgnoredLogInterval.TotalMilliseconds) || !_log.IsEnabled(EventLevel.Info))
        {
            return;
        }

        _ignoredLoggedAt[slot] = now == 0 ? 1 : now;
        _log.Info(
            LogSources.Hook,
            "Button ignored",
            ("button", number),
            ("direction", pressed ? "down" : "up"),
            ("reason", e.IsEventSimulated ? "simulated" : "unknown button"),
            ("mask", $"0x{(ushort)e.RawEvent.Mask:X4}"),
            ("x", e.Data.X),
            ("y", e.Data.Y));
    }

    private void OnMove(object? sender, MouseHookEventArgs e)
    {
        if (e.IsEventSimulated)
        {
            return;
        }

        var raw = RawInput.Move(e.Data.X, e.Data.Y, _clock.MonotonicMs);
        _handler!(in raw);
    }

    private void OnWheel(object? sender, MouseWheelHookEventArgs e)
    {
        if (e.Data.Direction != MouseWheelScrollDirection.Vertical || e.Data.Rotation == 0 || (e.IsEventSimulated && (_ownWheel is null || _ownWheel.TryClaim())))
        {
            LogIgnoredWheel(e);
            return;
        }

        // SharpHook: positive rotation is up (away from the user).
        var direction = e.Data.Rotation > 0 ? WheelDirection.Up : WheelDirection.Down;
        var raw = RawInput.WheelTick(direction, e.Data.X, e.Data.Y, _clock.MonotonicMs, Modifiers(e.RawEvent.Mask));
        e.SuppressEvent = _handler!(in raw);
    }

    private void LogIgnoredWheel(MouseWheelHookEventArgs e)
    {
        var slot = e.Data.Direction != MouseWheelScrollDirection.Vertical ? 1 : e.Data.Rotation == 0 ? 2 : 0;
        var now = _clock.MonotonicMs;
        var last = _wheelIgnoredLoggedAt[slot];
        if ((last != 0 && now - last < (long)IgnoredLogInterval.TotalMilliseconds) || !_log.IsEnabled(EventLevel.Info))
        {
            return;
        }

        _wheelIgnoredLoggedAt[slot] = now == 0 ? 1 : now;
        _log.Info(
            LogSources.Hook,
            "Wheel ignored",
            ("reason", slot switch { 0 => _ownWheel is null ? "simulated" : "own scroll", 1 => "horizontal", _ => "no rotation" }),
            ("simulated", e.IsEventSimulated),
            ("rotation", e.Data.Rotation),
            ("delta", e.Data.Delta),
            ("type", e.Data.Type),
            ("x", e.Data.X),
            ("y", e.Data.Y));
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
