using Augram.App.Components.HotkeyCapture;
using Augram.App.Declarations;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Mapping;
using Augram.Core.Steps;
using Augram.Core.Steps.Hotkey;
using Augram.Core.Steps.Remap;
using Avalonia.Controls;

namespace Augram.App.Components.Steps.Remap;

/// <summary>
/// The Remap step's form (F9, plan 0002 step 4; offered only under a hold remap): the output kind (Button / Key / Wheel), then
/// what the kind needs (a button dropdown; the capture field and a key dropdown, as the Hotkey form has them; a wheel
/// direction), the modifiers (Ctrl, Alt, Shift, Win, named as this platform names them: four "R"-able toggles for a key, as the
/// Hotkey form's, one row of check boxes for a button or a wheel, which have no right-hand keys) and a short note on what
/// they mean. Changing the kind keeps the modifiers and brings back the last button, key or direction chosen in this form.
/// On a button trigger (plan 0005, the <see cref="StepFormContext"/>) the output is a key, held while both buttons are down:
/// Key is the only kind offered (a stored Button or Wheel output stays listed, so it can be changed to a key), with that
/// trigger's help and note. Every edit emits one new <see cref="RemapStep"/>; the rules (a wheel output only for a wheel input,
/// a key output on a button trigger, …) are Core's and answer through the host's message line, which then shows the stored
/// step again.
/// </summary>
public sealed class RemapStepForm : IStepForm
{
    public const string Note = "Held while the input is held; Shift/Ctrl only around a button's press.";
    public const string OutputHelp = "Button and Key are held for as long as the input is held. Wheel sends a notch per notch of the input: for a wheel input only.";
    public const string ButtonModifiersHelp = "For a button they are pressed around its press only (Blender reads them when the drag starts); for a wheel, around each notch.";

    /// <summary>The note on a button trigger's Remap step (plan 0005 decision 3).</summary>
    public const string ButtonTriggerNote = "Pressed when the trigger fires and held while both buttons are down; released when either is released.";

    /// <summary>The Output ⓘ on a button trigger (plan 0005 decision 8).</summary>
    public const string ButtonTriggerOutputHelp = "A button trigger holds a key, with its modifiers, for as long as both buttons are down. Button and Wheel outputs are for a hold remap's inputs.";

    private static readonly IReadOnlyList<Choice<RemapOutputKind>> Kinds = Choice.FromEnum<RemapOutputKind>();

    private static readonly IReadOnlyList<Choice<MouseButton>> Buttons =
        [.. HeldButtonsExtensions.DisplayOrder.Select(button => new Choice<MouseButton>(button.ToString(), button))];

    private static readonly IReadOnlyList<Choice<ScrollDirection>> Directions =
    [
        new("Up", ScrollDirection.Up),
        new("Down", ScrollDirection.Down),
        new("Left (sideways)", ScrollDirection.Left),
        new("Right (sideways)", ScrollDirection.Right),
    ];

    // Built per form, not once per type: the key names follow HotkeyText.Names, which the App sets at startup.
    private static IReadOnlyList<Choice<KeyCode>> Keys =>
        [.. Enum.GetValues<KeyCode>().Select(key => new Choice<KeyCode>(key == KeyCode.None ? HotkeyCaptureBox.EmptyText : HotkeyText.KeyName(key), key))];

    public string TypeKey => RemapStepType.Instance.Key;

    public Control Build(IStep current, Action<IStep> changed) => Build(current, changed, StepFormContext.None);

