using System.Threading.Channels;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.Gestures;
using Augram.Core.Recognition;
using Augram.Engine.Input;

namespace Augram.Engine.Hosting;

/// <summary>
/// The engine's composition point (not the App's): owns the input source through its
/// <see cref="HookHealthMonitor"/>, the <see cref="CaptureStateMachine"/>, the hook-to-worker channel,
/// the worker thread and the tick timer. Thread roles are in <c>src/Augram.Engine/README.md</c>; the
/// short version: the hook thread only decides suppression (<see cref="InputGate"/>) and enqueues; the
/// worker (<see cref="EngineWorker"/>) owns the machine and everything after it. Gestures and recognition
/// options are delegates so the App wires its stores. M1: recognizes and reports, executes nothing.
/// </summary>
public sealed class EngineHost : IDisposable
{
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

    private readonly IEventLog _log;
    private readonly IClock _clock;
    private readonly Channel<WorkerMessage> _queue;
    private readonly InputGate _gate;
    private readonly HookHealthMonitor _monitor;
    private readonly EngineWorker _worker;
    private readonly Thread _workerThread;
    private readonly Timer _tick;
    private readonly TimeSpan _tickInterval;
    private readonly IDisposable? _healthRegistration;
    private int _tickArmed;
    private int _started;
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

        var machine = new CaptureStateMachine(options.StrokeButton, options.Thresholds);
        var recognizer = new StrokeRecognizer(gestures, recognition, ports.RecognitionLog, _log, _clock);
        _worker = new EngineWorker(this, _gate, _queue.Reader, machine, recognizer, ports, options.QueueCapacity);
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

    public void Start()
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Interlocked.Exchange(ref _started, 1) != 0)
        {
            throw new InvalidOperationException("The engine is already started.");
        }

        _log.Info(LogSources.Engine, "Engine starting", ("strokeButton", StrokeButton), ("enabled", Enabled), ("tickMs", _tickInterval.TotalMilliseconds));
        _workerThread.Start();
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
        _queue.Writer.TryComplete();
        if (_workerThread.IsAlive && _workerThread != Thread.CurrentThread)
        {
            _workerThread.Join(StopTimeout);
        }

        _log.Info(LogSources.Engine, "Engine stopped", ("droppedMoves", DroppedMoveCount));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Stop();
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
