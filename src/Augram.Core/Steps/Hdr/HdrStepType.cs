using System.Text.Json.Nodes;
using Augram.Core.Abstractions;

namespace Augram.Core.Steps.Hdr;

/// <summary>
/// The "HDR" step type: key <c>hdr</c>, category Display, platform-neutral: Windows switches HDR through CCD, macOS
/// through SkyLight's private switch (learnings 0002 §2–3, plan 0014). Parameters: <c>{ "action": "Toggle", "display": "UnderGesture" }</c>, an <see cref="HdrAction"/>
/// and a <see cref="DisplayTarget"/> name, both defaulting when absent. The step converts unchanged both ways.
/// </summary>
public sealed class HdrStepType : IStepType
{
    public const string ActionMember = "action";

    public const string DisplayMember = "display";

    private HdrStepType()
    {
    }

    public static HdrStepType Instance { get; } = new();

    public string Key => "hdr";

    public string DisplayName => "HDR";

    public StepCategory Category => StepCategory.Display;

    public bool IsPlatformNeutral => true;

    /// <summary>The same on every platform: nothing to convert.</summary>
    public StepConversion Convert(IStep step, HostPlatform from, HostPlatform to) => StepConversion.Same(step);

    public IStep CreateDefault() => new HdrStep();

    public IStep Read(JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var action = StepParameters.ReadEnum<HdrAction>(parameters, ActionMember) ?? HdrAction.Toggle;
        var target = StepParameters.ReadEnum<DisplayTarget>(parameters, DisplayMember) ?? DisplayTarget.UnderGesture;
        return new HdrStep(action, target);
    }

    public JsonObject Write(IStep step)
    {
        var hdr = StepParameters.Expect<HdrStep>(step, this);
        return new JsonObject { [ActionMember] = hdr.Action.ToString(), [DisplayMember] = hdr.Target.ToString() };
    }

    public StepResult Execute(IStep step, StepExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return HdrExecutor.Execute(StepParameters.Expect<HdrStep>(step, this), context);
    }
}