    public Control Build(IStep current, Action<IStep> changed, StepFormContext context)
    {
        ArgumentNullException.ThrowIfNull(changed);
        ArgumentNullException.ThrowIfNull(context);
        var form = new State(StepParameters.Expect<RemapStep>(current, RemapStepType.Instance).Output, output => changed(new RemapStep(output)));
        var onButtonTrigger = context.Trigger is Trigger.ButtonTrigger;
        IReadOnlyList<Choice<RemapOutputKind>> kinds = onButtonTrigger ? [.. Kinds.Where(kind => kind.Value == RemapOutputKind.Key || kind.Value == form.Output.Kind)] : Kinds;
        var screen = new FormScreen("Remap",
        [
            new Section("Remap",
            [
                new DropdownField<RemapOutputKind>("Output", kinds, form.Binding(() => form.Output.Kind, form.SetKind), onButtonTrigger ? ButtonTriggerOutputHelp : OutputHelp),
                new DropdownField<MouseButton>("Button", Buttons, form.Binding(() => form.ShownButton, form.SetButton)) { Visible = form.Shows(RemapOutputKind.Button) },
                new CustomField("Keys", () => form.KeyBox, Help: "Capture, press the key or the combination, then Accept or click anywhere else.") { Visible = form.Shows(RemapOutputKind.Key) },
                new DropdownField<KeyCode>("Key", Keys, form.Binding(() => form.ShownKey, form.SetKey), "Also for a key the capture cannot see.") { Visible = form.Shows(RemapOutputKind.Key) },
                new DropdownField<ScrollDirection>("Direction", Directions, form.Binding(() => form.ShownDirection, form.SetDirection)) { Visible = form.Shows(RemapOutputKind.Wheel) },
                .. ModifierToggles(form),
                new TogglesField("Keys held", [.. HotkeyKeys.LeftKeys(HotkeyKeys.AllModifiers).Select(key => form.Plain(HotkeyKeys.ModifierOf(key)))], ButtonModifiersHelp)
                {
                    Visible = form.Hides(RemapOutputKind.Key),
                },
                new NoteField("Note", onButtonTrigger ? ButtonTriggerNote : Note),
            ]),
        ]);
        return new SectionForm.SectionForm { Screen = screen };
    }

    /// <summary>A key output's four modifiers, each a <see cref="ModifierToggle"/> with its "R" (right-hand) toggle, as the Hotkey form's.</summary>
    private static IEnumerable<Field> ModifierToggles(State form)
        => HotkeyKeys.LeftKeys(HotkeyKeys.AllModifiers).Select(key => HotkeyKeys.ModifierOf(key)).Select(flag =>
            (Field)new CustomField(HotkeyText.Format(flag, KeyCode.None), form.Toggle(flag)) { Visible = form.Shows(RemapOutputKind.Key) });

    /// <summary>The form's one output and the editors that follow it; every change goes through <see cref="Emit"/>.</summary>
    private sealed class State
    {
        private readonly Action<RemapOutput> _changed;
        private readonly List<Action> _refresh = [];
        private MouseButton _button = MouseButton.Middle;
        private KeyCode _key = KeyCode.None;
        private KeyModifiers _rightHand = KeyModifiers.None;
        private ScrollDirection _direction = ScrollDirection.Up;

        public State(RemapOutput output, Action<RemapOutput> changed)
        {
            Output = output;
            _changed = changed;
            Remember(output);
            KeyBox = new HotkeyCaptureBox();
            KeyBox.Committed += (_, e) => Emit(new RemapOutput.Key(e.Key, e.Modifiers, e.RightHand & e.Modifiers));
            _refresh.Add(ShowKeyBox);
            ShowKeyBox();
        }

        public RemapOutput Output { get; private set; }

        /// <summary>The capture field of a key output: the key and its modifiers (and their sides) in one press.</summary>
        public HotkeyCaptureBox KeyBox { get; }

        public DelegateBinding<T> Binding<T>(Func<T> get, Action<T> set)
        {
            var binding = new DelegateBinding<T>(get, set, propertyName: null);
            _refresh.Add(binding.NotifyChanged);
            return binding;
        }

        public IValueBinding<bool> Shows(RemapOutputKind kind) => Binding(() => Output.Kind == kind, _ => { });

