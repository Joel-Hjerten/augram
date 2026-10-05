using System.Threading.Channels;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;

namespace Augram.Engine.Hosting;

/// <summary>
/// The engine worker thread's loop: the only code that calls <see cref="CaptureStateMachine.Handle"/>.
/// Drains the channel, runs the machine, publishes its state for the hook thread, and acts on the
/// outcomes: trail, click replay (the only place that injects input, A19), recognition, events, log.
/// Also cross-checks every button and wheel decision the hook made against the machine's own.
/// </summary>
internal sealed class EngineWorker
{
    private readonly EngineHost _host;
    private readonly InputGate _gate;
    private readonly ChannelReader<WorkerMessage> _reader;
    private readonly CaptureStateMachine _machine;
    private readonly StrokeRecognizer _recognizer;
    private readonly IStrokeTrail _trail;
    private readonly IInputSimulator _simulator;
    private readonly IEventLog _log;
    private readonly IClock _clock;
    private readonly int _deepQueue;
    private bool _trailOpen;
    private bool _deepWarned;

    public EngineWorker(EngineHost host, InputGate gate, ChannelReader<WorkerMessage> reader, CaptureStateMachine machine, StrokeRecognizer recognizer, EnginePorts ports, int queueCapacity)
    {
        _host = host;
        _gate = gate;
        _reader = reader;
        _machine = machine;
        _recognizer = recognizer;
        _trail = ports.Trail;
        _simulator = ports.Simulator;
        _log = ports.Log;
        _clock = ports.Clock;
        _deepQueue = Math.Max(1, queueCapacity / 2);
    }

    public void Run()
    {
        while (_reader.WaitToReadAsync().AsTask().GetAwaiter().GetResult())
        {
            CheckDepth();
            while (_reader.TryRead(out var message))
            {
                Process(message);
            }

            ReportDrops();
        }

        EndTrail();
    }

    private void Process(WorkerMessage message)
    {
        switch (message.Kind)
        {
            case WorkerMessage.MessageKind.Input:
                OnInput(message.Event!, message.HookSuppressed);
                break;
            case WorkerMessage.MessageKind.SetStrokeButton:
                var button = (MouseButton)message.Payload!;
                _machine.StrokeButton = button;
                _gate.PublishStrokeButton(button);
                _log.Info(LogSources.Engine, "Stroke button changed", ("button", button));
                break;
            case WorkerMessage.MessageKind.SetThresholds:
                var thresholds = (CaptureThresholds)message.Payload!;
                _machine.Thresholds = thresholds;
                _log.Info(LogSources.Engine, "Capture thresholds changed", ("startDistancePx", thresholds.StartDistancePx), ("minSegmentPx", thresholds.MinSegmentPx), ("cancelDelayMs", thresholds.CancelDelayMs), ("resetOnMovement", thresholds.ResetCancelDelayOnMovement));
                break;
            case WorkerMessage.MessageKind.Reset:
                _machine.Reset();
                EndTrail();
                _log.Info(LogSources.Capture, "Capture reset", ("reason", (string)message.Payload!));
                break;
            case WorkerMessage.MessageKind.ButtonObserved:
                _host.OnButtonObserved((MouseButton)message.Payload!);
                break;
        }

        AfterMachineChange();
    }

    private void OnInput(CaptureEvent e, bool hookSuppressed)
    {
        var outcomes = _machine.Handle(e);
        _gate.PublishState(_machine.State);
        if (e is CaptureEvent.ButtonDown or CaptureEvent.ButtonUp or CaptureEvent.Wheel)
        {
            CrossCheck(e, hookSuppressed, outcomes);
        }

        foreach (var outcome in outcomes)
        {
            Dispatch(outcome, e);
        }
    }

    private void AfterMachineChange()
    {
        var state = _machine.State;
        _gate.PublishState(state);
        if (_trailOpen && state is CaptureState.Idle or CaptureState.Cancelled)
        {
            // The machine owes no EndStroke when a pass-through press restarts it or the button changes mid-stroke.
            EndTrail();
        }

        _host.ArmTick(state is CaptureState.Held or CaptureState.Drawing);
    }

