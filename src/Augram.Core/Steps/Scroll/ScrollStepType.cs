using System.Text.Json.Nodes;
using Augram.Core.Abstractions;
using Augram.Core.Steps.Hotkey;

namespace Augram.Core.Steps.Scroll;

/// <summary>
/// The "Scroll" step type: key <c>scroll</c>, category Mouse (the wheel lands in the window under the gesture start, so the
/// executor activates it first, as for keys), platform-neutral: a direction and a notch count mean the same everywhere,
/// and held keys convert like a hotkey's (<see cref="HotkeyConversion.SwapModifiers"/>: Ctrl ↔ Cmd). Parameters:
/// <c>{ "direction": "Down", "notches": 3, "keys": "Control" }</c>, the direction one of <see cref="ScrollDirection"/>'s
/// names, the notches 1..20, the keys as <see cref="KeyModifiers"/> flag names separated by commas (written only when
/// there are any); all default when absent. The default instance scrolls down one notch.
/// </summary>
public sealed class ScrollStepType : IStepType
{
    public const int MinNotches = 1;

    public const int MaxNotches = 20;

    public const string DirectionMember = "direction";

    public const string NotchesMember = "notches";

    public const string KeysMember = "keys";

    private ScrollStepType()
    {
    }

    public static ScrollStepType Instance { get; } = new();

    public string Key => "scroll";

    public string DisplayName => "Scroll";

    public StepCategory Category => StepCategory.Mouse;

    public bool IsPlatformNeutral => true;

    /// <summary>Unchanged without held keys; with them, the hotkey's modifier swap, or none when the keys have no counterpart ("Win + scroll down needs a macOS version …").</summary>
    public StepConversion Convert(IStep step, HostPlatform from, HostPlatform to)
    {
        var scroll = StepParameters.Expect<ScrollStep>(step, this);
        if (from == to || scroll.HeldKeys == KeyModifiers.None)
        {
            return StepConversion.Same(step);
        }

        if (HotkeyConversion.SwapModifiers(scroll.HeldKeys, from, out var needs) is not { } keys)
        {
            return StepConversion.None($"{scroll.SummaryOn(from)} {needs}");
        }

        return keys == scroll.HeldKeys ? StepConversion.Same(step) : StepConversion.To(scroll with { Keys = keys });
    }

    public IStep CreateDefault() => new ScrollStep(ScrollDirection.Down);

    public IStep Read(JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var direction = StepParameters.ReadEnum<ScrollDirection>(parameters, DirectionMember) ?? ScrollDirection.Down;
        var notches = StepParameters.ReadInt32(parameters, NotchesMember, MinNotches, MaxNotches) ?? MinNotches;
        var keys = HotkeyStepType.ReadModifiers(parameters, KeysMember);
        return new ScrollStep(direction, notches, keys);
    }

    public JsonObject Write(IStep step)
    {
        var scroll = StepParameters.Expect<ScrollStep>(step, this);
        var parameters = new JsonObject
        {
            [DirectionMember] = scroll.Direction.ToString(),
            [NotchesMember] = scroll.NotchCount,
        };
        if (scroll.HeldKeys != KeyModifiers.None)
        {
            parameters[KeysMember] = scroll.HeldKeys.ToString();
        }

        return parameters;
    }

    public StepResult Execute(IStep step, StepExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ScrollExecutor.Execute(StepParameters.Expect<ScrollStep>(step, this), context);
    }
}
