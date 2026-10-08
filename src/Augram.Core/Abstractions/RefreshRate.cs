using System.Globalization;

namespace Augram.Core.Abstractions;

/// <summary>
/// A display refresh rate in millihertz: hertz rounded to three decimals, precise enough to keep 119.88 Hz apart from
/// 120 Hz and 23.976 Hz apart from 24 Hz. <see cref="Unknown"/> (zero) is a mode whose display reports no rate (some
/// macOS built-in panels). Windows reports whole hertz; <see cref="FromLegacyHertz"/> turns that into the exact rate
/// (learnings 0002 §2). Shown as "119.88 Hz", stored by the Display mode step as the number 119.88.
/// </summary>
public readonly record struct RefreshRate(int Millihertz)
{
    /// <summary>The highest rate a step may store, in hertz.</summary>
    public const int MaxHertz = 1000;

    // Rates that television timings also run at 1000/1001 of; Windows reports those rounded down (59.94 → 59).
    private static readonly int[] TelevisionRates = [24, 30, 48, 60, 120, 240];

    public static RefreshRate Unknown => default;

    public bool IsKnown => Millihertz > 0;

    /// <summary>The rate in hertz (119.880, 120.000, 23.976).</summary>
    public decimal Hertz => Millihertz / 1000m;

    /// <summary>"119.88", "120", "23.976"; "0" when unknown.</summary>
    public string Text => (Millihertz / 1000m).ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary><paramref name="hertz"/> rounded to three decimals; <see cref="Unknown"/> for zero, a negative or a non-finite value.</summary>
    public static RefreshRate FromHertz(double hertz)
        => double.IsFinite(hertz) && hertz > 0 && hertz <= MaxHertz ? new((int)Math.Round(hertz * 1000, MidpointRounding.AwayFromZero)) : Unknown;

    /// <summary>
    /// Windows' whole-hertz rate (<c>DEVMODE.dmDisplayFrequency</c>, Display Changer's <c>-refresh=</c>) as the rate the
    /// display runs at: one below a television rate is that rate's 1000/1001 variant (23 → 23.976, 59 → 59.94,
    /// 119 → 119.88), anything else is itself; 0 and 1 mean "the hardware default" and give <see cref="Unknown"/>.
    /// Verified one to one against DXGI's exact list on Joel's PC (learnings 0002 §2).
    /// </summary>
    public static RefreshRate FromLegacyHertz(int hertz)
    {
        if (hertz <= 1 || hertz > MaxHertz)
        {
            return Unknown;
        }

        return Array.IndexOf(TelevisionRates, hertz + 1) >= 0
            ? new((int)Math.Round((hertz + 1) * 1_000_000 / 1001.0, MidpointRounding.AwayFromZero))
            : new(hertz * 1000);
    }

    /// <summary>
    /// Whether the two rates are one timing in two spellings: within 0.2 % of each other, which pairs every rate with its
    /// 1000/1001 variant (120 and 119.88, 60 and 59.94, 24 and 23.976) and nothing further apart. Unknown is near nothing.
    /// </summary>
    public bool IsNear(RefreshRate other)
        => IsKnown && other.IsKnown && Math.Abs(Millihertz - other.Millihertz) * 500L <= Math.Max(Millihertz, other.Millihertz);

    /// <summary>"119.88 Hz"; "rate not reported" when unknown.</summary>
    public override string ToString() => IsKnown ? $"{Text} Hz" : "rate not reported";
}
