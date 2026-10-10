namespace Augram.Core.Capture;

/// <summary>The pointer, wheel, clock and key half of <see cref="CaptureStateMachine"/>.</summary>
public sealed partial class CaptureStateMachine
{
    private IReadOnlyList<CaptureOutcome> OnMove(CaptureEvent.Move move)
    {
        switch (State)
        {
            case CaptureState.Idle:
                return PassThroughOnly;
            case CaptureState.Held:
                var point = new CapturePoint(move.X, move.Y, move.TimestampMs);
                _pointer = (move.X, move.Y);
                TryRecord(point);
                var distance = _ownerIsStroke ? Thresholds.StartDistancePx : _drags.For(_owner, Thresholds.ButtonDragDistancePx);
                if (point.DistanceSquaredTo(_start) < Squared(distance))
                {
                    return None;
                }

                if (!_ownerIsStroke)
                {
                    // A drag of a held-back button: give it back at once, so the drag or the selection starts where it began.
                    // It never draws, so its own (shorter) distance decides: the largest its commands here set, else Options'.
                    return HandBackNow(move.X, move.Y, null);
                }

                State = CaptureState.Drawing;
                var outcomes = new CaptureOutcome[_points.Count];
                outcomes[0] = new CaptureOutcome.BeginStroke(_start);
                for (var i = 1; i < _points.Count; i++)
                {
                    outcomes[i] = new CaptureOutcome.StrokeProgress(_points[i]);
                }

                return outcomes;
            case CaptureState.Drawing:
                var next = new CapturePoint(move.X, move.Y, move.TimestampMs);
                return TryRecord(next) ? [new CaptureOutcome.StrokeProgress(next)] : None;
            default:
                return None;
        }
    }

    private IReadOnlyList<CaptureOutcome> OnWheel(CaptureEvent.Wheel wheel)
    {
        var state = State;
        if (state is not (CaptureState.Held or CaptureState.Drawing or CaptureState.WheelFiring))
        {
            return PassThroughOnly;
        }

        if (state == CaptureState.WheelFiring)
        {
            return [CaptureOutcome.Suppress.Instance, WheelTrigger(wheel.Direction)];
        }

        // The first tick freezes the press: later keys and buttons are no longer recorded, and the deadline is abandoned.
        State = CaptureState.WheelFiring;
        _wheelAfterDrawing = state == CaptureState.Drawing;
        return _wheelAfterDrawing
            ? [CaptureOutcome.Suppress.Instance, CaptureOutcome.EndStroke.Instance, WheelTrigger(wheel.Direction)]
            : [CaptureOutcome.Suppress.Instance, WheelTrigger(wheel.Direction)];
    }

    private IReadOnlyList<CaptureOutcome> OnTick(CaptureEvent.Tick tick)
    {
        if (State is not (CaptureState.Held or CaptureState.Drawing) || tick.TimestampMs < _deadlineMs)
        {
            return None;
        }

        if (!_ownerIsStroke)
        {
            // A long press of a held-back button reaches the app after the hold-still time (press-and-hold, games).
            return HandBackNow(_pointer.X, _pointer.Y, null);
        }

        return Cancel(State == CaptureState.Drawing ? CancelDrawing : CancelHeld);
    }

    /// <summary>A Ctrl, Alt, Shift or Win press the hook consumed while held: one of the press's After keys (sticky; repeats change nothing).</summary>
    private IReadOnlyList<CaptureOutcome> OnKey(CaptureEvent.Key key)
    {
        var modifier = key.Modifier & PressHold.TrackedKeys;
        if (key.Consumed && State is CaptureState.Held or CaptureState.Drawing && ((_beforeKeys | _afterKeys) & modifier) == 0)
        {
            _afterKeys |= modifier;
            Push(key.TimestampMs);
        }

        return None;
    }

    private CaptureOutcome.WheelTrigger WheelTrigger(WheelDirection direction)
        => new(direction, _start) { Hold = Hold(), AfterDrawing = _wheelAfterDrawing };

    /// <summary>Hands a held-back anchor back to the app: its down at the start point, the pointer back at (x, y).</summary>
    private CaptureOutcome[] HandBackNow(int x, int y, CaptureOutcome? decision)
    {
        State = CaptureState.HandedBack;
        var handBack = new CaptureOutcome.HandBack(_owner, _start, x, y);
        return decision is null ? [handBack] : [decision, handBack];
    }

    /// <summary>Something joined the press: the hold-still deadline starts again from now (never earlier than it was).</summary>
    private void Push(long timestampMs) => _deadlineMs = Math.Max(_deadlineMs, timestampMs + Thresholds.CancelDelayMs);

    /// <summary>Decimation: keep the point only if it is at least MinSegmentPx from the last kept one.</summary>
    private bool TryRecord(CapturePoint point)
    {
        if (point.DistanceSquaredTo(_lastRecorded) < Squared(Thresholds.MinSegmentPx))
        {
            return false;
        }

        _points.Add(point);
        _lastRecorded = point;
        if (Thresholds.ResetCancelDelayOnMovement)
        {
            _deadlineMs = point.TimestampMs + Thresholds.CancelDelayMs;
        }

        return true;
    }

    private IReadOnlyList<CaptureOutcome> Cancel(CaptureOutcome[] outcomes)
    {
        State = CaptureState.Cancelled;
        return outcomes;
    }
}
