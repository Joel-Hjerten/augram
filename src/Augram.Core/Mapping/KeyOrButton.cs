using System.Numerics;
using Augram.Core.Abstractions;
using Augram.Core.Capture;
using Augram.Core.Steps.Hotkey;

namespace Augram.Core.Mapping;

/// <summary>
/// One input of an Exclusions › While holding entry (plan 0012 decision 2): a keyboard key or a mouse button, held, never a
/// combination. A closed set of records with value equality, like <see cref="HoldRemaps.HoldInput"/>: a <see cref="Key"/> that
/// is not a modifier (S, Space, F13), a <see cref="Modifier"/> (one of Ctrl, Alt, Shift, Win/Cmd, either hand: the hook reads
/// those from the event's mask, as the global ignore keys are read), or a <see cref="Button"/>. A recorded modifier key folds
/// into its modifier (<see cref="FromKey"/>). The constructor is private: the three nested records are the whole set.
/// </summary>
public abstract record KeyOrButton
{
    private KeyOrButton()
    {
    }

    /// <summary>True for a stored form the rules keep: a key that is set and no modifier, exactly one modifier, any button.</summary>
    public abstract bool IsValid { get; }

    /// <summary>"S", "Space", "Alt" ("Opt" on a Mac), "Middle": the input alone in <paramref name="names"/>'s key names.</summary>
    public abstract string Describe(HostPlatform names);

    /// <summary>
    /// The input recorded from a key press: a modifier key (left or right) is its modifier, any other key itself; null for
    /// <see cref="KeyCode.None"/>.
    /// </summary>
    public static KeyOrButton? FromKey(KeyCode key) => key switch
    {
        KeyCode.None => null,
        KeyCode.LeftControl or KeyCode.RightControl => new Modifier(KeyModifiers.Control),
        KeyCode.LeftAlt or KeyCode.RightAlt => new Modifier(KeyModifiers.Alt),
        KeyCode.LeftShift or KeyCode.RightShift => new Modifier(KeyModifiers.Shift),
        KeyCode.LeftMeta or KeyCode.RightMeta => new Modifier(KeyModifiers.Meta),
        _ => new Key(key),
    };

    /// <summary>
    /// The input authored on <paramref name="from"/> as it reads on <paramref name="to"/> (plan 0012 decision 11), the hotkey
    /// converter's modifier swap unchanged (<c>HotkeyConversion.SwapModifiers</c>: Windows Ctrl is Cmd on a Mac and Mac Cmd is
    /// Ctrl on Windows, Alt is Option, Shift stays); keys and buttons stay as they are. No input, with the reason in
    /// <c>Needs</c>, when the modifier has no counterpart (the Windows key: "Win needs a macOS version: …").
    /// </summary>
    public (KeyOrButton? Input, string? Needs) Convert(HostPlatform from, HostPlatform to)
    {
        if (from == to || this is not Modifier modifier)
        {
            return (this, null);
        }

        return HotkeyConversion.SwapModifiers(modifier.Modifiers, from, out var needs) is { } swapped
            ? (swapped == modifier.Modifiers ? this : new Modifier(swapped), null)
            : (null, $"{Describe(from)} {needs}");
    }

    /// <summary>A key held, never a modifier key (those are <see cref="Modifier"/>s) and never <see cref="KeyCode.None"/>.</summary>
    public sealed record Key(KeyCode KeyCode) : KeyOrButton
    {
        public override bool IsValid => KeyCode != KeyCode.None && FromKey(KeyCode) is Key;

        public override string Describe(HostPlatform names) => HotkeyText.KeyName(KeyCode, names);
    }

    /// <summary>One modifier held, either hand: exactly one flag of <see cref="KeyModifiers"/>.</summary>
    public sealed record Modifier(KeyModifiers Modifiers) : KeyOrButton
    {
        public override bool IsValid => Modifiers != KeyModifiers.None && BitOperations.IsPow2((uint)Modifiers) && (Modifiers & ~(KeyModifiers.Control | KeyModifiers.Alt | KeyModifiers.Shift | KeyModifiers.Meta)) == 0;

        public override string Describe(HostPlatform names) => HotkeyText.Format(Modifiers, KeyCode.None, KeyModifiers.None, names);
    }

    /// <summary>A mouse button held.</summary>
    public sealed record Button(MouseButton MouseButton) : KeyOrButton
    {
        public override bool IsValid => Enum.IsDefined(MouseButton);

        public override string Describe(HostPlatform names) => MouseButton.ToString();
    }
}

/// <summary>
/// One row of an Exclusions › While holding entry's list as stored (plan 0012): the <see cref="Input"/> and the platform it was
/// <see cref="AuthoredOn"/>, the way a <see cref="CommandStep"/> is stored, so the list keeps its sides by the same rules
/// (<see cref="PlatformSides"/>, <see cref="PlatformList{T}"/>).
/// </summary>
public sealed record WhileHoldingInput(KeyOrButton Input, HostPlatform AuthoredOn) : IPlatformItem<WhileHoldingInput>
{
    /// <summary>What counts on <paramref name="platform"/>: the input itself where it was authored, else its conversion; null when it has none there.</summary>
    public KeyOrButton? ForPlatform(HostPlatform platform) => Input.Convert(AuthoredOn, platform).Input;

    /// <summary>Why the input counts for nothing on <paramref name="platform"/> ("Win needs a macOS version: …"); null when it counts.</summary>
    public string? NeedsOn(HostPlatform platform) => Input.Convert(AuthoredOn, platform).Needs;

    /// <inheritdoc/>
    public WhileHoldingInput? ConvertedTo(HostPlatform platform) => ForPlatform(platform) is { } input ? new WhileHoldingInput(input, platform) : null;
}

/// <summary>
/// The inputs of the While holding entries over one window for one platform (plan 0012 decisions 3 and 9: several entries add
/// up), in the form the hook reads at a press: the <see cref="Keys"/> (no modifiers), the <see cref="Modifiers"/> as a mask and
/// the <see cref="Buttons"/> as a set. Immutable; built off the hook thread by <see cref="IgnoreList.HeldInputsUnder"/>.
/// </summary>
public sealed record WhileHoldingSet(IReadOnlyList<KeyCode> Keys, KeyModifiers Modifiers, HeldButtons Buttons)
{
    public static WhileHoldingSet None { get; } = new([], KeyModifiers.None, HeldButtons.None);

    public bool IsEmpty => Keys.Count == 0 && Modifiers == KeyModifiers.None && Buttons == HeldButtons.None;

    /// <summary>The set of <paramref name="inputs"/>, each key once in their order; <see cref="None"/> for none.</summary>
    public static WhileHoldingSet Of(IEnumerable<KeyOrButton> inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        List<KeyCode>? keys = null;
        var modifiers = KeyModifiers.None;
        var buttons = HeldButtons.None;
        foreach (var input in inputs)
        {
            switch (input)
            {
                case KeyOrButton.Key key when !(keys ??= []).Contains(key.KeyCode):
                    keys.Add(key.KeyCode);
                    break;
                case KeyOrButton.Modifier modifier:
                    modifiers |= modifier.Modifiers;
                    break;
                case KeyOrButton.Button button:
                    buttons |= button.MouseButton.Flag();
                    break;
            }
        }

        return keys is null && modifiers == KeyModifiers.None && buttons == HeldButtons.None ? None : new WhileHoldingSet(keys ?? [], modifiers, buttons);
    }
}
