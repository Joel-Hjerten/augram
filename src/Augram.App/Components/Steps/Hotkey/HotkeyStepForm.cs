using System.Collections.Frozen;
using Augram.App.Components.Capture;
using Augram.App.Declarations;
using Augram.Core.Abstractions;
using Augram.Core.Steps;
using Augram.Core.Steps.Hotkey;
using Avalonia.Controls;

namespace Augram.App.Components.Steps.Hotkey;

/// <summary>
/// The Hotkey step's form (F5): the capture field (press the combination, Accept or click anywhere else), under it the
/// "Per keyboard layout" switch (plan 0011 decision 3; Joel, 2026-10-13, first round: the rows replace the field). With the
/// switch off, the one field edits the main shortcut. With it on, the field goes and one row per keyboard layout the OS has
/// installed (<see cref="ComponentPorts.KeyboardLayouts"/>, or the constructor's port for tests and the gallery) stands in its
/// place, each a capture field for that layout's own shortcut named by that layout (<see cref="HotkeyCaptureBox.LayoutId"/>:
/// "Cmd+Å" on the Swedish row, "Cmd+[" on the ABC row, whatever is active); a layout the step holds that is no longer installed
/// shows after them by its id, with Remove on the line under it; a note under the rows says what the step list shows. The
/// main shortcut is then derived, never edited on its own: turning the switch on seeds every installed layout's row with the
/// main shortcut (one edit; only the layout that differs needs capturing again), and after any row edit the main shortcut is
/// the active layout's row when that is set, else the first set row in display order (so the step list, the summary and the
/// other platform's conversion show the active layout's shortcut). Off keeps the main shortcut and drops every row in one
/// edit. Then the hand editor: four modifier toggles, each with an "R" toggle for its right-hand key (<see cref="ModifierToggle"/>),
/// and a key dropdown over every <see cref="KeyCode"/> labelled by <see cref="HotkeyText"/>; with the switch off it edits the
/// main shortcut, with it on the row the main shortcut is derived from (the active layout's when listed, else the first listed),
/// so the main follows. Every edit, from any field, emits one new <see cref="HotkeyStep"/> through one
/// <see cref="StepFormState{TStep}"/>, normalised (right-hand bits only for modifiers that are on; a layout row without a key is
/// no shortcut), and refreshes the other fields; an edit that changes nothing emits nothing. The form re-declares itself when
/// its shape changes (the switch, Remove) and when the layouts port is re-published (the composition root does so on a layout
/// change, a resource change here), so the key names, the rows and which row the main follows track the active layout (decision 6).
/// </summary>
public sealed class HotkeyStepForm : IStepForm
{
    public const string CaptureHelp = "Capture, press the combination, then Accept or click anywhere else. Clear removes it.";

    public const string PerLayoutLabel = "Per keyboard layout";

    public const string PerLayoutHelp = "One shortcut per keyboard layout the OS has installed, in place of the field above: on, every layout starts with that shortcut, so only the layout that differs needs capturing again; off keeps the active layout's shortcut and drops the rest. Chrome's Back is Cmd+[ under ABC and Cmd+Opt+8 under Swedish.";

    public const string RowsNoteLabel = "Note";

    public const string RowsNote = "The step list and the other platform show the active layout's shortcut; a layout without one presses it too.";

    public const string NotInstalledHint = "Not installed now; the shortcut stays until removed.";

    public const string RemoveLabel = "Remove";

    private static readonly HotkeyBinding NoShortcut = new(KeyModifiers.None, KeyCode.None);

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
            var installed = layouts.Installed;
            var activeId = layouts.Active?.Id;
            var rows = new List<(string Id, HotkeyCaptureBox Box)>();
            var box = perLayout ? null : new HotkeyCaptureBox { Modifiers = state.Modifiers, RightHand = state.RightHand, Key = state.Key, KeyboardLayouts = layouts };
            var form = new StepFormState<HotkeyStep>(state, next => { state = next; changed(next); }, step => step.Normalized())
            {
                Accepted = next =>
                {
                    if (box is not null)
                    {
                        Set(box, next.Main);
                    }

                    foreach (var (id, row) in rows)
                    {
                        Set(row, next.Layouts.TryGetValue(id, out var own) ? own : NoShortcut);
                    }
                },
            };

            // The ids in display order: the installed layouts, then the authored ones no longer installed.
            IReadOnlyList<string> Order() => [.. installed.Select(layout => layout.Id), .. Orphans(form.Current, installed)];

            // The main shortcut re-derived from the rows (the active layout's when set, else the first set one in display order); unchanged when no row is set.
            HotkeyStep Derived(HotkeyStep step)
            {
                var order = Order();
                var pick = activeId is not null && step.Layouts.TryGetValue(activeId, out var active) && active.IsSet
                    ? active
                    : order.Select(id => step.Layouts.TryGetValue(id, out var own) ? own : null).FirstOrDefault(own => own is { IsSet: true });
                return pick is null ? step : step with { Modifiers = pick.Modifiers, Key = pick.Key, RightHand = pick.RightHand };
            }

