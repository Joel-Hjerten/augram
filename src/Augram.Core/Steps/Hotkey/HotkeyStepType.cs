using System.Collections.Frozen;
using System.Text.Json.Nodes;
using Augram.Core.Abstractions;

namespace Augram.Core.Steps.Hotkey;

/// <summary>
/// The "Hotkey" step type: key <c>hotkey</c>, category Keyboard, platform-bound (F8: a hotkey's
/// modifiers differ per platform). Parameters: <c>{ "modifiers": "Control, Shift", "rightHand": "Shift", "key": "T" }</c>,
/// the modifiers as <see cref="KeyModifiers"/> flag names separated by commas ("None" when there are
/// none), <c>rightHand</c> the same way for those pressed with the right-hand key (written only when
/// there are any; read cut down to <c>modifiers</c>), and the key as a <see cref="KeyCode"/> name; all
/// default when absent. <c>layouts</c> (plan 0011) is an object keyed by layout id, each value a hotkey in the same
/// three members, written only when the step has at least one. The default instance is <see cref="HotkeyStep.Unset"/>,
/// which runs as Skipped "no key set".
/// </summary>
public sealed class HotkeyStepType : IStepType
{
    public const string ModifiersMember = "modifiers";

    public const string RightHandMember = "rightHand";

    public const string KeyMember = "key";

    public const string LayoutsMember = "layouts";

    private HotkeyStepType()
    {
    }

    public static HotkeyStepType Instance { get; } = new();

    public string Key => "hotkey";

    public string DisplayName => "Hotkey";

    public StepCategory Category => StepCategory.Keyboard;

    public bool IsPlatformNeutral => false;

    /// <summary>Ctrl ↔ Cmd, Alt ↔ Option and the exception table (<see cref="HotkeyConversion"/>); the layout shortcuts are dropped (plan 0011 decision 5).</summary>
    public StepConversion Convert(IStep step, HostPlatform from, HostPlatform to)
        => HotkeyConversion.Convert(StepParameters.Expect<HotkeyStep>(step, this), from, to);

    public IStep CreateDefault() => HotkeyStep.Unset;

    public IStep Read(JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var main = ReadBinding(parameters);
        return new HotkeyStep(main.Modifiers, main.Key, main.RightHand) { Layouts = ReadLayouts(parameters) }.Normalized();
    }

    public JsonObject Write(IStep step)
    {
        var hotkey = StepParameters.Expect<HotkeyStep>(step, this).Normalized();
        var parameters = WriteBinding(hotkey.Main, new JsonObject());
        if (hotkey.HasLayouts)
        {
            var layouts = new JsonObject();
            foreach (var (id, binding) in hotkey.Layouts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                layouts[id] = WriteBinding(binding, new JsonObject());
            }

            parameters[LayoutsMember] = layouts;
        }

        return parameters;
    }

    public StepResult Execute(IStep step, StepExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return HotkeyExecutor.Execute(StepParameters.Expect<HotkeyStep>(step, this), context);
    }

    /// <summary>The three members of one shortcut, the main one's and each layout's alike; not yet normalised.</summary>
    private static HotkeyBinding ReadBinding(JsonObject parameters)
    {
        var modifiers = StepParameters.ReadFlags<KeyModifiers>(parameters, ModifiersMember);
        var rightHand = StepParameters.ReadFlags<KeyModifiers>(parameters, RightHandMember);
        var key = StepParameters.ReadEnum<KeyCode>(parameters, KeyMember) ?? KeyCode.None;
        return new HotkeyBinding(modifiers, key, rightHand);
    }

    /// <summary><c>modifiers</c>, <c>rightHand</c> only when any, then <c>key</c>, into <paramref name="target"/>; the order every file has had.</summary>
    private static JsonObject WriteBinding(HotkeyBinding binding, JsonObject target)
    {
        target[ModifiersMember] = binding.Modifiers.ToString();
        if (binding.RightHand != KeyModifiers.None)
        {
            target[RightHandMember] = binding.RightHand.ToString();
        }

        target[KeyMember] = binding.Key.ToString();
        return target;
    }

    /// <summary>
    /// <c>layouts</c>: absent or null is none; anything but an object, an empty layout id, a value that is not an object, or
    /// a member a shortcut refuses is a <see cref="StepFormatException"/> naming <c>layouts</c> (and the entry).
    /// </summary>
    private static IReadOnlyDictionary<string, HotkeyBinding> ReadLayouts(JsonObject parameters)
    {
        var node = parameters[LayoutsMember];
        if (node is null)
        {
            return HotkeyStep.NoLayouts;
        }

        if (node is not JsonObject layouts)
        {
            throw new StepFormatException($"'{LayoutsMember}' must be an object of layout id to hotkey.");
        }

        var map = new Dictionary<string, HotkeyBinding>(StringComparer.Ordinal);
        foreach (var (id, value) in layouts)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new StepFormatException($"'{LayoutsMember}' holds an empty layout id.");
            }

            if (value is not JsonObject binding)
            {
                throw new StepFormatException($"'{LayoutsMember}' entry '{id}' must be an object with {ModifiersMember}, {RightHandMember} and {KeyMember}.");
            }

            try
            {
                map[id] = ReadBinding(binding);
            }
            catch (StepFormatException ex)
            {
                throw new StepFormatException($"'{LayoutsMember}' entry '{id}': {ex.Message}", ex);
            }
        }

        return map.ToFrozenDictionary(StringComparer.Ordinal);
    }
}
