using Augram.App.Declarations;
using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.Scroll;
using Avalonia.Controls;

namespace Augram.App.Components.Steps.Scroll;

/// <summary>
/// The Scroll step's form: the direction as a dropdown (left and right scroll sideways), the notches within the type's
/// range, and the keys held while the wheel turns as one row of four check boxes named as this platform's keyboard names
/// them (Ctrl, Alt, Shift, Win on Windows; Ctrl, Opt, Shift, Cmd on macOS, through <see cref="HotkeyText"/>). Every edit
/// emits one new <see cref="ScrollStep"/>; an edit that changes nothing emits nothing.
/// </summary>
public sealed class ScrollStepForm : IStepForm
{
    public const string WhereHelp = "Scrolls at the gesture start, in the window there, which is brought to the front first (A20).";

    private static readonly IReadOnlyList<Choice<ScrollDirection>> Directions =
        [.. Enum.GetValues<ScrollDirection>().Select(direction => new Choice<ScrollDirection>(Label(direction), direction))];

    public string TypeKey => ScrollStepType.Instance.Key;

    public Control Build(IStep current, Action<IStep> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var state = StepParameters.Expect<ScrollStep>(current, ScrollStepType.Instance);

        void Emit(ScrollStep next)
        {
            if (next == state)
            {
                return;
            }

            state = next;
            changed(next);
        }

        ToggleOption Key(KeyModifiers flag) => new(
            HotkeyText.Format(flag, KeyCode.None),
            new DelegateBinding<bool>(() => (state.HeldKeys & flag) != 0, on => Emit(state with { Keys = on ? state.HeldKeys | flag : state.HeldKeys & ~flag })));

        var screen = new FormScreen("Scroll",
        [
            new Section("Scroll",
            [
                new DropdownField<ScrollDirection>(
                    "Direction",
                    Directions,
                    new DelegateBinding<ScrollDirection>(() => state.Direction, direction => Emit(state with { Direction = direction })),
                    "Left and right scroll sideways, as a tilt wheel does."),
                new NumberField(
                    "Notches",
                    new DelegateBinding<double>(() => state.NotchCount, notches => Emit(state with { Notches = (int)notches })),
                    ScrollStepType.MinNotches,
                    ScrollStepType.MaxNotches,
                    Help: "Wheel clicks, one after another."),
                new TogglesField(
                    "Keys held",
                    [Key(KeyModifiers.Control), Key(KeyModifiers.Alt), Key(KeyModifiers.Shift), Key(KeyModifiers.Meta)],
                    "Held down while the wheel turns, then released: Ctrl + scroll zooms in most apps."),
            ], WhereHelp),
        ]);
        return new SectionForm.SectionForm { Screen = screen };
    }

    private static string Label(ScrollDirection direction) => direction switch
    {
        ScrollDirection.Left => "Left (sideways)",
        ScrollDirection.Right => "Right (sideways)",
        _ => direction.ToString(),
    };
}
