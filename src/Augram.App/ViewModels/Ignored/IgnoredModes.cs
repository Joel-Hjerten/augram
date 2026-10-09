using Augram.App.Declarations;

namespace Augram.App.ViewModels.Ignored;

/// <summary>
/// The two modes of an ignored app as the Ignored tab words them (F5; SP.net's Ignore List and its "Disable S+ if this App
/// Gains Focus"), over <see cref="Core.Mapping.IgnoredApp.DisableEntirely"/>: the form's choices, its help line and the row summary.
/// </summary>
public static class IgnoredModes
{
    public const string GesturesOff = "Gestures off over this app";

    public const string DisableWhileFocused = "Disable while focused";

    public const string Help =
        "Gestures off: over the app's windows the stroke button reaches the app untouched (its drag and the wheel too), as with the ignore key. "
        + "Disable while focused: the same, and while the app has focus Augram behaves as if switched off, so a virtual machine gets the raw button.";

    public static IReadOnlyList<Choice<bool>> Choices { get; } =
    [
        new(GesturesOff, false),
        new(DisableWhileFocused, true),
    ];

    public static string Label(bool disableWhileFocused) => disableWhileFocused ? DisableWhileFocused : GesturesOff;
}
