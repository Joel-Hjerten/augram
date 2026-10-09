using Augram.Core.Abstractions;
using Augram.Core.Capture;

namespace Augram.Engine.Input;

/// <summary>
/// The hook thread's copy of the facts it needs to answer "suppress?" without running the
/// <see cref="CaptureStateMachine"/>: which anchor owns the press in progress, and which buttons' consumed downs it still
/// owes a consumed up for (checklist A19, now per button). The machine itself lives on the engine worker and lags the
/// hook by the queue depth, so its state cannot be trusted for the pairing; it is read only where a stale answer costs
/// one event (a wheel tick, a button joining a press just after it was cancelled or handed back).
/// <para>
/// Derivation from the Capture README table, event by event (<c>SuppressionShadowTests</c> and <c>ChordPairingTests</c>
/// prove it equals the machine's own decision over random sequences):
/// a press with no press in progress, or of the owner again (a missed release): suppress iff the button is an anchor
/// (the stroke button, or one the window's <see cref="AnchorPlan"/> holds back), capture is allowed and the ignore key is
/// up; it then owns the press. Another button while the press is held and not frozen (no wheel tick, not cancelled, not
/// handed back): suppress iff the press's plan claims it for this owner. A release: suppress iff its press was suppressed,
/// whatever happened in between. A wheel tick: suppress iff a press is owned and the machine has not cancelled it or
/// handed it back. Moves never. Keys are <see cref="KeySuppressionShadow"/>'s, which asks <see cref="KeyClaim"/>.
/// </para>
/// </summary>
public sealed class SuppressionShadow
{
    private const int NoButton = -1;

    private int _owner = NoButton;
    private bool _ownerIsStroke;
    private int _owed;
    private AnchorPlan _plan;
    private bool _wheel;
    private KeyModifiers _beforeKeys;

    /// <summary>The anchor that owns the press in progress, or null.</summary>
    public MouseButton? Owed
    {
        get
        {
            var owner = Volatile.Read(ref _owner);
            return owner == NoButton ? null : (MouseButton)owner;
        }
    }

    /// <summary>Every button whose consumed down still owes a consumed up, the owner included.</summary>
    public HeldButtons OwedButtons => (HeldButtons)Volatile.Read(ref _owed);

    /// <summary>Everything this shadow knows, to undo a decision whose event could not be enqueued.</summary>
    public Snapshot Save() => new(_owner, _ownerIsStroke, _owed, _plan, _wheel, _beforeKeys);

    public void Restore(Snapshot snapshot)
    {
        _ownerIsStroke = snapshot.OwnerIsStroke;
        _plan = snapshot.Plan;
        _wheel = snapshot.Wheel;
        _beforeKeys = snapshot.BeforeKeys;
        Volatile.Write(ref _owed, snapshot.Owed);
        Volatile.Write(ref _owner, snapshot.Owner);
    }

    /// <summary>Forget every owed release. Only when the hook was reinstalled or the OS state is otherwise unknown.</summary>
    public void Reset() => Restore(new Snapshot(NoButton, false, 0, AnchorPlan.None, false, KeyModifiers.None));

    /// <summary>
    /// The keys a modifier press starting now would join the press as (an After key, consumed): Ctrl, Alt, Shift and Win not
    /// already held when the anchor went down, while a press is owned and not frozen; none otherwise.
    /// </summary>
    public KeyModifiers KeyClaim(CaptureState machineState)
        => Volatile.Read(ref _owner) == NoButton || Frozen(machineState) ? KeyModifiers.None : PressHold.TrackedKeys & ~_beforeKeys;

    /// <summary>The decision for one event without an anchor plan (only the stroke button is an anchor).</summary>
    public bool Decide(in RawInput input, CaptureState machineState, MouseButton strokeButton, bool captureAllowed, bool ignoreKeyHeld)
        => Decide(in input, machineState, strokeButton, captureAllowed, ignoreKeyHeld, AnchorPlan.None);

    /// <summary>
    /// Decides for one event and updates the record. <paramref name="machineState"/> is the worker's latest published state;
    /// <paramref name="strokeButton"/> the configured button; the two flags are the press verdict the <c>ButtonDown</c> event
    /// will carry; <paramref name="plan"/> the anchor plan for the window under the pointer, read once at this event.
    /// </summary>
    public bool Decide(in RawInput input, CaptureState machineState, MouseButton strokeButton, bool captureAllowed, bool ignoreKeyHeld, AnchorPlan plan)
    {
        var owner = Volatile.Read(ref _owner);
        var flag = (int)input.Button.Flag();
        switch (input.Kind)
        {
            case RawInputKind.ButtonDown:
                if (owner == NoButton || (int)input.Button == owner)
                {
                    var isAnchor = input.Button == strokeButton || plan.IsAnchor(input.Button);
                    if (isAnchor && captureAllowed && !ignoreKeyHeld)
                    {
                        _ownerIsStroke = input.Button == strokeButton;
                        _plan = plan;
                        _wheel = false;
                        _beforeKeys = input.Modifiers & PressHold.TrackedKeys;
                        Volatile.Write(ref _owed, _owed | flag);
                        Volatile.Write(ref _owner, (int)input.Button);
                        return true;
                    }

                    if (owner != NoButton)
                    {
                        Volatile.Write(ref _owner, NoButton);
                    }

                    Volatile.Write(ref _owed, _owed & ~flag);
                    return false;
                }

                var joins = !Frozen(machineState) && _plan.Claims((MouseButton)owner, _ownerIsStroke, input.Button);
                Volatile.Write(ref _owed, joins ? _owed | flag : _owed & ~flag);
                return joins;

            case RawInputKind.ButtonUp:
                var owed = (_owed & flag) != 0;
                Volatile.Write(ref _owed, _owed & ~flag);
                if ((int)input.Button == owner)
                {
                    Volatile.Write(ref _owner, NoButton);
                }

                return owed;

            case RawInputKind.Wheel:
                var suppress = owner != NoButton && machineState is not (CaptureState.Cancelled or CaptureState.HandedBack);
                _wheel |= suppress;
                return suppress;

            default:
                return false;
        }
    }

    /// <summary>A wheel tick froze the press, or the machine cancelled it or handed it back: nothing joins it any more.</summary>
    private bool Frozen(CaptureState machineState)
        => _wheel || machineState is CaptureState.WheelFiring or CaptureState.Cancelled or CaptureState.HandedBack;

    /// <summary>What <see cref="Save"/> returns; opaque to callers.</summary>
    public readonly record struct Snapshot(int Owner, bool OwnerIsStroke, int Owed, AnchorPlan Plan, bool Wheel, KeyModifiers BeforeKeys);
}
