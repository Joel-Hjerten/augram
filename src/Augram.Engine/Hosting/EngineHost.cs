using System.Threading.Channels;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Core.Recognition;
using Augram.Engine.Execution;
using Augram.Engine.Input;

namespace Augram.Engine.Hosting;

/// <summary>
/// The engine's composition point (not the App's): owns the input source through its
/// <see cref="HookHealthMonitor"/>, the <see cref="CaptureStateMachine"/>, the hook-to-worker channel,
/// the worker thread and the tick timer. Thread roles are in <c>src/Augram.Engine/README.md</c>; the
/// short version: the hook thread only decides suppression (<see cref="InputGate"/>) and enqueues; the
/// worker (<see cref="EngineWorker"/>) owns the machine and everything after it; the command executor
/// (<see cref="CommandExecutor"/>, its own thread, present only when <see cref="EnginePorts.Mapping"/>
/// is wired) resolves and runs what the worker hands it. Gestures and recognition options are
/// delegates so the App wires its stores. <see cref="CaptureKeys"/> is the hotkey field's system-wide
/// key capture (F5; <see cref="KeyCaptureController"/>). With a mapping, the <see cref="IgnoreListWatch"/>
/// (its own thread) keeps the ignore list's answer current for the hook: the stroke button passes through
/// over an ignored app, and everything does while a "disable while focused" app has focus
/// (<see cref="PausedBy"/>, <see cref="PauseChanged"/>; the App calls <see cref="MappingChanged"/> after an edit).
/// </summary>
public sealed partial class EngineHost : IDisposable
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

    /// <summary>F5's watchdog default: a hotkey capture with no key event for this long releases the keyboard.</summary>
    public static readonly TimeSpan DefaultKeyCaptureIdleTimeout = TimeSpan.FromSeconds(10);

    private readonly IEventLog _log;
    private readonly IClock _clock;
    private readonly Channel<WorkerMessage> _queue;
    private readonly InputGate _gate;
    private readonly HookHealthMonitor _monitor;
    private readonly EngineWorker _worker;
    private readonly CommandExecutor? _executor;
    private readonly KeyCaptureController _keyCapture;
    private readonly IgnoreListWatch? _ignoreWatch;
    private readonly Thread _workerThread;
    private readonly Timer _tick;
    private readonly TimeSpan _tickInterval;
    private readonly IDisposable? _healthRegistration;
    private int _tickArmed;
    private int _started;
    private int _running;
    private int _disposed;
    private long _lastStrokeLatencyMs = -1;
    private Action<MouseButton>? _buttonObserver;

    public EngineHost(EnginePorts ports, Func<IReadOnlyList<Gesture>> gestures, Func<RecognitionOptions> recognition, EngineHostOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(ports);
        ArgumentNullException.ThrowIfNull(gestures);
        ArgumentNullException.ThrowIfNull(recognition);
        options ??= EngineHostOptions.Default;
        ArgumentOutOfRangeException.ThrowIfLessThan(options.QueueCapacity, 16);

        _log = ports.Log;
        _clock = ports.Clock;
        _tickInterval = options.TickInterval ?? EngineHostOptions.DefaultTickInterval;
        // Wait mode so TryWrite reports a full queue instead of silently dropping; the gate decides per event kind.
        _queue = Channel.CreateBounded<WorkerMessage>(new BoundedChannelOptions(options.QueueCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false,
            AllowSynchronousContinuations = false,
        });
        _gate = new InputGate(_queue.Writer, _log, options.StrokeButton, options.IgnoreKey, options.Enabled);
        _keyCapture = new KeyCaptureController(_gate, _log);

        var machine = new CaptureStateMachine(options.StrokeButton, options.Thresholds);
        var recognizer = new StrokeRecognizer(gestures, recognition, ports.RecognitionLog, _log, _clock);
        _executor = ports.Mapping is null ? null : new CommandExecutor(ports, options);
        if (ports.Mapping is not null)
        {
            _ignoreWatch = new IgnoreListWatch(_gate, ports, ports.Mapping, OnPauseChanged, options.FocusPollInterval);
            _gate.Attach(_ignoreWatch);
        }

        _worker = new EngineWorker(this, _gate, _queue.Reader, machine, recognizer, _executor, ports, options.QueueCapacity);
        _workerThread = new Thread(_worker.Run) { IsBackground = true, Name = "augram-engine-worker" };
        _tick = new Timer(_ => _gate.Post(WorkerMessage.Input(new CaptureEvent.Tick(_clock.MonotonicMs), false), critical: false));
        _monitor = new HookHealthMonitor(ports.Input, _clock, _log, ports.CursorProbe, ports.SystemEvents, ports.Health, options.HealthPollInterval);
        _monitor.ResetRequested += OnResetRequested;
        _healthRegistration = ports.Health?.Register(snapshot =>
        {
            var latency = Volatile.Read(ref _lastStrokeLatencyMs);
            return latency < 0 ? snapshot : snapshot with { LastStrokeLatencyMs = latency };
        });
    }

    /// <summary>Raised on the worker thread after every completed stroke and wheel tick.</summary>
    public event EventHandler<EngineEvent>? EventRaised;

    /// <summary>The tray toggle. Off: new presses pass through; a press already consumed still gets its release consumed (A19).</summary>
    public bool Enabled
    {
        get => _gate.Enabled;
        set
        {
            if (_gate.Enabled == value)
            {
                return;
            }

            _gate.Enabled = value;
            _log.Info(LogSources.Engine, value ? "Engine enabled" : "Engine disabled");
        }
    }

    /// <summary>The configured button. Setting it is applied by the worker in order with the input around it; the getter reflects it once applied.</summary>
    public MouseButton StrokeButton
    {
        get => _gate.StrokeButton;
        set => _gate.Post(WorkerMessage.StrokeButton(value), critical: true);
    }

    public KeyModifiers IgnoreKey
    {
        get => _gate.IgnoreKey;
        set => _gate.IgnoreKey = value;
    }

    public CaptureState State => _gate.State;

    public HookHealthMonitor Health => _monitor;

    public long DroppedMoveCount => _gate.DroppedMoveCount;

    /// <summary>True between <see cref="Start"/> and <see cref="Stop"/>: the hook is (being) installed and the worker runs.</summary>
    public bool IsRunning => Volatile.Read(ref _running) != 0;

    /// <summary>True while a <see cref="CaptureKeys"/> capture is armed.</summary>
    public bool IsCapturingKeys => _gate.KeysCaptured;

    /// <summary>True when a Mapping port was wired and recognised gestures and wheel ticks are executed; false is M1 behaviour (recognise and report only).</summary>
    internal bool HasExecutor => _executor is not null;

    /// <summary>For tests: the executor thread is alive.</summary>
    internal bool ExecutorRunning => _executor?.IsRunning ?? false;

    public void SetThresholds(CaptureThresholds thresholds)
    {
        ArgumentNullException.ThrowIfNull(thresholds);
        _gate.Post(WorkerMessage.Thresholds(thresholds), critical: true);
    }

    /// <summary>
    /// Detect-to-assign (F1): <paramref name="callback"/> receives the next physical button press, on the
    /// worker thread, once. The press is observed, not swallowed: it is captured, suppressed or passed
    /// through exactly as it would be otherwise. A later call replaces an earlier one; disposing the
    /// result cancels. The caller marshals to its own thread and applies the timeout.
    /// </summary>
    public IDisposable CaptureNextButtonPress(Action<MouseButton> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);
        Volatile.Write(ref _buttonObserver, callback);
        _gate.ObserveNextPress(true);
        return new ButtonCapture(this, callback);
    }

    /// <summary>
    /// Hotkey capture (F5): until released, every key press is suppressed system-wide (Win+L, Alt+Tab,
    /// Esc and PrintScreen included) and reported, with its release, to <paramref name="callback"/> on the
    /// worker thread; the mouse is never affected. A key held since before the call reaches the OS to its
    /// release; a key pressed during the capture stays swallowed to its release, even after the capture
    /// ends (A19 for keys). Released by disposing the result (not reported back), and by the engine itself,
    /// reported as <see cref="KeyCaptureEventKind.Released"/> with the reason: no key event for
    /// <paramref name="idleTimeout"/> (the watchdog; <see cref="DefaultKeyCaptureIdleTimeout"/> is F5's
    /// default), another call replacing this one, a hook reset, <see cref="Stop"/>. The caller marshals to its
    /// own thread. Works whether or not gestures are enabled; does nothing useful before <see cref="Start"/>.
    /// </summary>
    public IDisposable CaptureKeys(Action<KeyCaptureEvent> callback, TimeSpan idleTimeout)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return _keyCapture.Arm(callback, idleTimeout);
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException("The engine is already started.");
        }

        _log.Info(LogSources.Engine, "Engine starting", ("strokeButton", StrokeButton), ("enabled", Enabled), ("tickMs", _tickInterval.TotalMilliseconds));
        _workerThread.Start();
        _executor?.Start();
        _ignoreWatch?.Start();
        Volatile.Write(ref _running, 1);
        _monitor.Start(_gate.Handle);
    }

    public void Stop()
    {
        if (Volatile.Read(ref _started) == 0)
        {
            return;
        }

        _monitor.Stop();
        ArmTick(false);
        Volatile.Write(ref _running, 0);
        // Before the queue completes, so the release notice still reaches the caller through the worker.
        _keyCapture.ReleaseCurrent(KeyCaptureEvent.EngineStoppedReason);
        _queue.Writer.TryComplete();
        if (_workerThread.IsAlive && _workerThread != Thread.CurrentThread)
        {
            _workerThread.Join(StopTimeout);
        }

        _executor?.Stop();
        _ignoreWatch?.Stop();
        _log.Info(LogSources.Engine, "Engine stopped", ("droppedMoves", DroppedMoveCount));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Stop();
        _executor?.Dispose();
        _ignoreWatch?.Dispose();
        _tick.Dispose();
        _monitor.ResetRequested -= OnResetRequested;
        _monitor.Dispose();
        _healthRegistration?.Dispose();
    }

    internal void PublishStrokeLatency(long latencyMs) => Volatile.Write(ref _lastStrokeLatencyMs, latencyMs);

    internal void OnButtonObserved(MouseButton button)
    {
        var observer = Interlocked.Exchange(ref _buttonObserver, null);
        if (observer is null)
        {
            return;
        }

        try
        {
            observer(button);
        }
        catch (Exception exception)
        {
            _log.Error(LogSources.Engine, "Button capture callback threw", exception, ("button", button));
        }
    }

    internal void OnKeyCaptured(KeyCaptureEvent captured) => _keyCapture.Deliver(captured);

    internal void ArmTick(bool armed)
    {
        var next = armed ? 1 : 0;
        if (Interlocked.Exchange(ref _tickArmed, next) == next || Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        _tick.Change(armed ? _tickInterval : Timeout.InfiniteTimeSpan, armed ? _tickInterval : Timeout.InfiniteTimeSpan);
    }

    internal void Raise(EngineEvent engineEvent)
    {
        try
        {
            EventRaised?.Invoke(this, engineEvent);
        }
        catch (Exception exception)
        {
            _log.Error(LogSources.Engine, "Event handler threw", exception, ("event", engineEvent.GetType().Name));
        }
    }

    private void OnResetRequested(object? sender, string reason)
    {
        _keyCapture.ReleaseCurrent($"{KeyCaptureEvent.HookResetReason} ({reason})");
        _gate.ResetShadow();
        _gate.Post(WorkerMessage.Reset(reason), critical: true);
    }

    private sealed class ButtonCapture(EngineHost host, Action<MouseButton> callback) : IDisposable
    {
        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref host._buttonObserver, null, callback) == callback)
            {
                host._gate.ObserveNextPress(false);
            }
        }
    }
}
