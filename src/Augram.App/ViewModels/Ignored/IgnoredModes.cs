using Augram.App.Declarations;

namespace Augram.App.ViewModels.Ignored;

/// <summary>
/// The two modes of an Exclusions › Global entry as the Exclusions tab words them (F5; SP.net's Ignore List and its "Disable S+ if
/// this App Gains Focus"), over <see cref="Core.Mapping.IgnoredApp.DisableEntirely"/>: the form's choices, its help line, the row
/// summary, and what each mode means for hold remaps (plan 0005 decision 7: they keep working in the plain mode, since they never
/// use the stroke button; the disable-while-focused mode stops them too).
/// </summary>
public static class IgnoredModes
{
    public const string GesturesOff = "Gestures off over this app";

    public const string DisableWhileFocused = "Disable while focused";

    public const string Help =
        "Gestures off: over the app's windows the stroke button reaches the app untouched (its drag and the wheel too), as with the ignore key. "
        + "Disable while focused: the same, and while the app has focus Augram behaves as if switched off, so a virtual machine gets the raw button.";

    /// <summary>A plain entry's form (Joel, 2026-10-10).</summary>
    public const string HoldRemapsStillWork = "Hold remaps still work in this app: they never use the stroke button.";

    /// <summary>An entry's form in the disable-while-focused mode.</summary>
    public const string OffWhileFocused = "Augram is off while this app has focus, hold remaps included.";

    /// <summary>The Global list's ⓘ, for all its entries.</summary>
    public const string HoldRemapsInTheseApps =
        "Hold remaps still work in these apps: they never use the stroke button (a \"Disable while focused\" app stops them too while it has focus).";

    /// <summary>The label of the form's row that says it.</summary>
    public const string HoldRemapsLabel = "Hold remaps";

    public static IReadOnlyList<Choice<bool>> Choices { get; } =
    [
        new(GesturesOff, false),
        new(DisableWhileFocused, true),
    ];

    public static string Label(bool disableWhileFocused) => disableWhileFocused ? DisableWhileFocused : GesturesOff;

    /// <summary>What the mode means for hold remaps: <see cref="HoldRemapsStillWork"/> or <see cref="OffWhileFocused"/>.</summary>
    public static string HoldRemaps(bool disableWhileFocused) => disableWhileFocused ? OffWhileFocused : HoldRemapsStillWork;
}
