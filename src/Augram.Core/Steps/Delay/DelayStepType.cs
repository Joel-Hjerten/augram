using System.Text.Json.Nodes;
using Augram.Core.Abstractions;

namespace Augram.Core.Steps.Delay;

/// <summary>
/// The "Delay" step type: key <c>delay</c>, category Timing, platform-neutral. Parameters:
/// <c>{ "milliseconds": 30 }</c>, 0..60000; the default instance waits 30 ms (the A8 settle figure,
/// a sensible first value for a hand-inserted pause).
/// </summary>
public sealed class DelayStepType : IStepType
{
    public const int MinMilliseconds = 0;

    public const int MaxMilliseconds = 60_000;

    public const int DefaultMilliseconds = 30;

    private DelayStepType()
    {
    }

    public static DelayStepType Instance { get; } = new();

    public string Key => "delay";

    public string DisplayName => "Delay";

    public StepCategory Category => StepCategory.Timing;

    public bool IsPlatformNeutral => true;

    /// <summary>The same on every platform: nothing to convert.</summary>
    public StepConversion Convert(IStep step, HostPlatform from, HostPlatform to) => StepConversion.Same(step);

    public IStep CreateDefault() => new DelayStep(DefaultMilliseconds);

    public IStep Read(JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var milliseconds = StepParameters.ReadInt32(parameters, "milliseconds", MinMilliseconds, MaxMilliseconds) ?? DefaultMilliseconds;
        return new DelayStep(milliseconds);
    }

    public JsonObject Write(IStep step)
    {
        var delay = StepParameters.Expect<DelayStep>(step, this);
        return new JsonObject { ["milliseconds"] = delay.Milliseconds };
    }

    public StepResult Execute(IStep step, StepExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return DelayExecutor.Execute(StepParameters.Expect<DelayStep>(step, this), context);
    }
}
