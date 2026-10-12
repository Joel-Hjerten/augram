using Augram.App.Declarations;
using Augram.Core.Abstractions;
using Augram.Core.Steps.Hotkey;
using Avalonia.Controls;

namespace Augram.App.Components.Capture;

/// <summary>
/// The key and modifier editors a step form puts beside a <see cref="HotkeyCaptureBox"/> or in its place, named as this
/// platform's keyboard names them (<see cref="HotkeyText"/>): the key dropdown, a modifier with its right-hand "R"
/// (<see cref="ModifierToggle"/>), and a row of held keys. Written once for the Hotkey, Remap and Scroll forms.
/// </summary>
public static class HotkeyChoices
{
    /// <summary>The four modifiers in press order (Ctrl, Alt, Shift, Win; <see cref="HotkeyKeys.LeftKeys"/>'s order).</summary>
    public static IReadOnlyList<KeyModifiers> Modifiers { get; } = [.. HotkeyKeys.LeftKeys(HotkeyKeys.AllModifiers).Select(HotkeyKeys.ModifierOf)];

    /// <summary>
    /// Every <see cref="KeyCode"/> by its name here, <see cref="KeyCode.None"/> as the field's empty text. Built per form, not once
    /// per type: the key names follow <see cref="HotkeyText.Names"/>, which follows <c>RunningPlatform</c> once the App sets it at startup.
    /// </summary>
    public static IReadOnlyList<Choice<KeyCode>> Keys =>
        [.. Enum.GetValues<KeyCode>().Select(key => new Choice<KeyCode>(key == KeyCode.None ? HotkeyCaptureBox.EmptyText : HotkeyText.KeyName(key), key))];

    /// <summary>
    /// A field's editor for <paramref name="flag"/>: a <see cref="ModifierToggle"/> showing <paramref name="shown"/>'s modifiers
    /// and right-hand keys, which calls <paramref name="toggled"/> with both as the user leaves them, and is refreshed through
    /// <paramref name="onEdit"/>. Built now, so its refresh is registered before any edit; the field's build hands out this one instance.
    /// </summary>
    public static Func<Control> ModifierToggle(
        KeyModifiers flag,
        Func<(KeyModifiers Modifiers, KeyModifiers RightHand)> shown,
        Action<KeyModifiers, KeyModifiers> toggled,
        Action<Action> onEdit)
    {
        ArgumentNullException.ThrowIfNull(shown);
        ArgumentNullException.ThrowIfNull(toggled);
        ArgumentNullException.ThrowIfNull(onEdit);
        var toggle = new ModifierToggle();
        void Show()
        {
            var (modifiers, rightHand) = shown();
            toggle.IsOn = (modifiers & flag) != 0;
            toggle.IsRightHand = (rightHand & flag) != 0;
        }

        Show();
        onEdit(Show);
        toggle.Toggled += (_, _) =>
        {
            var (modifiers, rightHand) = shown();
            toggled(
                toggle.IsOn ? modifiers | flag : modifiers & ~flag,
                toggle.IsRightHand ? rightHand | flag : rightHand & ~flag);
        };
        return () => toggle;
    }

    /// <summary>
    /// One check box per modifier (<see cref="Modifiers"/>), for the keys held around a press or a wheel turn: each shows whether
    /// <paramref name="held"/> has it and calls <paramref name="set"/> with it added or taken out. With <paramref name="onEdit"/>
    /// the boxes are refreshed after every edit of the form (another editor can change them).
    /// </summary>
    public static IReadOnlyList<ToggleOption> HeldKeys(Func<KeyModifiers> held, Action<KeyModifiers> set, Action<Action>? onEdit = null)
    {
        ArgumentNullException.ThrowIfNull(held);
        ArgumentNullException.ThrowIfNull(set);
        return [.. Modifiers.Select(flag =>
        {
            var binding = new DelegateBinding<bool>(() => (held() & flag) != 0, on => set(on ? held() | flag : held() & ~flag));
            onEdit?.Invoke(binding.NotifyChanged);
            return new ToggleOption(HotkeyText.Format(flag, KeyCode.None), binding);
        })];
    }
}
