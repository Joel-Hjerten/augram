using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Diagnostics;
using Augram.Core.Mapping;
using Augram.Engine.Execution;

namespace Augram.Engine.Hosting;

/// <summary>The outcome half of <see cref="EngineWorker"/>: what each <see cref="CaptureOutcome"/> makes the worker do. Acts first, logs second.</summary>
internal sealed partial class EngineWorker
{
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
                var result = ClickRelay.Replay(_simulator, replay.Button, replay.X, replay.Y, replay.AfterKeys);
                _log.Debug(LogSources.Capture, "Click replayed", ("button", replay.Button), ("x", replay.X), ("y", replay.Y), ("keys", replay.AfterKeys), ("result", result), ("lagMs", _clock.MonotonicMs - e.TimestampMs));
                break;
            case CaptureOutcome.ClickTrigger click:
                var relay = click.Hold.After == HeldButtons.None ? new ClickRelay(click.Button, click.X, click.Y, click.Hold.AfterKeys) : null;
                Fire(null, new PressedTrigger(Trigger.Click, click.Hold), click.Start, null, relay);
                break;
            case CaptureOutcome.HandBack back:
                var pressed = _simulator.Press(back.Button, back.Start.X, back.Start.Y);
                var moved = _simulator.MoveTo(back.X, back.Y);
                _log.Debug(LogSources.Capture, "Press handed back", ("button", back.Button), ("x", back.Start.X), ("y", back.Start.Y), ("result", pressed == SimulationResult.Success ? moved : pressed));
                break;
            case CaptureOutcome.ReleaseHandedBack release:
                var released = _simulator.Release(release.Button);
                _log.Debug(LogSources.Capture, "Handed-back press released", ("button", release.Button), ("x", release.X), ("y", release.Y), ("result", released));
                break;
            case CaptureOutcome.StrokeComplete stroke:
                Recognize(stroke);
                break;
            case CaptureOutcome.WheelTrigger wheel when wheel.AfterDrawing:
                // SP.net: after drawing, a tick is "this gesture + wheel", which no command is; the no-gesture wheel commands do not fire.
                _log.Debug(LogSources.Capture, "Wheel after drawing; no command", ("direction", wheel.Direction));
                break;
            case CaptureOutcome.WheelTrigger wheel:
                _log.Info(LogSources.Capture, "Wheel trigger", ("direction", wheel.Direction), ("x", wheel.Start.X), ("y", wheel.Start.Y));
                var wheelEvent = new EngineEvent.WheelTriggered(wheel.Direction, wheel.Start);
                _host.Raise(wheelEvent);
                Fire(wheelEvent, new PressedTrigger(Trigger.ForWheel(wheel.Direction), wheel.Hold), wheel.Start, null, null);
                break;
            case CaptureOutcome.Cancelled { Reason: CancelReason.ReleasedElsewhere } when e is CaptureEvent.ButtonReleasedElsewhere elsewhere:
                // Rare and worth seeing: another program took the release (plan 0005 decision 10), so nothing is replayed or fired.
                _log.Info(LogSources.Capture, "Press released elsewhere", ("button", elsewhere.Button), ("x", elsewhere.X), ("y", elsewhere.Y));
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
            var (engineEvent, draft) = _recognizer.Recognize(stroke, _gate.TakeWorstHandlerMicroseconds());
            _host.PublishStrokeLatency(Math.Max(0, _clock.MonotonicMs - stroke.Points[^1].TimestampMs));
            _host.Raise(engineEvent);
            if (engineEvent is EngineEvent.GestureRecognized recognized)
            {
                Fire(engineEvent, new PressedTrigger(Trigger.ForGesture(recognized.GestureId), stroke.Hold), recognized.Start, draft, null);
            }
        }
        catch (Exception exception)
        {
            _log.Error(LogSources.Recognition, "Recognition failed", exception, ("points", stroke.Points.Count));
        }
    }

    /// <summary>
    /// After the event is raised: the App's intercept may claim it (training popup, F3/A6; a click has no event and is never
    /// claimed); else the executor gets it, with the click to relay when nothing fires; else (no Mapping port: recognise and
    /// report only, as in M1) the draft is completed with the reason and a click is relayed at once. Acts first, logs second.
    /// </summary>
    private void Fire(EngineEvent? engineEvent, PressedTrigger trigger, CapturePoint start, RecognitionLogEntry? draft, ClickRelay? relay)
    {
        if (engineEvent is not null && Intercepted(engineEvent))
        {
            if (draft is not null)
            {
                _recognitionLog.Add(draft with { NothingFiredReason = ConsumedReason });
            }

            _log.Debug(LogSources.Capture, "Stroke consumed", ("trigger", trigger.Describe()));
            return;
        }

        if (_executor is not null)
        {
            _executor.Enqueue(new ExecutionRequest(trigger, start, draft) { Relay = relay });
            return;
        }

        relay?.Run(_simulator);
        if (draft is not null)
        {
            _recognitionLog.Add(draft with { NothingFiredReason = NoMappingReason });
        }
    }

    private bool Intercepted(EngineEvent engineEvent)
    {
        if (_intercept is null)
        {
            return false;
        }

        try
        {
            return _intercept(engineEvent);
        }
        catch (Exception exception)
        {
            _log.Error(LogSources.Engine, "Intercept threw", exception, ("event", engineEvent.GetType().Name));
            return false;
        }
    }
}
