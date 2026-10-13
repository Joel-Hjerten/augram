using System.Collections.Frozen;
using Augram.App.Components.Capture;
using Augram.App.Declarations;
using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.Hotkey;
using Avalonia.Controls;

namespace Augram.App.Components.Steps.Hotkey;

/// <summary>
/// The Hotkey step's form (F5): the capture field first (press the combination, Accept or click anywhere
/// else), under it the "Per keyboard layout" switch (plan 0011 decision 3) and, while it is on, one row per keyboard
/// layout the OS has installed (<see cref="ComponentPorts.KeyboardLayouts"/>, or the constructor's port for tests and
/// the gallery), each a capture field for that layout's own shortcut, empty until set; a layout the step holds that is no
/// longer installed shows by its id, with Remove on the line under it. Then the main shortcut edited by hand as a fallback: four modifier
/// toggles, each with an "R" toggle for its right-hand key (<see cref="ModifierToggle"/>), and a key dropdown over every
/// <see cref="KeyCode"/> labelled by <see cref="HotkeyText"/>. Every edit, from any field, emits one new
/// <see cref="HotkeyStep"/> through one <see cref="StepFormState{TStep}"/>, normalised (right-hand bits only for modifiers
/// that are on; a layout row without a key is no shortcut), and refreshes the other fields; an edit that changes nothing
/// emits nothing. The switch off drops every layout shortcut in one edit. The form re-declares itself when its shape
/// changes (the switch, Remove) and when the layouts port is re-published (the composition root does so on a layout
/// change, a resource change here), so the key names and the rows follow the active layout (decision 6).
/// </summary>
public sealed class HotkeyStepForm : IStepForm
{
    public const string CaptureHelp = "Capture, press the combination, then Accept or click anywhere else. Clear removes it.";

    public const string PerLayoutLabel = "Per keyboard layout";

    public const string PerLayoutHelp = "A shortcut of its own under each keyboard layout the OS has installed; a layout without one presses the shortcut above. Chrome's Back is Cmd+[ under ABC and Cmd+Opt+8 under Swedish. Off drops them all.";

    public const string NotInstalledHint = "Not installed now; the shortcut stays until removed.";

    public const string RemoveLabel = "Remove";

    private readonly IKeyboardLayouts? _layouts;

    public HotkeyStepForm()
    {
    }

    /// <summary>A form over explicit layouts (tests, the gallery) instead of the published port.</summary>
    public HotkeyStepForm(IKeyboardLayouts layouts)
    {
        ArgumentNullException.ThrowIfNull(layouts);
        _layouts = layouts;
    }

    /// <summary>R names the right-hand keys under this platform's names (RAlt, RCtrl on Windows; ROpt, RCmd on macOS).</summary>
    public static string ModifiersHelp =>
        $"R sends the right-hand key ({HotkeyText.Format(KeyModifiers.Alt, KeyCode.None, KeyModifiers.Alt)}, {HotkeyText.Format(KeyModifiers.Control, KeyCode.None, KeyModifiers.Control)}), which some apps treat differently.";

    public string TypeKey => HotkeyStepType.Instance.Key;

