using System.Text.Json.Nodes;
using Augram.Core.Abstractions;

namespace Augram.Core.Steps.WindowOp;

/// <summary>
/// The "Window" step type: key <c>windowOp</c>, platform-neutral because <see cref="WindowOperation"/>
/// is semantic and each platform adapter maps or declines it (README in this folder has the table).
/// Parameters: <c>{ "operation": "Minimize" }</c>, plus <c>"width"</c> and <c>"height"</c> (1..32767,
/// both required) when the operation is <see cref="WindowOperation.SetSize"/>.
/// </summary>
public sealed class WindowOpStepType : IStepType
{
    public const int MinSizePx = 1;

    public const int MaxSizePx = 32767;

    private WindowOpStepType()
    {
    }

    public static WindowOpStepType Instance { get; } = new();

    public string Key => "windowOp";

    public string DisplayName => "Window";

    public StepCategory Category => StepCategory.System;

    public bool IsPlatformNeutral => true;

    /// <summary>The same on every platform: nothing to convert.</summary>
    public StepConversion Convert(IStep step, HostPlatform from, HostPlatform to) => StepConversion.Same(step);

    public IStep CreateDefault() => new WindowOpStep(WindowOperation.Minimize);

    public IStep Read(JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var operation = StepParameters.ReadEnum<WindowOperation>(parameters, "operation") ?? WindowOperation.Minimize;
        if (operation != WindowOperation.SetSize)
        {
            return new WindowOpStep(operation);
        }

        var width = StepParameters.ReadInt32(parameters, "width", MinSizePx, MaxSizePx)
            ?? throw StepParameters.Required("width", "'operation' is SetSize");
        var height = StepParameters.ReadInt32(parameters, "height", MinSizePx, MaxSizePx)
            ?? throw StepParameters.Required("height", "'operation' is SetSize");
        return new WindowOpStep(operation, new WindowSize(width, height));
    }

    public JsonObject Write(IStep step)
    {
        var windowOp = StepParameters.Expect<WindowOpStep>(step, this);
        var parameters = new JsonObject { ["operation"] = windowOp.Operation.ToString() };
        if (windowOp.Operation == WindowOperation.SetSize && windowOp.Size is { } size)
        {
            parameters["width"] = size.Width;
            parameters["height"] = size.Height;
        }

        return parameters;
    }

    public StepResult Execute(IStep step, StepExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return WindowOpExecutor.Execute(StepParameters.Expect<WindowOpStep>(step, this), context);
    }
}
