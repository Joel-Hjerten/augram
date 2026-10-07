using Augram.Core.Abstractions;
using Augram.Core.Capture;

namespace Augram.Engine.Input;

/// <summary>
/// The hook thread's copy of the one fact it needs to answer "suppress?" without running the
/// <see cref="CaptureStateMachine"/>: which button's consumed press it still owes a consumed release
/// for (checklist A19). The machine itself lives on the engine worker and lags the hook by the
/// queue depth, so its state cannot be trusted for the press/release pairing; it is read only for
/// the wheel decision, where a stale answer costs one scroll tick at most.
/// <para>
/// Derivation from the Capture README table, event by event (<c>SuppressionShadowTests</c>
/// proves it equals the machine's own decision over random sequences):
/// stroke-button press: suppress iff capture is allowed and the ignore key is up (the machine says
/// the same from every state, since a press while not Idle restarts the capture); stroke-button
/// release: suppress iff the press was suppressed; the "stroke button" for both is the owed button
/// while one is owed, so a button change mid-capture keeps consuming the old button (the machine's
/// <c>_activeButton</c>); other buttons are never suppressed; a wheel tick is suppressed iff the
/// machine is Held, Drawing or WheelFiring; moves never are. Keys are <see cref="KeySuppressionShadow"/>'s (hotkey capture).
/// </para>
/// </summary>
public sealed class SuppressionShadow
{
    private const int NoButton = -1;
    private int _owed = NoButton;

    /// <summary>The button whose release is still owed, or null. Snapshot for undoing a failed enqueue.</summary>
    public MouseButton? Owed
    {
        get
        {
            var owed = Volatile.Read(ref _owed);
            return owed == NoButton ? null : (MouseButton)owed;
        }
    }

    /// <summary>Forget any owed release. Only when the hook was reinstalled or the OS state is otherwise unknown.</summary>
    public void Reset() => Volatile.Write(ref _owed, NoButton);

    public void Restore(MouseButton? owed) => Volatile.Write(ref _owed, owed.HasValue ? (int)owed.Value : NoButton);

    /// <summary>
    /// Decides for one event and updates the owed button. <paramref name="machineState"/> is the worker's
    /// latest published state; <paramref name="strokeButton"/> the configured button; the two flags are the
    /// press verdict the <c>ButtonDown</c> event will carry.
    /// </summary>
    public bool Decide(in RawInput input, CaptureState machineState, MouseButton strokeButton, bool captureAllowed, bool ignoreKeyHeld)
    {
        var owed = Volatile.Read(ref _owed);
        var active = owed == NoButton ? strokeButton : (MouseButton)owed;
        switch (input.Kind)
        {
            case RawInputKind.ButtonDown:
                if (input.Button != active)
                {
                    return false;
                }

                var suppress = captureAllowed && !ignoreKeyHeld;
                Volatile.Write(ref _owed, suppress ? (int)input.Button : NoButton);
                return suppress;

            case RawInputKind.ButtonUp:
                if (input.Button != active)
                {
                    return false;
                }

                Volatile.Write(ref _owed, NoButton);
                return owed != NoButton;

            case RawInputKind.Wheel:
                return machineState is CaptureState.Held or CaptureState.Drawing or CaptureState.WheelFiring;

            default:
                return false;
        }
    }
}
