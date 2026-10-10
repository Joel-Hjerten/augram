using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Steps.Remap;

namespace Augram.Core.HoldRemaps;

/// <summary>
/// The behaviour of hold remaps (F9, plan 0002) as a pure object, run by the engine worker exactly as it runs
/// <see cref="CaptureStateMachine"/>: feed it <see cref="HoldRemapEvent"/>s, it returns <see cref="HoldRemapOutcome"/>s. No hook,
/// no timer, no clock, no window: time arrives on the events, and it is called from one thread. The contract, event by
/// event, is the table in <c>HoldRemaps/README.md</c>; the short version:
/// <list type="bullet">
/// <item>The hold key's press is swallowed, and its repeats. At its release it is sent (a tap) only if it was released within
/// the tap time and nothing was used meanwhile: any mouse button or wheel turn, input or not, and any input key count as
/// used; Ctrl, Alt, Shift and Win do not (decision 4).</item>
/// <item>A key that is no input and no modifier, pressed within the tap time before anything was used, is typing (the
/// rollover): the hold key is tapped first, then that key is replayed (its repeats and release too), and the hold remap is
/// off until the hold key is released.</item>
/// <item>Input buttons are followed as a set: the output held is the command whose set equals the input buttons held now
/// (decision 7); on every change the old output is released first, then the new one pressed. The machine keeps following
/// them after the hold key is released, until the last is up.</item>
/// <item>Where moves are fed (macOS, whose simulator re-posts drags), a move while the set's output is a button is swallowed
/// and re-posted as a drag of that output (<see cref="HoldRemapOutcome.DragOutput"/>); otherwise it passes.</item>
/// <item>A key input's Remap key output mirrors its down, repeats and up; a Steps command runs once per press (repeats
/// ignored) and once per wheel notch.</item>
/// <item>Every output down is paired with its up (A19): at the input's release, whatever the hold key did meanwhile, or at
/// <see cref="HoldRemapEvent.Reset"/>.</item>
/// </list>
/// Split in <c>HoldRemapMachine.cs</c> (the hold key, buttons, the wheel, reset) and <c>HoldRemapMachine.Keys.cs</c>.
/// </summary>
public sealed partial class HoldRemapMachine
{
    private static readonly HoldRemapOutcome[] SuppressOnly = [HoldRemapOutcome.Suppress.Instance];
    private static readonly HoldRemapOutcome[] PassThroughOnly = [HoldRemapOutcome.PassThrough.Instance];

    private HoldRemapEntry? _remap;
    private bool _holding;
    private bool _rolledOver;
    private bool _used;
    private long _downAt;
    private HeldButtons _owed;
    private HoldBinding? _buttons;

    /// <summary>The hold key of a hold that focus moving ended (<see cref="HoldRemapEvent.FocusMoved"/>), still down: its repeats and release stay swallowed.</summary>
    private KeyCode _ended;

    public HoldRemapState State => _holding
        ? _rolledOver ? HoldRemapState.RolledOver : HoldRemapState.Holding
        : IsFollowing ? HoldRemapState.Following : HoldRemapState.Idle;

    /// <summary>The hold remap held or followed; null while idle.</summary>
    public HoldRemapEntry? Remap => _remap;

    /// <summary>Input buttons whose down was swallowed and whose up is still owed (A19).</summary>
    public HeldButtons OwedButtons => _owed;

    private bool IsFollowing => _owed != HeldButtons.None || _claimed.Count > 0;

    public IReadOnlyList<HoldRemapOutcome> Handle(HoldRemapEvent e) => e switch
    {
        HoldRemapEvent.HoldDown down => OnHoldDown(down),
        HoldRemapEvent.HoldUp up => (_holding && up.HoldKey != _remap!.HoldKey) || (!_holding && up.HoldKey == _ended && _ended != KeyCode.None)
            ? OnKey(new HoldRemapEvent.Key(up.HoldKey, KeyPhase.Up, up.TimestampMs))
            : OnHoldUp(up.TimestampMs),
        HoldRemapEvent.Button button => button.IsDown ? OnButtonDown(button) : OnButtonUp(button),
        HoldRemapEvent.Move move => OnMove(move),
        HoldRemapEvent.Wheel wheel => OnWheel(wheel),
        HoldRemapEvent.Key key => OnKey(key),
        HoldRemapEvent.FocusMoved => OnFocusMoved(),
        HoldRemapEvent.Reset => OnReset(),
        _ => throw new ArgumentOutOfRangeException(nameof(e), e, "Unknown hold remap event."),
    };

