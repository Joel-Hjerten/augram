using System.Threading.Channels;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Engine.Execution;

namespace Augram.Engine.Hosting;

/// <summary>
/// The engine worker thread's loop: the only code that calls <see cref="CaptureStateMachine.Handle"/> and
/// <see cref="Core.HoldRemaps.HoldRemapMachine.Handle"/>.
/// Drains the channel, runs the machine, publishes its state for the hook thread, and acts on the
/// outcomes (<c>EngineWorker.Outcomes.cs</c>): trail, click replay and hand-back (the only place that injects
/// mouse input for a press, A19), recognition, events, log. A recognised gesture, a wheel tick or a click trigger
/// is then offered to the App's intercept (the training popup), else enqueued to the <see cref="CommandExecutor"/>;
/// the worker itself never runs a step. Hold remaps (F9) run the same way on their own machine
/// (<c>EngineWorker.HoldRemaps.cs</c>): the worker injects their outputs, taps and replays itself, in order with the input,
/// and enqueues a Steps command. Also cross-checks every button and wheel decision the hook made against the
/// machine's own, and every hold remap decision. Hotkey-capture key events and release notices pass straight through to the
/// <see cref="KeyCaptureController"/>'s caller.
/// </summary>
internal sealed partial class EngineWorker
{
    public const string NoMappingReason = "no mapping configured";
    public const string ConsumedReason = "consumed by training";

    private readonly EngineHost _host;
    private readonly InputGate _gate;
    private readonly ChannelReader<WorkerMessage> _reader;
    private readonly CaptureStateMachine _machine;
    private readonly StrokeRecognizer _recognizer;
    private readonly CommandExecutor? _executor;
    private readonly Func<EngineEvent, bool>? _intercept;
    private readonly RecognitionLog _recognitionLog;
    private readonly IStrokeTrail _trail;
    private readonly IInputSimulator _simulator;
    private readonly IEventLog _log;
    private readonly IClock _clock;
    private readonly bool _maskWinAlt;
    private readonly int _deepQueue;
    private bool _trailOpen;
    private bool _deepWarned;

    public EngineWorker(EngineHost host, InputGate gate, ChannelReader<WorkerMessage> reader, CaptureStateMachine machine, StrokeRecognizer recognizer, CommandExecutor? executor, EnginePorts ports, int queueCapacity)
    {
        _host = host;
        _gate = gate;
        _reader = reader;
        _machine = machine;
        _recognizer = recognizer;
        _executor = executor;
        _intercept = ports.Intercept;
        _recognitionLog = ports.RecognitionLog;
        _trail = ports.Trail;
        _simulator = ports.Simulator;
        _log = ports.Log;
        _clock = ports.Clock;
        _maskWinAlt = ports.WindowOperations.Platform == HostPlatform.Windows;
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
        // The engine stops: nothing a hold remap pressed may stay down (A19).
        ResetHold("engine stopped");
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
                // The anchor plans name the stroke button: the ignore-list watch works them out again for the new one.
                _host.MappingChanged();
                break;
            case WorkerMessage.MessageKind.SetThresholds:
                var thresholds = (CaptureThresholds)message.Payload!;
                _machine.Thresholds = thresholds;
                _log.Info(LogSources.Engine, "Capture thresholds changed", ("startDistancePx", thresholds.StartDistancePx), ("minSegmentPx", thresholds.MinSegmentPx), ("cancelDelayMs", thresholds.CancelDelayMs), ("resetOnMovement", thresholds.ResetCancelDelayOnMovement));
                break;
            case WorkerMessage.MessageKind.Reset:
                var reason = (string)message.Payload!;
                ResetMachine(reason);
                _log.Info(LogSources.Capture, "Capture reset", ("reason", reason));
                break;
            case WorkerMessage.MessageKind.ButtonObserved:
                _host.OnButtonObserved((MouseButton)message.Payload!);
                break;
            case WorkerMessage.MessageKind.KeyCaptured:
                _host.OnKeyCaptured((KeyCaptureEvent)message.Payload!);
                return;
            case WorkerMessage.MessageKind.Notify:
                ((Action)message.Payload!)();
                return;
            case WorkerMessage.MessageKind.Hold:
                OnHold(message);
                return;
            case WorkerMessage.MessageKind.HoldReplay:
                OnHoldReplay(message);
                return;
        }

        AfterMachineChange();
    }

    private void OnInput(CaptureEvent e, bool hookSuppressed)
    {
        var activeBefore = _machine.ActiveButton;
        var outcomes = _machine.Handle(e);
        _gate.PublishState(_machine.State);
        if (e is CaptureEvent.ButtonDown or CaptureEvent.ButtonUp or CaptureEvent.Wheel)
        {
            CrossCheck(e, hookSuppressed, outcomes, activeBefore);
        }

        foreach (var outcome in outcomes)
        {
            Dispatch(outcome, e);
        }

        if (e is CaptureEvent.ButtonDown down && _machine.State == CaptureState.Held && _machine.ActiveButton == down.Button)
        {
            MaskWinAlt(down);
        }
    }

    /// <summary>
    /// A captured press with Win or Alt held before it (learnings 0003 §3.2): Windows would see that key go down and up with
    /// nothing between (the press was swallowed) and open Start or the menu bar; one Ctrl tap now makes it a combination, as
    /// SP.net does for its hotkeys. Windows only; macOS does nothing on a lone Cmd or Option.
    /// </summary>
    private void MaskWinAlt(CaptureEvent.ButtonDown down)
    {
        if (_maskWinAlt && (down.Modifiers & (KeyModifiers.Meta | KeyModifiers.Alt)) != 0)
        {
            _simulator.KeyPress(KeyCode.LeftControl);
            _simulator.KeyRelease(KeyCode.LeftControl);
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

    /// <summary>
    /// The hard reset; a handed-back press's injected down gets its up first, so the app is never left holding it, and the
    /// hold remap releases every output and replayed key it holds.
    /// </summary>
    private void ResetMachine(string reason)
    {
        if (_machine.HandedBackButton is { } button)
        {
            _simulator.Release(button);
        }

        _machine.Reset();
        EndTrail();
        ResetHold(reason);
    }

    private void CrossCheck(CaptureEvent e, bool hookSuppressed, IReadOnlyList<CaptureOutcome> outcomes, MouseButton activeBefore)
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

        // A wheel tick, or a button joining a press, can race the hold-still cancel or a hand-back by one tick interval
        // (documented in README); a mismatch on the anchor's own press or release is a bug.
        var raced = e is CaptureEvent.Wheel || (e is CaptureEvent.ButtonDown down && down.Button != activeBefore) || (e is CaptureEvent.ButtonUp up && up.Button != activeBefore);
        var level = raced ? EventLevel.Debug : EventLevel.Warning;
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
            ResetMachine("events dropped");
            AfterMachineChange();
        }
    }
}
