using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Steps.Remap;

namespace Augram.Core.HoldRemaps;

/// <summary>
/// One active hold remap of a <see cref="HoldRemapPlan"/>: its hold key and tap time, its inputs as bit sets (every button
/// some input names, the wheel directions, a 256-bit key set) for the hook, and its commands as <see cref="HoldBinding"/>s for
/// the worker's <see cref="HoldRemapMachine"/>. Immutable. The <c>IsInput</c> reads and <see cref="HoldsButtonOutput"/> are a
/// mask each and allocate nothing, so the hook may call them (CLAUDE.md invariant 1); the <c>For…</c> lookups walk the bindings
/// without allocating either.
/// </summary>
public sealed class HoldRemapEntry
{
    private readonly HoldBinding[] _bindings;
    private readonly int _wheel;

    /// <summary>Bit <c>(int)set</c> is set when <see cref="ForButtons"/>(set) is a Remap command with a button output (sets of physical buttons: below 64).</summary>
    private readonly ulong _buttonOutputSets;

    public HoldRemapEntry(HoldRemap holdRemap, IEnumerable<HoldBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(holdRemap);
        ArgumentNullException.ThrowIfNull(bindings);
        Id = holdRemap.Id;
        Name = holdRemap.Name;
        HoldKey = holdRemap.HoldKey;
        TapTimeMs = holdRemap.TapTimeMs;
        _bindings = [.. bindings];
        foreach (var binding in _bindings)
        {
            switch (binding.Input)
            {
                case HoldInput.Buttons buttons:
                    Buttons |= buttons.Set & HeldButtonsExtensions.Physical;
                    break;
                case HoldInput.Wheel wheel:
                    _wheel |= 1 << (int)wheel.Direction;
                    break;
                case HoldInput.Key key:
                    Keys = Keys.With(key.KeyCode);
                    break;
            }
        }

        for (var set = 1; set <= (int)HeldButtonsExtensions.Physical; set++)
        {
            if (ForButtons((HeldButtons)set)?.Output is RemapOutput.Button)
            {
                _buttonOutputSets |= 1UL << set;
            }
        }
    }

    public HoldRemapId Id { get; }

    public string Name { get; }

    public KeyCode HoldKey { get; }

    public int TapTimeMs { get; }

    /// <summary>Every physical button some input names, alone or in a set: such a button's press during the hold belongs to the hold remap.</summary>
    public HeldButtons Buttons { get; }

    /// <summary>Every key some input names.</summary>
    public KeySet Keys { get; }

    public IReadOnlyList<HoldBinding> Bindings => _bindings;

    public bool IsInput(MouseButton button) => (Buttons & button.Flag()) != HeldButtons.None;

    public bool IsInput(WheelDirection direction) => (_wheel & (1 << (int)direction)) != 0;

    public bool IsInput(KeyCode key) => Keys.Contains(key);

    /// <summary>
    /// True when the input buttons <paramref name="held"/> are exactly the set of a Remap command with a button output: what
    /// the machine holds while those buttons are down. The hook asks it when the owed set changes, to know whether physical
    /// moves are swallowed and re-posted as drags of that output (macOS, learnings 0005). One mask read.
    /// </summary>
    public bool HoldsButtonOutput(HeldButtons held)
        => (held & ~HeldButtonsExtensions.Physical) == HeldButtons.None && ((_buttonOutputSets >> (int)held) & 1) != 0;

    /// <summary>The command whose button set equals <paramref name="held"/> exactly (plan 0002 decision 7); null when none does.</summary>
    public HoldBinding? ForButtons(HeldButtons held)
    {
        foreach (var binding in _bindings)
        {
            if (binding.Input is HoldInput.Buttons buttons && buttons.Set == held)
            {
                return binding;
            }
        }

        return null;
    }

    public HoldBinding? ForWheel(WheelDirection direction)
    {
        foreach (var binding in _bindings)
        {
            if (binding.Input is HoldInput.Wheel wheel && wheel.Direction == direction)
            {
                return binding;
            }
        }

        return null;
    }

    public HoldBinding? ForKey(KeyCode key)
    {
        foreach (var binding in _bindings)
        {
            if (binding.Input is HoldInput.Key input && input.KeyCode == key)
            {
                return binding;
            }
        }

        return null;
    }
}
