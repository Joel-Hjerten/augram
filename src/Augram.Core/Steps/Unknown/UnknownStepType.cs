using System.Text.Json.Nodes;
using Augram.Core.Abstractions;
using Augram.Core.Diagnostics;

namespace Augram.Core.Steps.Unknown;

/// <summary>
/// The "kept as is" placeholder type for <see cref="UnknownStep"/>: key <c>unknown</c>, category
/// <see cref="StepCategory.Other"/> so the picker never offers it. It only ever enters a command through
/// <c>CommandStepJsonReader</c>; the file never holds the key <c>unknown</c>, because the writer uses
/// <see cref="IStep.StoredKey"/> (the original type's key) and <see cref="Write"/> returns the original parameters.
/// Nothing to convert; running it skips with the reason.
/// </summary>
public sealed class UnknownStepType : IStepType
{
    private UnknownStepType()
    {
    }

    public static UnknownStepType Instance { get; } = new();

    public string Key => "unknown";

    public string DisplayName => "Needs a newer Augram";

    public StepCategory Category => StepCategory.Other;

    public bool IsPlatformNeutral => true;

    public StepConversion Convert(IStep step, HostPlatform from, HostPlatform to) => StepConversion.Same(step);

    public IStep CreateDefault() => Keep(Key, [], $"unknown step type '{Key}'");

    /// <summary>Only reached for a file that says <c>"type": "unknown"</c>, which no Augram writes: kept like any other.</summary>
    public IStep Read(JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        return Keep(Key, parameters, $"unknown step type '{Key}'");
    }

    public JsonObject Write(IStep step)
        => JsonNode.Parse(StepParameters.Expect<UnknownStep>(step, this).ParametersJson) as JsonObject ?? [];

    public StepResult Execute(IStep step, StepExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var unknown = StepParameters.Expect<UnknownStep>(step, this);
        context.Log.Info("steps", "Unknown step skipped", ("type", unknown.StoredTypeKey), ("reason", unknown.Reason));
        return StepResult.Skipped($"this Augram cannot run it ({unknown.Reason}); update Augram");
    }

    /// <summary>The step as read: <paramref name="typeKey"/> and a copy of <paramref name="parameters"/>, with why it could not be read.</summary>
    public static UnknownStep Keep(string typeKey, JsonObject parameters, string reason)
    {
        ArgumentNullException.ThrowIfNull(typeKey);
        ArgumentNullException.ThrowIfNull(parameters);
        return new UnknownStep(typeKey, parameters.ToJsonString(), reason);
    }
}
