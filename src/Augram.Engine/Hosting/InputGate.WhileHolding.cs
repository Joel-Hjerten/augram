using System.Numerics;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;

namespace Augram.Engine.Hosting;

/// <summary>
/// The held-input check of <see cref="InputGate"/> (plan 0012, Engine README "While holding"): at a press that would start
/// something (an anchor's press, a hold key's press), a global ignore key or an input of the Exclusions › While holding entries
/// over the window held makes it pass through as ignored. Read from the hook's own records, never remembered and never an OS
/// call: no allocation, no LINQ.
/// </summary>
internal sealed partial class InputGate
{
    /// <summary>
    /// What makes the press in <paramref name="input"/> pass through as ignored, or <see cref="IgnoredBy.None"/>. First the
    /// global ignore keys, from the event's mask as always; then <paramref name="holding"/>'s inputs (decision 10: only what the
    /// OS saw go down counts, never a key or button Augram swallowed): a modifier from the event's mask limited to modifier
    /// keys whose press passed (<see cref="Input.KeySuppressionShadow.PassedModifiers"/>), a key whose press passed and is no
    /// older than the record's lost-release age (<see cref="Input.KeySuppressionShadow.IsHeldByOs"/>), a button whose press
    /// passed (<see cref="Input.SuppressionShadow.PassedButtons"/>), never the key or button being pressed
    /// (<paramref name="pressing"/>). A few reads, a loop over the listed keys; hook thread.
    /// </summary>
    private IgnoredBy IgnoredAtPress(WhileHoldingSet holding, in RawInput input, HeldButtons pressing)
    {
        var ignoreKey = input.Modifiers & IgnoreKey;
        if (ignoreKey != KeyModifiers.None)
        {
            return IgnoredBy.IgnoreKey(ignoreKey);
        }

        if (ReferenceEquals(holding, WhileHoldingSet.None))
        {
            return IgnoredBy.None;
        }

        if (holding.Modifiers != KeyModifiers.None && (input.Modifiers & holding.Modifiers & _keys.PassedModifiers()) is var modifiers and not KeyModifiers.None)
        {
            return IgnoredBy.Modifier(modifiers);
        }

        var keys = holding.Keys;
        for (var i = 0; i < keys.Count; i++)
        {
            var key = keys[i];
            if (key != input.Key && _keys.IsHeldByOs(key, input.TimestampMs))
            {
                return IgnoredBy.Key(key);
            }
        }

        var buttons = holding.Buttons & _shadow.PassedButtons & ~pressing;
        return buttons == HeldButtons.None
            ? IgnoredBy.None
            : IgnoredBy.Button((MouseButton)(BitOperations.TrailingZeroCount((uint)buttons) - 1));
    }
}
