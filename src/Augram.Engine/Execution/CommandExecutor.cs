using System.Diagnostics;
using System.Threading.Channels;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;
using Augram.Core.Mapping;
using Augram.Engine.Hosting;

namespace Augram.Engine.Execution;

/// <summary>
/// The command executor thread (<c>augram-command-executor</c>; thread table in the Engine README). The
/// engine worker enqueues one <see cref="ExecutionRequest"/> per recognised gesture or wheel tick and
/// goes straight back to the hook queue; this thread looks the window up, resolves the command through
/// <see cref="CommandResolver"/>, completes the recognition log entry, and hands a command that fires to
/// <see cref="CommandRunner"/> (activation per A20, settle delay per A8, steps in order). One command at
/// a time; a step may block here (a Delay does) and nothing on the hook or UI thread waits on it. The
/// queue is small (<see cref="QueueCapacity"/>) and drops its oldest request when full: a backlog of
/// gestures is never worth replaying late. <see cref="Stop"/> cancels the running command, drains the
/// queue without running it, and joins the thread.
/// </summary>
internal sealed class CommandExecutor : IDisposable
{
    public const int QueueCapacity = 8;
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(5);

    private readonly Channel<ExecutionRequest> _queue;
    private readonly Thread _thread;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Func<MappingDocument> _mapping;
    private readonly IWindowSystem _windows;
    private readonly HostPlatform _platform;
    private readonly RecognitionLog _recognitionLog;
    private readonly IEventLog _log;
    private readonly IInputSimulator _simulator;
    private readonly CommandRunner _runner;
    private readonly IDisposable? _healthRegistration;
    private int _started;
    private int _stopped;

    public CommandExecutor(EnginePorts ports, EngineHostOptions options)
    {
        ArgumentNullException.ThrowIfNull(ports);
        ArgumentNullException.ThrowIfNull(options);
        _mapping = ports.Mapping ?? throw new ArgumentException("The executor needs the Mapping port.", nameof(ports));
        _windows = ports.Windows;
        _platform = ports.WindowOperations.Platform;
        _recognitionLog = ports.RecognitionLog;
        _log = ports.Log;
        _simulator = ports.Simulator;
        _runner = new CommandRunner(ports, options.SettleDelayMs, _stopping.Token);
        _queue = Channel.CreateBounded<ExecutionRequest>(
            new BoundedChannelOptions(QueueCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = true,
                AllowSynchronousContinuations = false,
            },
            dropped => _log.Warning(LogSources.Execution, "Execution queue full", ("dropped", dropped.Trigger.Describe()), ("capacity", QueueCapacity)));
        _thread = new Thread(Run) { IsBackground = true, Name = "augram-command-executor" };
        _healthRegistration = ports.Health?.Register(snapshot =>
            _runner.LastActivationOutcome is { } outcome ? snapshot with { LastActivationOutcome = outcome } : snapshot);
    }

    public bool IsRunning => _thread.IsAlive;

    public void Start()
    {
        if (Interlocked.Exchange(ref _started, 1) == 0)
        {
            _thread.Start();
        }
    }

    /// <summary>Called on the engine worker only; never blocks. After <see cref="Stop"/> the request is dropped with a Debug line.</summary>
    public void Enqueue(ExecutionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!_queue.Writer.TryWrite(request with { EnqueuedAt = Stopwatch.GetTimestamp() }))
        {
            _log.Debug(LogSources.Execution, "Execution request dropped: executor stopped", ("trigger", request.Trigger.Describe()));
        }
    }

    /// <summary>Cancels the running command (a blocking step wakes), completes the queue and joins the thread (5 s cap). Idempotent.</summary>
    public void Stop()
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0)
        {
            return;
        }

        _stopping.Cancel();
        _queue.Writer.TryComplete();
        if (_thread.IsAlive && _thread != Thread.CurrentThread)
        {
            _thread.Join(StopTimeout);
        }
    }

    public void Dispose()
    {
        Stop();
        _healthRegistration?.Dispose();
        _stopping.Dispose();
    }

    private void Run()
    {
        while (_queue.Reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
        {
            while (_queue.Reader.TryRead(out var request))
            {
                if (_stopping.IsCancellationRequested)
                {
                    continue;
                }

                try
                {
                    Execute(request);
                }
                catch (Exception exception)
                {
                    _log.Error(LogSources.Execution, "Command execution failed", exception, ("trigger", request.Trigger.Describe()));
                }
            }
        }
    }

    /// <summary>The rule order from the README's Execution section: window under the start, resolve, complete the entry, log, run when it fires.</summary>
    private void Execute(ExecutionRequest request)
    {
        var target = _windows.WindowAt(request.Start.X, request.Start.Y);
        var resolution = CommandResolver.Resolve(_mapping(), target, request.Trigger, _platform);
        if (request.Draft is { } draft)
        {
            _recognitionLog.Add(draft with
            {
                MatchedGroup = resolution.Group?.Name,
                FiredCommand = resolution.Fires ? resolution.Command!.Name : null,
                NothingFiredReason = resolution.Fires ? null : resolution.Reason,
            });
        }

        _log.Info(
            LogSources.Execution,
            "Trigger resolved",
            ("trigger", request.Describe()),
            ("outcome", resolution.Outcome),
            ("reason", resolution.Reason),
            ("group", resolution.Group?.Name),
            ("command", resolution.Command?.Name),
            ("process", target?.ProcessName));

        if (resolution.Fires)
        {
            _runner.Run(request, resolution.Group!, resolution.Command!, target);
        }
        else if (request.Relay is { } relay)
        {
            // A click trigger that fires nothing here goes to the app with its keys held (Joel, 2026-10-09; SP.net swallowed it).
            var result = relay.Run(_simulator);
            _log.Debug(LogSources.Execution, "Click relayed", ("button", relay.Button), ("keys", relay.AfterKeys), ("result", result));
        }
    }
}
