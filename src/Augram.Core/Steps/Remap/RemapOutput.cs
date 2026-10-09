using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Steps.Hotkey;

namespace Augram.Core.Steps.Remap;

/// <summary>
/// What a Remap step turns its input into (F9, plan 0002): a mouse button, a key or a wheel notch, each with modifiers. A
/// closed set of records with value equality; the constructor is private. The modifiers mean different things per kind, as
/// Joel's AutoHotkey script has it: around a button's <em>down</em> only (Blender reads them when the drag starts, and
/// nothing stays held through it), held for a key's whole press (so its repeats keep them), around a wheel notch.
/// </summary>
public abstract record RemapOutput
{
    private RemapOutput()
    {
    }

    public abstract RemapOutputKind Kind { get; }

    /// <summary>The modifiers sent with the output; only the four of <see cref="HotkeyKeys.AllModifiers"/> count.</summary>
    public abstract KeyModifiers Modifiers { get; init; }

    /// <summary>False only for a key output whose key is not chosen yet: such a command claims its input and sends nothing.</summary>
    public virtual bool IsSet => true;

    /// <summary>"Ctrl + Middle", "Ctrl+Shift+Alt+R", "Num .", "Ctrl + wheel up", with the modifiers in <paramref name="names"/>' words.</summary>
    public abstract string Describe(HostPlatform names);

    private protected string WithModifiers(string what, HostPlatform names)
    {
        var keys = Modifiers & HotkeyKeys.AllModifiers;
        return keys == KeyModifiers.None ? what : $"{HotkeyText.Format(keys, KeyCode.None, names: names)} + {what}";
    }

    /// <summary>A mouse button held for as long as the input is held; <paramref name="Modifiers"/> are pressed around its down only.</summary>
    public sealed record Button(MouseButton MouseButton, KeyModifiers Modifiers = KeyModifiers.None) : RemapOutput
    {
        public override RemapOutputKind Kind => RemapOutputKind.Button;

        public override KeyModifiers Modifiers { get; init; } = Modifiers;

        public override string Describe(HostPlatform names) => WithModifiers(MouseButton.ToString(), names);
    }

    /// <summary>
    /// A key held for as long as the input is held, with <paramref name="Modifiers"/> held for the whole press;
    /// <paramref name="RightHand"/> says which of them are the right-hand key, as a Hotkey step's does.
    /// </summary>
    public sealed record Key(KeyCode KeyCode, KeyModifiers Modifiers = KeyModifiers.None, KeyModifiers RightHand = KeyModifiers.None) : RemapOutput
    {
        public override RemapOutputKind Kind => RemapOutputKind.Key;

        public override KeyModifiers Modifiers { get; init; } = Modifiers;

        public override bool IsSet => KeyCode != KeyCode.None;

        public override string Describe(HostPlatform names)
            => IsSet ? HotkeyText.Format(Modifiers & HotkeyKeys.AllModifiers, KeyCode, RightHand, names) : "(no key set)";
    }

    /// <summary>One wheel notch in <paramref name="Direction"/> (side-scrolling included), with <paramref name="Modifiers"/> held around it; for a wheel input only.</summary>
    public sealed record Wheel(ScrollDirection Direction, KeyModifiers Modifiers = KeyModifiers.None) : RemapOutput
    {
        public override RemapOutputKind Kind => RemapOutputKind.Wheel;

        public override KeyModifiers Modifiers { get; init; } = Modifiers;

        public override string Describe(HostPlatform names) => WithModifiers($"wheel {Direction.ToString().ToLowerInvariant()}", names);
    }
}
