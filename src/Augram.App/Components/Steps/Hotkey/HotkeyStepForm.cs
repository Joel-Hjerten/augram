using Augram.App.Components.Capture;
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

    /// <summary>R names the right-hand keys under this platform's names (RAlt, RCtrl on Windows; ROpt, RCmd on macOS).</summary>
    public static string ModifiersHelp =>
        $"R sends the right-hand key ({HotkeyText.Format(KeyModifiers.Alt, KeyCode.None, KeyModifiers.Alt)}, {HotkeyText.Format(KeyModifiers.Control, KeyCode.None, KeyModifiers.Control)}), which some apps treat differently.";

    public string TypeKey => HotkeyStepType.Instance.Key;

    public Control Build(IStep current, Action<IStep> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var opened = StepParameters.Expect<HotkeyStep>(current, HotkeyStepType.Instance).Normalized();
        var box = new HotkeyCaptureBox { Modifiers = opened.Modifiers, RightHand = opened.RightHand, Key = opened.Key };
        var form = new StepFormState<HotkeyStep>(opened, changed, step => step.Normalized())
        {
            Accepted = next =>
            {
                box.Modifiers = next.Modifiers;
                box.RightHand = next.RightHand;
                box.Key = next.Key;
            },
        };

        Func<Control> Modifier(KeyModifiers flag) => HotkeyChoices.ModifierToggle(
            flag,
            () => (form.Current.Modifiers, form.Current.RightHand),
            (modifiers, rightHand) => form.Emit(form.Current with { Modifiers = modifiers, RightHand = rightHand }),
            form.OnEdit);

        var key = form.Refreshed(() => form.Current.Key, value => form.Emit(form.Current with { Key = value }), propertyName: nameof(HotkeyStep.Key));
        box.Committed += (_, e) => form.Emit(new HotkeyStep(e.Modifiers, e.Key, e.RightHand));

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
                new DropdownField<KeyCode>("Key", HotkeyChoices.Keys, key, $"Also for a key alone, a lone modifier ({HotkeyText.KeyName(KeyCode.LeftMeta)}) or a key the capture cannot see."),
            ], ModifiersHelp),
        ]);
        return new SectionForm.SectionForm { Screen = screen };
    }
}