            // A row's new shortcut, and the main shortcut following it.
            void EmitRow(string id, HotkeyBinding binding) => form.Emit(Derived(form.Current with { Layouts = With(form.Current.Layouts, id, binding) }));

            // A hand edit of the shortcut shown: the main one with the switch off; with it on, the row the main follows. The
            // shown value echoed back (the dropdown's renderer does so on every refresh) is no edit, so it writes no row.
            void EmitMain(KeyModifiers modifiers, KeyCode key, KeyModifiers rightHand)
            {
                var edited = new HotkeyBinding(modifiers, key, rightHand).Normalized();
                if (edited == form.Current.Main)
                {
                    return;
                }

                var target = perLayout ? (activeId is not null && Order().Contains(activeId, StringComparer.Ordinal) ? activeId : Order().FirstOrDefault()) : null;
                if (target is null)
                {
                    form.Emit(form.Current with { Modifiers = modifiers, Key = key, RightHand = rightHand });
                }
                else
                {
                    EmitRow(target, new HotkeyBinding(modifiers, key, rightHand));
                }
            }

            if (box is not null)
            {
                box.Committed += (_, e) => form.Emit(form.Current with { Modifiers = e.Modifiers, Key = e.Key, RightHand = e.RightHand });
            }

            void SetPerLayout(bool on)
            {
                if (on == perLayout)
                {
                    return;
                }

                perLayout = on;
                form.Emit(form.Current with { Layouts = on ? Seeded(installed, form.Current.Main) : HotkeyStep.NoLayouts });
                Show();
            }

            var hotkey = new List<Field>();
            if (box is not null)
            {
                hotkey.Add(new CustomField("Keys", () => box, Help: CaptureHelp));
            }

            hotkey.Add(new ToggleField(PerLayoutLabel, form.Bind(() => perLayout, SetPerLayout, propertyName: "PerLayout"), PerLayoutHelp));
            if (perLayout)
            {
                hotkey.AddRange(installed.SelectMany(layout => LayoutRows(form, layouts, rows, EmitRow, layout.Id, layout.Name, remove: null)));
                hotkey.AddRange(Orphans(form.Current, installed).SelectMany(id => LayoutRows(form, layouts, rows, EmitRow, id, id, remove: () =>
                {
                    form.Emit(Derived(form.Current with { Layouts = Without(form.Current.Layouts, id) }));
                    Show();
                })));
                hotkey.Add(new NoteField(RowsNoteLabel, RowsNote));
            }

            Func<Control> Modifier(KeyModifiers flag) => HotkeyChoices.ModifierToggle(
                flag,
                () => (form.Current.Modifiers, form.Current.RightHand),
                (modifiers, rightHand) => EmitMain(modifiers, form.Current.Key, rightHand),
                form.OnEdit);

            var key = form.Refreshed(() => form.Current.Key, value => EmitMain(form.Current.Modifiers, value, form.Current.RightHand), propertyName: nameof(HotkeyStep.Key));
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
    /// One layout's row: its capture field over the step's shortcut for it (empty when none), named by that layout; for a
    /// layout no longer installed (<paramref name="remove"/> given) a Remove button on the line under it, with the "Not
    /// installed now" hint.
    /// </summary>
    private static IEnumerable<Field> LayoutRows(StepFormState<HotkeyStep> form, IKeyboardLayouts layouts, List<(string Id, HotkeyCaptureBox Box)> rows, Action<string, HotkeyBinding> emit, string id, string label, Action? remove)
    {
        var binding = form.Current.Layouts.TryGetValue(id, out var own) ? own : NoShortcut;
        var box = new HotkeyCaptureBox { Modifiers = binding.Modifiers, RightHand = binding.RightHand, Key = binding.Key, LayoutId = id, KeyboardLayouts = layouts };
        box.Committed += (_, e) => emit(id, new HotkeyBinding(e.Modifiers, e.Key, e.RightHand));
        rows.Add((id, box));
        yield return new CustomField(label, () => box);
        if (remove is not null)
        {
            yield return new ButtonsField(string.Empty, [new FieldButton(RemoveLabel, remove)]) { Hint = new DelegateBinding<string>(() => NotInstalledHint) };
        }
    }

    private static void Set(HotkeyCaptureBox box, HotkeyBinding binding)
    {
        box.Modifiers = binding.Modifiers;
        box.RightHand = binding.RightHand;
        box.Key = binding.Key;
    }

    /// <summary>The layouts the step holds that are not installed now, by id in ordinal order.</summary>
    private static IEnumerable<string> Orphans(HotkeyStep step, IReadOnlyList<KeyboardLayout> installed)
        => step.Layouts.Keys.Where(id => !installed.Any(layout => layout.Id == id)).Order(StringComparer.Ordinal);

    /// <summary>Every installed layout with <paramref name="main"/> as its shortcut: what the switch seeds the rows with.</summary>
    private static IReadOnlyDictionary<string, HotkeyBinding> Seeded(IReadOnlyList<KeyboardLayout> installed, HotkeyBinding main)
        => installed.Select(layout => layout.Id).Distinct(StringComparer.Ordinal).ToFrozenDictionary(id => id, _ => main, StringComparer.Ordinal);

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
