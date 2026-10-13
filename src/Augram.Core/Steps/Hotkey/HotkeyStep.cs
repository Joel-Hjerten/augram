using System.Collections.Frozen;
using Augram.Core.Abstractions;

namespace Augram.Core.Steps.Hotkey;

/// <summary>
/// Presses a modifier set and one key, then releases them in reverse order (F5: "modifier set + one
/// key"; a chord is a sequence of these steps). <see cref="KeyCode.None"/> is the explicit "no key set
/// yet" a new step starts with; running it skips. <see cref="RightHand"/> says which of
/// <see cref="Modifiers"/> are pressed with the right-hand key (RAlt, RCtrl: some apps treat them
/// differently, F5); a bit outside <see cref="Modifiers"/> means nothing, the text and the executor
/// ignore it, and <see cref="Normalized"/> drops it. The modifiers are platform-bound (F8): a step stores
/// what was authored, and the other platform runs <see cref="HotkeyConversion"/>'s best guess.
/// <see cref="Layouts"/> (plan 0011) holds one shortcut per keyboard layout, keyed by the platform's layout id; the
/// main shortcut (<see cref="Main"/>) is the fallback for every layout without its own, and <see cref="ForLayout"/>
/// picks. Two steps are equal when their main shortcut and their layout maps hold the same entries.
/// </summary>
public sealed record HotkeyStep(KeyModifiers Modifiers, KeyCode Key, KeyModifiers RightHand = KeyModifiers.None) : IStep
{
    public const string UnsetSummary = HotkeyBinding.UnsetSummary;

    /// <summary>What a summary ends with when the step has layout shortcuts (plan 0011 decision 5).</summary>
    public const string PerLayoutSuffix = " · per layout";

    /// <summary>The empty layout map, the default; ids compare ordinally, as the platforms' ids are case-sensitive. Declared before <see cref="Unset"/>, which is built with it.</summary>
    public static IReadOnlyDictionary<string, HotkeyBinding> NoLayouts { get; } = FrozenDictionary<string, HotkeyBinding>.Empty;

    /// <summary>No modifiers, no key: what the picker adds and what Clear returns to.</summary>
    public static HotkeyStep Unset { get; } = new(KeyModifiers.None, KeyCode.None);

    /// <summary>
    /// The layout shortcuts: layout id (<see cref="KeyboardLayout.Id"/>, ordinal) → the shortcut to press under that layout.
    /// Never null; <see cref="NoLayouts"/> when the step has none. Immutable by contract: a new map is a new step.
    /// </summary>
    public IReadOnlyDictionary<string, HotkeyBinding> Layouts { get; init; } = NoLayouts;

    public IStepType Type => HotkeyStepType.Instance;

    public bool IsSet => Key != KeyCode.None;

    /// <summary>True when the step holds at least one layout shortcut.</summary>
    public bool HasLayouts => Layouts.Count > 0;

    /// <summary>The main shortcut, the fallback for every layout without its own, as a binding.</summary>
    public HotkeyBinding Main => new(Modifiers, Key, RightHand);

    /// <summary>
    /// "Ctrl+Shift+T", "Alt+F4", "RCtrl+RShift+P", "Esc"; <see cref="UnsetSummary"/> while no key is set. With layout shortcuts
    /// (<see cref="HasLayouts"/>): the active layout's shortcut (<see cref="ForLayout"/> of <see cref="HotkeyText.Layouts"/>'
    /// <c>Active</c>, as the executor picks; the main one for a layout without a row or when the port cannot say), named by
    /// that layout, plus " · per layout" (Joel, 2026-10-13, third round: the stored main shortcut is the row of the layout active
    /// at the last edit, and under the other layout that key reads as a shortcut never captured).
    /// </summary>
    public string Summary => HasLayouts ? WithSuffix(ActiveSummary(null)) : Main.Summary;

    /// <summary>"Win+D" on Windows, "Cmd+D" on macOS: the same Meta modifier under that keyboard's name; with layout shortcuts the active layout's, as <see cref="Summary"/>.</summary>
    public string SummaryOn(HostPlatform platform) => HasLayouts ? WithSuffix(ActiveSummary(platform)) : Main.SummaryOn(platform);

    /// <summary>The shortcut the active layout runs, in its own key names (one read of the port's <c>Active</c>).</summary>
    private string ActiveSummary(HostPlatform? platform)
    {
        var active = HotkeyText.Layouts.Active?.Id;
        var shown = ForLayout(active);
        return shown.IsSet ? HotkeyText.Format(shown.Modifiers, shown.Key, shown.RightHand, platform, layoutId: active) : HotkeyBinding.UnsetSummary;
    }

    /// <summary>The shortcut to press under <paramref name="layoutId"/>: that layout's own when the step has one, else <see cref="Main"/>. Null (the platform cannot say) is the main one.</summary>
    public HotkeyBinding ForLayout(string? layoutId)
        => layoutId is not null && Layouts.TryGetValue(layoutId, out var own) ? own : Main;

    /// <summary>
    /// This step with <see cref="RightHand"/> cut down to <see cref="Modifiers"/>, every layout shortcut cut down the same way,
    /// and layout entries without a key dropped (an empty row is no shortcut: the main one runs there); itself when it already is.
    /// </summary>
    public HotkeyStep Normalized()
    {
        bool mainNormalized = (RightHand & ~Modifiers) == KeyModifiers.None;
        bool layoutsNormalized = Layouts.Count == 0 || Layouts.Values.All(binding => binding.IsSet && binding.IsNormalized);
        if (mainNormalized && layoutsNormalized)
        {
            return this;
        }

        var layouts = layoutsNormalized
            ? Layouts
            : Layouts.Where(pair => pair.Value.IsSet).ToFrozenDictionary(pair => pair.Key, pair => pair.Value.Normalized(), StringComparer.Ordinal);
        return this with { RightHand = RightHand & Modifiers, Layouts = layouts };
    }

    public bool Equals(HotkeyStep? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Modifiers != other.Modifiers || Key != other.Key || RightHand != other.RightHand || Layouts.Count != other.Layouts.Count)
        {
            return false;
        }

        foreach (var (id, binding) in Layouts)
        {
            if (!other.Layouts.TryGetValue(id, out var theirs) || binding != theirs)
            {
                return false;
            }
        }

        return true;
    }

    public override int GetHashCode()
    {
        int layouts = 0;
        foreach (var (id, binding) in Layouts)
        {
            layouts ^= HashCode.Combine(StringComparer.Ordinal.GetHashCode(id), binding);
        }

        return HashCode.Combine(Modifiers, Key, RightHand, Layouts.Count, layouts);
    }

    private string WithSuffix(string summary) => HasLayouts ? summary + PerLayoutSuffix : summary;
}
