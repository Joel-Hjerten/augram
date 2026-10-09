using Augram.Core.Abstractions;
using Augram.Core.Steps.Hotkey;

namespace Augram.Core.Steps.Scroll;

/// <summary>
/// Turns the mouse wheel at the gesture start: <paramref name="Notches"/> notches (1..20) in <paramref name="Direction"/>,
/// up and down or, for side-scrolling, left and right, while holding <paramref name="Keys"/> (Ctrl + scroll zooms in most
/// apps; SP.net's Explorer zoom posted exactly that). Bits outside the four modifiers mean nothing; a notch count outside
/// the range runs clamped. The held keys are platform-bound like a hotkey's (F8), so Ctrl becomes Cmd on a Mac.
/// </summary>
public sealed record ScrollStep(ScrollDirection Direction, int Notches = 1, KeyModifiers Keys = KeyModifiers.None) : IStep
{
    public IStepType Type => ScrollStepType.Instance;

    /// <summary>The keys held while scrolling, cut down to the four modifiers.</summary>
    public KeyModifiers HeldKeys => Keys & HotkeyKeys.AllModifiers;

    /// <summary>The notches sent: <see cref="Notches"/> within <see cref="ScrollStepType.MinNotches"/>..<see cref="ScrollStepType.MaxNotches"/>.</summary>
    public int NotchCount => Math.Clamp(Notches, ScrollStepType.MinNotches, ScrollStepType.MaxNotches);

    /// <summary>"Scroll up", "Ctrl + scroll down ×3", "Scroll right".</summary>
    public string Summary => SummaryOn(HotkeyText.Names);

    /// <summary>The summary with the held keys in <paramref name="platform"/>'s names: "Cmd + scroll down" on a Mac.</summary>
    public string SummaryOn(HostPlatform platform)
    {
        var scroll = $"scroll {Direction.ToString().ToLowerInvariant()}" + (NotchCount > 1 ? $" ×{NotchCount}" : string.Empty);
        return HeldKeys == KeyModifiers.None
            ? char.ToUpperInvariant(scroll[0]) + scroll[1..]
            : $"{HotkeyText.Format(HeldKeys, KeyCode.None, names: platform)} + {scroll}";
    }
}
