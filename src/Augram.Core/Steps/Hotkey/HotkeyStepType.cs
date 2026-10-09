using System.Text.Json.Nodes;
using Augram.Core.Abstractions;

namespace Augram.Core.Steps.Hotkey;

/// <summary>
/// The "Hotkey" step type: key <c>hotkey</c>, category Keyboard, platform-bound (F8: a hotkey's
/// modifiers differ per platform). Parameters: <c>{ "modifiers": "Control, Shift", "rightHand": "Shift", "key": "T" }</c>,
/// the modifiers as <see cref="KeyModifiers"/> flag names separated by commas ("None" when there are
/// none), <c>rightHand</c> the same way for those pressed with the right-hand key (written only when
/// there are any; read cut down to <c>modifiers</c>), and the key as a <see cref="KeyCode"/> name; all
/// default when absent. The default instance is <see cref="HotkeyStep.Unset"/>, which runs as Skipped "no key set".
/// </summary>
public sealed class HotkeyStepType : IStepType
{
    public const string ModifiersMember = "modifiers";

    public const string RightHandMember = "rightHand";

    public const string KeyMember = "key";

    private static readonly string[] ModifierNames =
        [.. Enum.GetNames<KeyModifiers>().Where(name => name != nameof(KeyModifiers.None))];

    private HotkeyStepType()
    {
    }

    public static HotkeyStepType Instance { get; } = new();

    public string Key => "hotkey";

    public string DisplayName => "Hotkey";

    public StepCategory Category => StepCategory.Keyboard;

    public bool IsPlatformNeutral => false;

    /// <summary>Ctrl ↔ Cmd, Alt ↔ Option and the exception table (<see cref="HotkeyConversion"/>).</summary>
    public StepConversion Convert(IStep step, HostPlatform from, HostPlatform to)
        => HotkeyConversion.Convert(StepParameters.Expect<HotkeyStep>(step, this), from, to);

    public IStep CreateDefault() => HotkeyStep.Unset;

    public IStep Read(JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var modifiers = ReadModifiers(parameters, ModifiersMember);
        var rightHand = ReadModifiers(parameters, RightHandMember);
        var key = StepParameters.ReadEnum<KeyCode>(parameters, KeyMember) ?? KeyCode.None;
        return new HotkeyStep(modifiers, key, rightHand).Normalized();
    }

    public JsonObject Write(IStep step)
    {
        var hotkey = StepParameters.Expect<HotkeyStep>(step, this).Normalized();
        var parameters = new JsonObject { [ModifiersMember] = hotkey.Modifiers.ToString() };
        if (hotkey.RightHand != KeyModifiers.None)
        {
            parameters[RightHandMember] = hotkey.RightHand.ToString();
        }

        parameters[KeyMember] = hotkey.Key.ToString();
        return parameters;
    }

    public StepResult Execute(IStep step, StepExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return HotkeyExecutor.Execute(StepParameters.Expect<HotkeyStep>(step, this), context);
    }

    /// <summary>
    /// Flag names, case-insensitive, comma-separated; "None", an empty string and an absent member mean none; anything else
    /// names the member. Shared with the Scroll step's held keys, so a modifier set reads the same in every step.
    /// </summary>
    internal static KeyModifiers ReadModifiers(JsonObject parameters, string member)
    {
        var text = StepParameters.ReadString(parameters, member);
        if (text is null)
        {
            return KeyModifiers.None;
        }

        var modifiers = KeyModifiers.None;
        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(part, nameof(KeyModifiers.None), StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var name = ModifierNames.FirstOrDefault(candidate => string.Equals(candidate, part, StringComparison.OrdinalIgnoreCase))
                ?? throw new StepFormatException($"'{member}' must list {string.Join(", ", ModifierNames)} separated by commas, or None; got '{text}'.");
            modifiers |= Enum.Parse<KeyModifiers>(name);
        }

        return modifiers;
    }
}
