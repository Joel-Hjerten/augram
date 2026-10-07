using Augram.Core.Abstractions;

namespace Augram.Core.Steps.Hotkey;

/// <summary>
/// A hotkey's best guess on the other platform (F8, Joel 2026-10-07). First the exception table, whole combinations
/// whose counterpart is not the plain modifier swap; then the swap itself: Windows Ctrl becomes Cmd and Mac Cmd becomes
/// Ctrl, Alt is Option on both, Shift and the key stay, and a right-hand modifier keeps its side. The Windows key has
/// no Mac counterpart, and a Mac combination holding both Cmd and Ctrl has no Windows one: those report "needs a …
/// version". A combination the rules leave as it was (a bare key, Shift+F5, Ctrl+Tab) is unchanged.
/// </summary>
internal static class HotkeyConversion
{
    private const KeyModifiers Ctrl = KeyModifiers.Control;
    private const KeyModifiers Alt = KeyModifiers.Alt;
    private const KeyModifiers Shift = KeyModifiers.Shift;
    private const KeyModifiers Meta = KeyModifiers.Meta;

    // Whole combinations whose counterpart is not the modifier swap. Grows as real use shows more.
    private static readonly Dictionary<(KeyModifiers, KeyCode), (KeyModifiers, KeyCode)> WindowsToMac = new()
    {
        [(Alt, KeyCode.Tab)] = (Meta, KeyCode.Tab),
        [(Alt | Shift, KeyCode.Tab)] = (Meta | Shift, KeyCode.Tab),
        [(Ctrl, KeyCode.Tab)] = (Ctrl, KeyCode.Tab),
        [(Ctrl | Shift, KeyCode.Tab)] = (Ctrl | Shift, KeyCode.Tab),
        [(Alt, KeyCode.F4)] = (Meta, KeyCode.W),
        [(Ctrl, KeyCode.Y)] = (Meta | Shift, KeyCode.Z),
    };

    private static readonly Dictionary<(KeyModifiers, KeyCode), (KeyModifiers, KeyCode)> MacToWindows = new()
    {
        [(Meta, KeyCode.Tab)] = (Alt, KeyCode.Tab),
        [(Meta | Shift, KeyCode.Tab)] = (Alt | Shift, KeyCode.Tab),
        [(Ctrl, KeyCode.Tab)] = (Ctrl, KeyCode.Tab),
        [(Ctrl | Shift, KeyCode.Tab)] = (Ctrl | Shift, KeyCode.Tab),
        [(Meta, KeyCode.Q)] = (Alt, KeyCode.F4),
        [(Meta | Shift, KeyCode.Z)] = (Ctrl, KeyCode.Y),
    };

    public static StepConversion Convert(HotkeyStep step, HostPlatform from, HostPlatform to)
    {
        var source = step.Normalized();
        if (from == to || !source.IsSet)
        {
            return StepConversion.Same(step);
        }

        var exceptions = from == HostPlatform.Windows ? WindowsToMac : MacToWindows;
        if (exceptions.TryGetValue((source.Modifiers, source.Key), out var mapped))
        {
            return Result(step, new HotkeyStep(mapped.Item1, mapped.Item2));
        }

        if (from == HostPlatform.Windows)
        {
            return (source.Modifiers & Meta) != 0
                ? StepConversion.None($"{HotkeyText.Format(source.Modifiers, source.Key, source.RightHand, HostPlatform.Windows)} needs a macOS version: the Windows key has no Mac counterpart")
                : Result(step, source with { Modifiers = Swap(source.Modifiers, Ctrl, Meta), RightHand = Swap(source.RightHand, Ctrl, Meta) });
        }

        return (source.Modifiers & (Meta | Ctrl)) == (Meta | Ctrl)
            ? StepConversion.None($"{HotkeyText.Format(source.Modifiers, source.Key, source.RightHand, HostPlatform.MacOS)} needs a Windows version: Cmd and Ctrl together have no Windows counterpart")
            : Result(step, source with { Modifiers = Swap(source.Modifiers, Meta, Ctrl), RightHand = Swap(source.RightHand, Meta, Ctrl) });
    }

    private static StepConversion Result(HotkeyStep original, HotkeyStep converted)
        => converted == original.Normalized() ? StepConversion.Same(original) : StepConversion.To(converted);

    /// <summary><paramref name="modifiers"/> with <paramref name="from"/> replaced by <paramref name="to"/>.</summary>
    private static KeyModifiers Swap(KeyModifiers modifiers, KeyModifiers from, KeyModifiers to)
        => (modifiers & from) == 0 ? modifiers : (modifiers & ~from) | to;
}
