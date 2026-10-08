using System.Text.Json.Nodes;
using Augram.Core.Abstractions;

namespace Augram.Core.Steps.Hdr;

/// <summary>
/// The "HDR" step type: key <c>hdr</c>, category Display, platform-bound because only Windows lets an app switch HDR
/// (learnings 0002 §3). Parameters: <c>{ "action": "Toggle", "display": "UnderGesture" }</c>, an <see cref="HdrAction"/>
/// and a <see cref="DisplayTarget"/> name, both defaulting when absent. On macOS the step converts to nothing with the
/// reason; one authored on a Mac runs unchanged on Windows.
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

    public bool IsPlatformNeutral => false;

    /// <summary>Unchanged on Windows; on macOS no equivalent, said in the step's own words.</summary>
    public StepConversion Convert(IStep step, HostPlatform from, HostPlatform to)
    {
        var hdr = StepParameters.Expect<HdrStep>(step, this);
        return to == HostPlatform.MacOS && from != to
            ? StepConversion.None($"{hdr.Summary} has no macOS equivalent: macOS offers apps no way to switch HDR")
            : StepConversion.Same(step);
    }

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