    private IReadOnlyList<HoldRemapOutcome> OnHoldDown(HoldRemapEvent.HoldDown down)
    {
        var entry = down.Remap;
        if (_holding)
        {
            // The same key again is a repeat reported as a press; another hold key is an ordinary key of this hold (decision 6).
            return entry.HoldKey == _remap!.HoldKey ? SuppressOnly : OnKey(new HoldRemapEvent.Key(entry.HoldKey, KeyPhase.Down, down.TimestampMs));
        }

        if (IsFollowing && entry.Id != _remap!.Id)
        {
            // Another hold remap's key while this one still follows its buttons: not a hold (the hook should not have claimed it).
            return PassThroughOnly;
        }

        // A new hold, or the same hold remap again while it follows buttons or keys still down (those count as used).
        _ended = entry.HoldKey == _ended ? KeyCode.None : _ended;
        _remap = entry;
        _holding = true;
        _rolledOver = false;
        _used = IsFollowing;
        _downAt = down.TimestampMs;
        return SuppressOnly;
    }

    private IReadOnlyList<HoldRemapOutcome> OnHoldUp(long timestampMs)
    {
        if (!_holding)
        {
            return SuppressOnly;
        }

        var remap = _remap!;
        var tap = !_rolledOver && !_used && timestampMs - _downAt <= remap.TapTimeMs;
        _holding = false;
        _rolledOver = false;
        EndIfDone();
        return tap ? [HoldRemapOutcome.Suppress.Instance, new HoldRemapOutcome.TapHoldKey(remap.HoldKey)] : SuppressOnly;
    }

    private IReadOnlyList<HoldRemapOutcome> OnButtonDown(HoldRemapEvent.Button down)
    {
        var flag = down.MouseButton.Flag();
        if ((_owed & flag) != HeldButtons.None)
        {
            // Its release was missed: still ours, still owed.
            return SuppressOnly;
        }

        _used |= _holding;
        var engaged = _holding ? !_rolledOver : _owed != HeldButtons.None;
        if (!engaged || !_remap!.IsInput(down.MouseButton))
        {
            return PassThroughOnly;
        }

        _owed |= flag;
        var outcomes = new List<HoldRemapOutcome>(3) { HoldRemapOutcome.Suppress.Instance };
        Follow(down.X, down.Y, outcomes);
        return outcomes;
    }

    private IReadOnlyList<HoldRemapOutcome> OnButtonUp(HoldRemapEvent.Button up)
    {
        var flag = up.MouseButton.Flag();
        if ((_owed & flag) == HeldButtons.None)
        {
            return PassThroughOnly;
        }

        _owed &= ~flag;
        var outcomes = new List<HoldRemapOutcome>(3) { HoldRemapOutcome.Suppress.Instance };
        Follow(up.X, up.Y, outcomes);
        EndIfDone();
        return outcomes;
    }

    /// <summary>Rolling (decision 7): the command whose set equals the owed buttons now; on a change, release the old output, then fire the new.</summary>
    private void Follow(int x, int y, List<HoldRemapOutcome> outcomes)
    {
        var next = _owed == HeldButtons.None ? null : _remap!.ForButtons(_owed);
        if (next == _buttons)
        {
            return;
        }

        Release(_buttons, outcomes);
        _buttons = next;
        if (next is not null)
        {
            Fire(next, x, y, momentary: false, outcomes);
        }
    }

