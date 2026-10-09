using Augram.Core.Abstractions;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.Remap;

namespace Augram.Core.HoldRemaps;

/// <summary>
/// The key half of <see cref="HoldRemapMachine"/>: the hold key's repeats and release, input keys (claimed at their press and
/// followed until their release, after the hold key's too), modifiers (always through) and the typing rollover.
/// </summary>
public sealed partial class HoldRemapMachine
{
    private readonly List<(KeyCode Key, HoldBinding Binding)> _claimed = [];
    private readonly List<KeyCode> _replayed = [];

    private IReadOnlyList<HoldRemapOutcome> OnKey(HoldRemapEvent.Key e)
    {
        var key = e.KeyCode;
        if (_holding && key == _remap!.HoldKey)
        {
            return e.Phase == KeyPhase.Up ? OnHoldUp(e.TimestampMs) : SuppressOnly;
        }

        if (_replayed.Contains(key))
        {
            return Replayed(key, e.Phase);
        }

        if (IndexOfClaimed(key) is var index and >= 0)
        {
            return Claimed(index, e.Phase);
        }

        if (e.Phase != KeyPhase.Down || !_holding || _rolledOver)
        {
            return PassThroughOnly;
        }

        var remap = _remap!;
        if (remap.IsInput(key) && remap.ForKey(key) is { } binding)
        {
            _used = true;
            _claimed.Add((key, binding));
            var outcomes = new List<HoldRemapOutcome>(2) { HoldRemapOutcome.Suppress.Instance };
            Fire(binding, e.X, e.Y, momentary: false, outcomes);
            return outcomes;
        }

        if (HotkeyKeys.IsModifier(key) || _used || e.TimestampMs - _downAt > remap.TapTimeMs)
        {
            // Ctrl, Alt, Shift, Win never count (Shift held across a tap gives Shift + Space); past the tap time or once
            // something was used, other keys simply pass and the hold key is not sent at its release.
            return PassThroughOnly;
        }

        // Typing: "a b" fast with the space still down. The space goes first, then this key; off until the hold key is up.
        _rolledOver = true;
        _replayed.Add(key);
        return [HoldRemapOutcome.Suppress.Instance, new HoldRemapOutcome.TapHoldKey(remap.HoldKey), new HoldRemapOutcome.ReplayKey(key, KeyPhase.Down)];
    }

    /// <summary>A key whose down was replayed: its repeats and release are swallowed and replayed in turn.</summary>
    private HoldRemapOutcome[] Replayed(KeyCode key, KeyPhase phase)
    {
        if (phase == KeyPhase.Up)
        {
            _replayed.Remove(key);
            return [HoldRemapOutcome.Suppress.Instance, new HoldRemapOutcome.ReplayKey(key, KeyPhase.Up)];
        }

        return [HoldRemapOutcome.Suppress.Instance, new HoldRemapOutcome.ReplayKey(key, KeyPhase.Repeat)];
    }

    /// <summary>An input key whose down was claimed: a key output mirrors its repeats; its release releases the output.</summary>
    private IReadOnlyList<HoldRemapOutcome> Claimed(int index, KeyPhase phase)
    {
        var binding = _claimed[index].Binding;
        if (phase != KeyPhase.Up)
        {
            return binding.Output is RemapOutput.Key output
                ? [HoldRemapOutcome.Suppress.Instance, new HoldRemapOutcome.RepeatOutput(binding.CommandId, output)]
                : SuppressOnly;
        }

        _claimed.RemoveAt(index);
        var outcomes = new List<HoldRemapOutcome>(2) { HoldRemapOutcome.Suppress.Instance };
        Release(binding, outcomes);
        EndIfDone();
        return outcomes;
    }

    private int IndexOfClaimed(KeyCode key)
    {
        for (var i = 0; i < _claimed.Count; i++)
        {
            if (_claimed[i].Key == key)
            {
                return i;
            }
        }

        return -1;
    }
}