    public Control Build(IStep current, Action<IStep> changed)
    {
        ArgumentNullException.ThrowIfNull(changed);
        var host = new SectionForm.SectionForm();
        var state = StepParameters.Expect<HotkeyStep>(current, HotkeyStepType.Instance).Normalized();
        var perLayout = state.HasLayouts;
        var shown = Fingerprint(Layouts(host));

        void Show()
        {
            var layouts = Layouts(host);
            var box = new HotkeyCaptureBox { Modifiers = state.Modifiers, RightHand = state.RightHand, Key = state.Key };
            var form = new StepFormState<HotkeyStep>(state, next => { state = next; changed(next); }, step => step.Normalized())
            {
                Accepted = next =>
                {
                    box.Modifiers = next.Modifiers;
                    box.RightHand = next.RightHand;
                    box.Key = next.Key;
                },
            };
            box.Committed += (_, e) => form.Emit(form.Current with { Modifiers = e.Modifiers, Key = e.Key, RightHand = e.RightHand });

            void SetPerLayout(bool on)
            {
                if (on == perLayout)
                {
                    return;
                }

                perLayout = on;
                if (!on)
                {
                    form.Emit(form.Current with { Layouts = HotkeyStep.NoLayouts });
                }

                Show();
            }

            var hotkey = new List<Field>
            {
                new CustomField("Keys", () => box, Help: CaptureHelp),
                new ToggleField(PerLayoutLabel, form.Bind(() => perLayout, SetPerLayout, propertyName: "PerLayout"), PerLayoutHelp),
            };
            if (perLayout)
            {
                var installed = layouts.Installed;
                hotkey.AddRange(installed.SelectMany(layout => LayoutRows(form, layout.Id, layout.Name, remove: null)));
                hotkey.AddRange(form.Current.Layouts.Keys
                    .Where(id => !installed.Any(layout => layout.Id == id))
                    .Order(StringComparer.Ordinal)
                    .SelectMany(id => LayoutRows(form, id, id, remove: () =>
                    {
                        form.Emit(form.Current with { Layouts = Without(form.Current.Layouts, id) });
                        Show();
                    })));
            }

            Func<Control> Modifier(KeyModifiers flag) => HotkeyChoices.ModifierToggle(
                flag,
                () => (form.Current.Modifiers, form.Current.RightHand),
                (modifiers, rightHand) => form.Emit(form.Current with { Modifiers = modifiers, RightHand = rightHand }),
                form.OnEdit);

            var key = form.Refreshed(() => form.Current.Key, value => form.Emit(form.Current with { Key = value }), propertyName: nameof(HotkeyStep.Key));
            host.Screen = new FormScreen("Hotkey",
            [
                new Section("Hotkey", hotkey),
                new Section("Edit by hand",
                [
                    new CustomField(HotkeyText.Format(KeyModifiers.Control, KeyCode.None), Modifier(KeyModifiers.Control)),
                    new CustomField(HotkeyText.Format(KeyModifiers.Alt, KeyCode.None), Modifier(KeyModifiers.Alt)),
                    new CustomField(HotkeyText.Format(KeyModifiers.Shift, KeyCode.None), Modifier(KeyModifiers.Shift)),
                    new CustomField(HotkeyText.Format(KeyModifiers.Meta, KeyCode.None), Modifier(KeyModifiers.Meta)),
                    new DropdownField<KeyCode>("Key", HotkeyChoices.Keys, key, $"Also for a key alone, a lone modifier ({HotkeyText.KeyName(KeyCode.LeftMeta)}) or a key the capture cannot see."),
                ], ModifiersHelp),
            ]);
        }

        Show();
        host.ResourcesChanged += (_, _) =>
        {
            // The composition root re-publishes the ports when the keyboard layout changes; the names and the rows follow.
            var now = Fingerprint(Layouts(host));
            if (now != shown)
            {
                shown = now;
                Show();
            }
        };
        return host;
    }

    /// <summary>
    /// One layout's row: its capture field over the step's shortcut for it (empty when none); for a layout no longer installed
    /// (<paramref name="remove"/> given) a Remove button on the line under it, with the "Not installed now" hint.
    /// </summary>
    private static IEnumerable<Field> LayoutRows(StepFormState<HotkeyStep> form, string id, string label, Action? remove)
    {
        var binding = form.Current.Layouts.TryGetValue(id, out var own) ? own : new HotkeyBinding(KeyModifiers.None, KeyCode.None);
        var box = new HotkeyCaptureBox { Modifiers = binding.Modifiers, RightHand = binding.RightHand, Key = binding.Key };
        box.Committed += (_, e) => form.Emit(form.Current with { Layouts = With(form.Current.Layouts, id, new HotkeyBinding(e.Modifiers, e.Key, e.RightHand)) });
        yield return new CustomField(label, () => box);
        if (remove is not null)
        {
            yield return new ButtonsField(string.Empty, [new FieldButton(RemoveLabel, remove)]) { Hint = new DelegateBinding<string>(() => NotInstalledHint) };
        }
    }

    private static IReadOnlyDictionary<string, HotkeyBinding> With(IReadOnlyDictionary<string, HotkeyBinding> layouts, string id, HotkeyBinding binding)
        => layouts.Where(pair => pair.Key != id).Append(new(id, binding)).ToFrozenDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    private static IReadOnlyDictionary<string, HotkeyBinding> Without(IReadOnlyDictionary<string, HotkeyBinding> layouts, string id)
        => layouts.Where(pair => pair.Key != id).ToFrozenDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    /// <summary>What the form shows of the port: the active layout and the installed list; a different reading re-declares the form.</summary>
    private static string Fingerprint(IKeyboardLayouts layouts)
        => string.Join("\n", layouts.Installed.Select(layout => layout.Id + "=" + layout.Name).Prepend(layouts.Active?.Id ?? string.Empty));

    private IKeyboardLayouts Layouts(Control host)
        => _layouts ?? (ComponentPorts.Near(host) ?? ComponentPorts.OfApplication())?.KeyboardLayouts ?? NullKeyboardLayouts.Instance;
}