        public IValueBinding<bool> Hides(RemapOutputKind kind) => Binding(() => Output.Kind != kind, _ => { });

        public void SetKind(RemapOutputKind kind) => Emit(kind switch
        {
            RemapOutputKind.Button => new RemapOutput.Button(_button, Output.Modifiers),
            RemapOutputKind.Key => new RemapOutput.Key(_key, Output.Modifiers, _rightHand & Output.Modifiers),
            _ => new RemapOutput.Wheel(_direction, Output.Modifiers),
        });

        // A kind's own editor acts only while the output is of that kind: a hidden dropdown that its renderer refreshes after
        // an edit echoes its value back, and must not turn a wheel output back into a button.
        public void SetButton(MouseButton button)
        {
            if (Output is RemapOutput.Button output)
            {
                Emit(output with { MouseButton = button });
            }
        }

        public void SetKey(KeyCode key)
        {
            if (Output is RemapOutput.Key output)
            {
                Emit(output with { KeyCode = key });
            }
        }

        public void SetDirection(ScrollDirection direction)
        {
            if (Output is RemapOutput.Wheel output)
            {
                Emit(output with { Direction = direction });
            }
        }

        /// <summary>The button the Button dropdown shows: the output's, else the last one chosen here.</summary>
        public MouseButton ShownButton => Output is RemapOutput.Button button ? button.MouseButton : _button;

        public KeyCode ShownKey => Output is RemapOutput.Key key ? key.KeyCode : _key;

        public ScrollDirection ShownDirection => Output is RemapOutput.Wheel wheel ? wheel.Direction : _direction;

        /// <summary>One check box of a button's or a wheel's modifiers.</summary>
        public ToggleOption Plain(KeyModifiers flag) => new(
            HotkeyText.Format(flag, KeyCode.None),
            Binding(() => (Output.Modifiers & flag) != 0, on => Emit(Output with { Modifiers = on ? Output.Modifiers | flag : Output.Modifiers & ~flag })));

        /// <summary>A key output's modifier with its right-hand toggle; built now, so its refresh is registered before any edit.</summary>
        public Func<Control> Toggle(KeyModifiers flag)
        {
            var toggle = new ModifierToggle();
            void Show()
            {
                toggle.IsOn = (Output.Modifiers & flag) != 0;
                toggle.IsRightHand = Output is RemapOutput.Key { RightHand: var right } && (right & flag) != 0;
            }

            Show();
            _refresh.Add(Show);
            toggle.Toggled += (_, _) =>
            {
                if (Output is RemapOutput.Key key)
                {
                    var modifiers = toggle.IsOn ? key.Modifiers | flag : key.Modifiers & ~flag;
                    var right = toggle.IsRightHand ? key.RightHand | flag : key.RightHand & ~flag;
                    Emit(key with { Modifiers = modifiers, RightHand = right & modifiers });
                }
            };
            return () => toggle;
        }

        private void Emit(RemapOutput next)
        {
            if (next == Output)
            {
                return;
            }

            Output = next;
            Remember(next);
            _changed(next);
            foreach (var refresh in _refresh)
            {
                refresh();
            }
        }

        private void Remember(RemapOutput output)
        {
            switch (output)
            {
                case RemapOutput.Button button:
                    _button = button.MouseButton;
                    break;
                case RemapOutput.Key key:
                    _key = key.KeyCode;
                    _rightHand = key.RightHand;
                    break;
                case RemapOutput.Wheel wheel:
                    _direction = wheel.Direction;
                    break;
            }
        }

        private void ShowKeyBox()
        {
            var key = Output as RemapOutput.Key;
            KeyBox.Modifiers = key?.Modifiers ?? KeyModifiers.None;
            KeyBox.RightHand = key?.RightHand ?? KeyModifiers.None;
            KeyBox.Key = key?.KeyCode ?? KeyCode.None;
        }
    }
}