    /// <summary>
    /// A physical move (fed only where the simulator re-posts drags): while the owed set's output is a button, Suppress and
    /// re-post it as a drag of that output; otherwise PassThrough. Changes nothing: what is held follows the buttons alone.
    /// </summary>
    private IReadOnlyList<HoldRemapOutcome> OnMove(HoldRemapEvent.Move move)
        => _buttons is { Output: RemapOutput.Button button } binding
            ? [HoldRemapOutcome.Suppress.Instance, new HoldRemapOutcome.DragOutput(binding.CommandId, button, move.X, move.Y)]
            : PassThroughOnly;

    private IReadOnlyList<HoldRemapOutcome> OnWheel(HoldRemapEvent.Wheel wheel)
    {
        if (!_holding || _rolledOver)
        {
            return PassThroughOnly;
        }

        _used = true;
        if (!_remap!.IsInput(wheel.Direction))
        {
            return PassThroughOnly;
        }

        var outcomes = new List<HoldRemapOutcome>(3) { HoldRemapOutcome.Suppress.Instance };
        if (_remap.ForWheel(wheel.Direction) is { } binding)
        {
            Fire(binding, wheel.X, wheel.Y, momentary: true, outcomes);
        }

        return outcomes;
    }

    /// <summary>
    /// The app in front is no longer the hold's: a hold with nothing owed (no input button, no input key) ends without a tap,
    /// and its hold key's repeats and release stay swallowed (<see cref="_ended"/>); with inputs owed the hold keeps following
    /// them. No input decision: nothing physical happened.
    /// </summary>
    private IReadOnlyList<HoldRemapOutcome> OnFocusMoved()
    {
        if (!_holding || IsFollowing)
        {
            return [];
        }

        var key = _remap!.HoldKey;
        _holding = false;
        _rolledOver = false;
        _ended = key;
        EndIfDone();
        return [new HoldRemapOutcome.HoldEnded(key)];
    }

    /// <summary>Releases every output still held (the button set's, each input key's, each replayed key) and forgets everything.</summary>
    private List<HoldRemapOutcome> OnReset()
    {
        var outcomes = new List<HoldRemapOutcome>();
        Release(_buttons, outcomes);
        for (var i = _claimed.Count - 1; i >= 0; i--)
        {
            Release(_claimed[i].Binding, outcomes);
        }

        foreach (var key in _replayed)
        {
            outcomes.Add(new HoldRemapOutcome.ReplayKey(key, KeyPhase.Up));
        }

        _claimed.Clear();
        _replayed.Clear();
        _owed = HeldButtons.None;
        _buttons = null;
        _holding = false;
        _rolledOver = false;
        _used = false;
        _remap = null;
        _ended = KeyCode.None;
        return outcomes;
    }

    /// <summary>
    /// What a binding does at its input's press: a Remap output pressed (and at once released when <paramref name="momentary"/>,
    /// a wheel notch's), a wheel output turned, a Steps command run; nothing for a command that does nothing.
    /// </summary>
    private static void Fire(HoldBinding binding, int x, int y, bool momentary, List<HoldRemapOutcome> outcomes)
    {
        switch (binding.Output)
        {
            case RemapOutput.Wheel wheel:
                outcomes.Add(new HoldRemapOutcome.WheelOutput(binding.CommandId, wheel, x, y));
                break;
            case { } output:
                outcomes.Add(new HoldRemapOutcome.PressOutput(binding.CommandId, output, x, y));
                if (momentary)
                {
                    outcomes.Add(new HoldRemapOutcome.ReleaseOutput(binding.CommandId, output));
                }

                break;
            case null when binding.RunsSteps:
                outcomes.Add(new HoldRemapOutcome.RunSteps(binding.CommandId, x, y));
                break;
        }
    }

    /// <summary>The release of what <paramref name="binding"/> holds down: a button or key output; nothing for a wheel output, steps or nothing.</summary>
    private static void Release(HoldBinding? binding, List<HoldRemapOutcome> outcomes)
    {
        if (binding?.Output is { } output and not RemapOutput.Wheel)
        {
            outcomes.Add(new HoldRemapOutcome.ReleaseOutput(binding.CommandId, output));
        }
    }

    private void EndIfDone()
    {
        if (!_holding && !IsFollowing)
        {
            _remap = null;
            _buttons = null;
        }
    }
}
