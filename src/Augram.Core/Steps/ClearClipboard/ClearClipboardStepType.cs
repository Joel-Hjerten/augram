using System.Text.Json.Nodes;
using Augram.Core.Abstractions;

namespace Augram.Core.Steps.ClearClipboard;

/// <summary>
/// The "Clear clipboard" step type: key <c>clearClipboard</c>, category System, platform-neutral, no parameters
/// (<c>{}</c>; members a later version may add are ignored on read). Runs through <see cref="StepExecutionContext.Clipboard"/>.
/// </summary>
public sealed class ClearClipboardStepType : IStepType
{
    private ClearClipboardStepType()
    {
    }

    public static ClearClipboardStepType Instance { get; } = new();

    public string Key => "clearClipboard";

    public string DisplayName => ClearClipboardStep.Text;

    public StepCategory Category => StepCategory.System;

    public bool IsPlatformNeutral => true;

    /// <summary>The same on every platform: nothing to convert.</summary>
    public StepConversion Convert(IStep step, HostPlatform from, HostPlatform to) => StepConversion.Same(step);

    public IStep CreateDefault() => new ClearClipboardStep();

    public IStep Read(JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return new ClearClipboardStep();
    }

    public JsonObject Write(IStep step)
    {
        StepParameters.Expect<ClearClipboardStep>(step, this);
        return [];
    }

    public StepResult Execute(IStep step, StepExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return ClearClipboardExecutor.Execute(StepParameters.Expect<ClearClipboardStep>(step, this), context);
    }
}
