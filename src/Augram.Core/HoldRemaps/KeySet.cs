using Augram.Core.Abstractions;

namespace Augram.Core.HoldRemaps;

/// <summary>
/// A set of <see cref="KeyCode"/>s as 256 bits (every key value fits below 256), so the hook can ask "is this key an input of
/// the held hold remap?" with one shift and one mask, never allocating (CLAUDE.md invariant 1). Immutable; a value outside
/// 0..255 is never in the set.
/// </summary>
public readonly record struct KeySet(ulong Bits0, ulong Bits1, ulong Bits2, ulong Bits3)
{
    public static KeySet Empty => default;

    public bool IsEmpty => (Bits0 | Bits1 | Bits2 | Bits3) == 0;

    public bool Contains(KeyCode key)
    {
        var value = (int)key;
        if (value is < 0 or > 255)
        {
            return false;
        }

        var bit = 1UL << (value & 63);
        return (value >> 6) switch
        {
            0 => (Bits0 & bit) != 0,
            1 => (Bits1 & bit) != 0,
            2 => (Bits2 & bit) != 0,
            _ => (Bits3 & bit) != 0,
        };
    }

    /// <summary>This set with <paramref name="key"/> in it; itself for a value outside 0..255.</summary>
    public KeySet With(KeyCode key)
    {
        var value = (int)key;
        if (value is < 0 or > 255)
        {
            return this;
        }

        var bit = 1UL << (value & 63);
        return (value >> 6) switch
        {
            0 => this with { Bits0 = Bits0 | bit },
            1 => this with { Bits1 = Bits1 | bit },
            2 => this with { Bits2 = Bits2 | bit },
            _ => this with { Bits3 = Bits3 | bit },
        };
    }
}
