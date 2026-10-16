using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.HoldRemaps;
using Augram.Engine.Input;

namespace Augram.Engine.Hosting;

/// <summary>
/// The hold remap half of <see cref="InputGate"/> (F9, plan 0002): the four hook-thread methods that ask the
/// <see cref="HoldRemapShadow"/> before gesture capture and post one <c>Hold</c> message per decision. Split from
/// <c>InputGate.cs</c> so <see cref="Handle"/> reads in one screen; the same class, the same fields, nothing added on the hook
/// thread (Engine README, "Hold remaps").
/// </summary>
internal sealed partial class InputGate
{
    /// <summary>
    /// A hold key held with nothing owed, and the published plan is now another app group's or none (Joel, 2026-10-10): the
    /// hold ends here, before this event is decided, and the machine is told in order (<see cref="HoldRemapEvent.FocusMoved"/>).
    /// Seen at the next button, wheel or key event, which is the first moment it matters. One volatile read while holding.
    /// </summary>
    private void EndHoldIfFocusMoved(long timestampMs)
    {
        if (!_hold.Holding || !_hold.FocusMovedFrom(Volatile.Read(ref _foreground).GroupId))
        {
            return;
        }

        var before = _hold.Save();
        _hold.EndByFocus();
        if (!Post(WorkerMessage.Hold(new HoldRemapEvent.FocusMoved(timestampMs), hookSuppressed: true), critical: true))
        {
            _hold.Restore(before);
        }
    }

    /// <summary>
    /// A button's down or up while a hold is engaged: the hold remap's decision, posted as one <c>Hold</c> message. A press
    /// that cannot be enqueued is undone (it goes on to gesture capture as if the hold had passed it); a release is decided
    /// all the same (A19), and the worker resets the machine when it sees the drop.
    /// </summary>
    private bool DecideHoldButton(in RawInput input)
    {
        if (!_hold.Engaged)
        {
            return false;
        }

        var isDown = input.Kind == RawInputKind.ButtonDown;
        var before = _hold.Save();
        var taken = _hold.DecideButton(in input);
        var e = new HoldRemapEvent.Button(input.Button, isDown, input.X, input.Y, input.TimestampMs);
        if (Post(WorkerMessage.Hold(e, taken), critical: true) || !isDown)
        {
            return taken;
        }

        _hold.Restore(before);
        return false;
    }

    /// <summary>A wheel notch while a hold key is held: the hold remap's decision (every notch counts as used), posted; undone when it cannot be enqueued.</summary>
    private bool DecideHoldWheel(in RawInput input)
    {
        if (!_hold.Holding)
        {
            return false;
        }

        var before = _hold.Save();
        var taken = _hold.DecideWheel(in input);
        if (Post(WorkerMessage.Hold(new HoldRemapEvent.Wheel(input.Wheel, input.X, input.Y, input.TimestampMs), taken), critical: true))
        {
            return taken;
        }

        _hold.Restore(before);
        return false;
    }

    /// <summary>
    /// A key event, hold remaps first: the hold key's press is offered when the foreground's plan has it and nothing else
    /// stands in the way (enabled, not paused by a focused app, no global ignore key and no input of the app in front's While
    /// holding entries held (plan 0012), no hotkey capture, no gesture press owned, a fresh press; "over an ignored app" does
    /// not count: Blender is ignored that way and keeps its hold remap). One message per decision; a press that cannot be
    /// enqueued is undone, a release is still swallowed.
    /// </summary>
    private bool DecideHoldKey(in RawInput input, bool capturing)
    {
        var isUp = input.Kind == RawInputKind.KeyUp;
        var fresh = !isUp && _keys.IsFreshPress(in input);
        HoldRemapEntry? candidate = null;
        HoldRemapPlan? plan = null;
        if (fresh && !capturing && !_hold.Holding)
        {
            plan = Volatile.Read(ref _foreground);
            if (!plan.IsEmpty
                && Enabled
                && (IgnoreState & PausedByFocus) == 0
                && !_shadow.Owed.HasValue)
            {
                candidate = plan.Find(input.Key);
                // A global ignore key or an input of the app in front's While holding entries held (plan 0012 decision 8):
                // decided at this press only, so an input pressed after the hold key leaves a running hold remap alone.
                if (candidate is not null && IgnoredAtPress(Volatile.Read(ref _foregroundHolding), in input, HeldButtons.None).IsSet)
                {
                    candidate = null;
                }
            }
        }

        if (candidate is null && _hold.Idle && (!fresh || capturing || _hold.PendingReplays == 0))
        {
            return false;
        }

        var before = _hold.Save();
        var decision = _hold.DecideKey(input.Key, isUp, fresh, input.TimestampMs, candidate, capturing, plan?.GroupId);
        if (decision.Verdict == HoldKeyVerdict.None)
        {
            return false;
        }

        HoldRemapEvent e = decision.Verdict switch
        {
            HoldKeyVerdict.HoldDown => new HoldRemapEvent.HoldDown(candidate!, input.TimestampMs),
            HoldKeyVerdict.HoldUp => new HoldRemapEvent.HoldUp(input.Key, input.TimestampMs),
            _ => new HoldRemapEvent.Key(input.Key, decision.Phase, input.TimestampMs, _pointerX, _pointerY),
        };
        var message = decision.Verdict == HoldKeyVerdict.Ordered
            ? WorkerMessage.HoldReplay((HoldRemapEvent.Key)e)
            : WorkerMessage.Hold(e, decision.Suppress, decision.AwaitsInjection, decision.Verdict == HoldKeyVerdict.HoldDown ? plan?.GroupName : null);
        if (decision.AwaitsInjection)
        {
            _hold.ReplayPosted();
        }

        if (Post(message, critical: true))
        {
            return decision.Suppress;
        }

        if (decision.AwaitsInjection)
        {
            _hold.ReplayDone();
        }

        if (isUp)
        {
            return decision.Suppress;
        }

        _hold.Restore(before);
        return false;
    }
}
