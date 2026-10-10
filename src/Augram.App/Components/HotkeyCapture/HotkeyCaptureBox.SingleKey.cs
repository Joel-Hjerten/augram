using Augram.Core.Abstractions;
using Augram.Core.Steps.Hotkey;
using Avalonia;

namespace Augram.App.Components.HotkeyCapture;

/// <summary>
/// The one-key mode of <see cref="HotkeyCaptureBox"/> (plan 0002 step 4: a hold remap's hold key, a hold remap command's key
/// input): the field takes one key with no modifiers and shows it alone ("Space", "W"). A key it refuses is not recorded and
/// <see cref="HotkeyCaptureBox.StatusText"/> says why: always Ctrl, Alt, Shift and Win (Cmd and Opt on a Mac), with the
/// host's reason when <see cref="KeyProblem"/> gives one (Core's rule, so the field never words a rule of its own), and any
/// other key <see cref="KeyProblem"/> refuses (an input that is the hold key). Fn never reaches the field at all.
/// </summary>
public sealed partial class HotkeyCaptureBox
{
    public const string PromptKeyText = "Press a key…";

    public static readonly StyledProperty<bool> SingleKeyProperty = AvaloniaProperty.Register<HotkeyCaptureBox, bool>(nameof(SingleKey));

    /// <summary>One key, no modifiers: a modifier is refused, and modifiers held with a key are dropped.</summary>
    public bool SingleKey { get => GetValue(SingleKeyProperty); set => SetValue(SingleKeyProperty, value); }

    /// <summary>In one-key mode, why a key cannot be taken (a sentence to show), or null when it can; from the host's rules.</summary>
    public Func<KeyCode, string?>? KeyProblem { get; set; }

    /// <summary>What the field says while it captures: which keys reach it.</summary>
    private string CaptureHelp => IsWindowOnly ? WindowOnlyText : EngineHelpText;

    /// <summary>Why one-key mode refuses <paramref name="key"/>; null to take it.</summary>
    public string? Refusal(KeyCode key)
    {
        if (!HotkeyKeys.IsModifier(key))
        {
            return KeyProblem?.Invoke(key);
        }

        return KeyProblem?.Invoke(key) ?? $"{HotkeyText.KeyName(key)} cannot be used here: this field takes one key, not {ModifierWords()}.";
    }

    /// <summary>"Ctrl, Alt, Shift or Win", in this platform's names (Opt and Cmd on a Mac).</summary>
    public static string ModifierWords()
        => $"{HotkeyText.Format(KeyModifiers.Control, KeyCode.None)}, {HotkeyText.Format(KeyModifiers.Alt, KeyCode.None)}, {HotkeyText.Format(KeyModifiers.Shift, KeyCode.None)} or {HotkeyText.Format(KeyModifiers.Meta, KeyCode.None)}";

    /// <summary>The field's text while capturing: the key alone in one-key mode, else what is held ("Ctrl+Shift+…").</summary>
    private string LiveText() => SingleKey
        ? _recorder.HasCombination ? HotkeyText.KeyName(_recorder.Key) : PromptKeyText
        : _recorder.LiveText ?? PromptText;
}