    private void Dispatch(CaptureOutcome outcome, CaptureEvent e)
    {
        switch (outcome)
        {
            case CaptureOutcome.BeginStroke begin:
                _trail.Begin(begin.Start);
                _trailOpen = true;
                _log.Debug(LogSources.Capture, "Stroke began", ("x", begin.Start.X), ("y", begin.Start.Y));
                break;
            case CaptureOutcome.StrokeProgress progress:
                _trail.Extend(progress.Point);
                break;
            case CaptureOutcome.EndStroke:
                EndTrail();
                break;
            case CaptureOutcome.ReplayClick replay:
                var result = _simulator.Click(replay.Button, replay.X, replay.Y);
                _log.Debug(LogSources.Capture, "Click replayed", ("button", replay.Button), ("x", replay.X), ("y", replay.Y), ("result", result), ("lagMs", _clock.MonotonicMs - e.TimestampMs));
                break;
            case CaptureOutcome.StrokeComplete stroke:
                Recognize(stroke);
                break;
            case CaptureOutcome.WheelTrigger wheel:
                _log.Info(LogSources.Capture, "Wheel trigger", ("direction", wheel.Direction), ("x", wheel.Start.X), ("y", wheel.Start.Y));
                _host.Raise(new EngineEvent.WheelTriggered(wheel.Direction, wheel.Start));
                break;
            case CaptureOutcome.Cancelled cancelled:
                _log.Debug(LogSources.Capture, "Gesture cancelled", ("reason", cancelled.Reason));
                break;
        }
    }

    private void Recognize(CaptureOutcome.StrokeComplete stroke)
    {
        try
        {
            var engineEvent = _recognizer.Recognize(stroke, _gate.TakeWorstHandlerMicroseconds());
            _host.PublishStrokeLatency(Math.Max(0, _clock.MonotonicMs - stroke.Points[^1].TimestampMs));
            _host.Raise(engineEvent);
        }
        catch (Exception exception)
        {
            _log.Error(LogSources.Recognition, "Recognition failed", exception, ("points", stroke.Points.Count));
        }
    }

    private void CrossCheck(CaptureEvent e, bool hookSuppressed, IReadOnlyList<CaptureOutcome> outcomes)
    {
        var machineSuppressed = false;
        foreach (var outcome in outcomes)
        {
            if (outcome is CaptureOutcome.Suppress)
            {
                machineSuppressed = true;
            }
        }

        if (machineSuppressed == hookSuppressed)
        {
            return;
        }

        // A wheel tick can race the hold-still cancel by one tick interval (documented in README); anything else is a bug.
        var level = e is CaptureEvent.Wheel ? EventLevel.Debug : EventLevel.Warning;
        if (_log.IsEnabled(level))
        {
            _log.Log(new LogEvent(DateTimeOffset.Now, level, LogSources.Capture, "Suppression decision mismatch", [new("event", e.GetType().Name), new("hook", hookSuppressed), new("machine", machineSuppressed)]));
        }
    }

    private void EndTrail()
    {
        if (!_trailOpen)
        {
            return;
        }

        _trailOpen = false;
        _trail.End();
    }

    private void CheckDepth()
    {
        var depth = _reader.Count;
        if (depth >= _deepQueue && !_deepWarned)
        {
            _deepWarned = true;
            _log.Warning(LogSources.Capture, "Input queue deep", ("depth", depth), ("capacity", _deepQueue * 2));
        }
        else if (depth < _deepQueue / 2)
        {
            _deepWarned = false;
        }
    }

    private void ReportDrops()
    {
        var (moves, buttons) = _gate.TakeDropCounts();
        if (moves > 0)
        {
            _log.Warning(LogSources.Capture, "Input queue full: moves or ticks dropped", ("dropped", moves));
        }

        if (buttons > 0)
        {
            _log.Error(LogSources.Capture, "Input queue full: button or wheel events dropped; capture reset", ("dropped", buttons));
            _machine.Reset();
            EndTrail();
            AfterMachineChange();
        }
    }
}
