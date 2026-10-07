using Augram.App.Components.HotkeyCapture;
using Augram.App.Declarations;
using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.Hotkey;
using Avalonia.Controls;

namespace Augram.App.Components.Steps.Hotkey;

/// <summary>
/// The Hotkey step's form (F5): the capture field first (press the combination, Accept or click anywhere
/// else), then the same value edited by hand as a fallback: four modifier toggles and a key dropdown over
/// every <see cref="KeyCode"/> labelled by <see cref="HotkeyText"/>. Every edit, from either side, emits one
/// new <see cref="HotkeyStep"/> and refreshes the other side; an edit that changes nothing emits nothing.
/// </summary>
public sealed class HotkeyStepForm : IStepForm
{
    public const string CaptureHelp = "Capture, press the combination, then Accept or click anywhere else. Clear removes it.";

    private static readonly IReadOnlyList<Choice<KeyCode>> Keys =
        [.. Enum.GetValues<KeyCode>().Select(key => new Choice<KeyCode>(key == KeyCode.None ? HotkeyCaptureBox.EmptyText : HotkeyText.KeyName(key), key))];

    public string TypeKey => HotkeyStepType.Instance.Key;

    public Control Build(IStep current, Action<IStep> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var state = StepParameters.Expect<HotkeyStep>(current, HotkeyStepType.Instance);
        var box = new HotkeyCaptureBox { Modifiers = state.Modifiers, Key = state.Key };
        var refreshEditors = new List<Action>();

        void Emit(HotkeyStep next)
        {
            if (next == state)
            {
                return;
            }

            state = next;
            box.Modifiers = next.Modifiers;
            box.Key = next.Key;
            changed(next);
            foreach (var refresh in refreshEditors)
            {
                refresh();
            }
        }

        DelegateBinding<bool> Modifier(KeyModifiers flag)
        {
            var binding = new DelegateBinding<bool>(
                () => (state.Modifiers & flag) != 0,
                on => Emit(state with { Modifiers = on ? state.Modifiers | flag : state.Modifiers & ~flag }),
                propertyName: flag.ToString());
            refreshEditors.Add(binding.NotifyChanged);
            return binding;
        }

        var key = new DelegateBinding<KeyCode>(() => state.Key, value => Emit(state with { Key = value }), propertyName: nameof(HotkeyStep.Key));
        refreshEditors.Add(key.NotifyChanged);
        box.Committed += (_, e) => Emit(new HotkeyStep(e.Modifiers, e.Key));

        var screen = new FormScreen("Hotkey",
        [
            new Section("Hotkey",
            [
                new CustomField("Keys", () => box, Help: CaptureHelp),
            ]),
            new Section("Edit by hand",
            [
                new ToggleField(HotkeyText.Format(KeyModifiers.Control, KeyCode.None), Modifier(KeyModifiers.Control)),
                new ToggleField(HotkeyText.Format(KeyModifiers.Alt, KeyCode.None), Modifier(KeyModifiers.Alt)),
                new ToggleField(HotkeyText.Format(KeyModifiers.Shift, KeyCode.None), Modifier(KeyModifiers.Shift)),
                new ToggleField(HotkeyText.Format(KeyModifiers.Meta, KeyCode.None), Modifier(KeyModifiers.Meta)),
                new DropdownField<KeyCode>("Key", Keys, key, "Also for a key alone, a lone modifier (Left Win) or a key the capture cannot see."),
            ], "Windows modifiers; macOS conversion comes later (F8)."),
        ]);
        return new SectionForm.SectionForm { Screen = screen };
    }
}
