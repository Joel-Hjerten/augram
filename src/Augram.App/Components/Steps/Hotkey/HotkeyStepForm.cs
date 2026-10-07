using Augram.App.Components.HotkeyCapture;
using Augram.App.Declarations;
using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.Hotkey;
using Avalonia.Controls;

namespace Augram.App.Components.Steps.Hotkey;

/// <summary>
/// The Hotkey step's form (F5): the capture field first (press the combination, Accept or click anywhere
/// else), then the same value edited by hand as a fallback: four modifier toggles, each with an "R" toggle
/// for its right-hand key (<see cref="ModifierToggle"/>), and a key dropdown over every <see cref="KeyCode"/>
/// labelled by <see cref="HotkeyText"/>. Every edit, from either side, emits one new
/// <see cref="HotkeyStep"/>, normalised (right-hand bits only for modifiers that are on), and refreshes the
/// other side; an edit that changes nothing emits nothing.
/// </summary>
public sealed class HotkeyStepForm : IStepForm
{
    public const string CaptureHelp = "Capture, press the combination, then Accept or click anywhere else. Clear removes it.";

    public const string ModifiersHelp = "Windows modifiers; R sends the right-hand key (RAlt, RCtrl), which some apps treat differently. macOS conversion comes later (F8).";

    private static readonly IReadOnlyList<Choice<KeyCode>> Keys =
        [.. Enum.GetValues<KeyCode>().Select(key => new Choice<KeyCode>(key == KeyCode.None ? HotkeyCaptureBox.EmptyText : HotkeyText.KeyName(key), key))];

    public string TypeKey => HotkeyStepType.Instance.Key;

    public Control Build(IStep current, Action<IStep> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var state = StepParameters.Expect<HotkeyStep>(current, HotkeyStepType.Instance).Normalized();
        var box = new HotkeyCaptureBox { Modifiers = state.Modifiers, RightHand = state.RightHand, Key = state.Key };
        var refreshEditors = new List<Action>();

        void Emit(HotkeyStep next)
        {
            next = next.Normalized();
            if (next == state)
            {
                return;
            }

            state = next;
            box.Modifiers = next.Modifiers;
            box.RightHand = next.RightHand;
            box.Key = next.Key;
            changed(next);
            foreach (var refresh in refreshEditors)
            {
                refresh();
            }
        }

        // Built now, so its refresh is registered before any edit; the field's Build hands out this one instance.
        Func<Control> Modifier(KeyModifiers flag)
        {
            var toggle = new ModifierToggle();
            void Refresh()
            {
                toggle.IsOn = (state.Modifiers & flag) != 0;
                toggle.IsRightHand = (state.RightHand & flag) != 0;
            }

            Refresh();
            refreshEditors.Add(Refresh);
            toggle.Toggled += (_, _) => Emit(state with
            {
                Modifiers = toggle.IsOn ? state.Modifiers | flag : state.Modifiers & ~flag,
                RightHand = toggle.IsRightHand ? state.RightHand | flag : state.RightHand & ~flag,
            });
            return () => toggle;
        }

        var key = new DelegateBinding<KeyCode>(() => state.Key, value => Emit(state with { Key = value }), propertyName: nameof(HotkeyStep.Key));
        refreshEditors.Add(key.NotifyChanged);
        box.Committed += (_, e) => Emit(new HotkeyStep(e.Modifiers, e.Key, e.RightHand));

        var screen = new FormScreen("Hotkey",
        [
            new Section("Hotkey",
            [
                new CustomField("Keys", () => box, Help: CaptureHelp),
            ]),
            new Section("Edit by hand",
            [
                new CustomField(HotkeyText.Format(KeyModifiers.Control, KeyCode.None), Modifier(KeyModifiers.Control)),
                new CustomField(HotkeyText.Format(KeyModifiers.Alt, KeyCode.None), Modifier(KeyModifiers.Alt)),
                new CustomField(HotkeyText.Format(KeyModifiers.Shift, KeyCode.None), Modifier(KeyModifiers.Shift)),
                new CustomField(HotkeyText.Format(KeyModifiers.Meta, KeyCode.None), Modifier(KeyModifiers.Meta)),
                new DropdownField<KeyCode>("Key", Keys, key, "Also for a key alone, a lone modifier (Left Win) or a key the capture cannot see."),
            ], ModifiersHelp),
        ]);
        return new SectionForm.SectionForm { Screen = screen };
    }
}
