using System.Text.Json.Nodes;
using Augram.Core.Steps;

namespace Augram.Core.Tests.Mapping.Support;

/// <summary>
/// The step type the mapping tests register, so nothing here depends on a real step type existing.
/// Parameters: <c>{ "text": "..." }</c>; a missing text is "default"; a non-string text is a
/// <see cref="StepFormatException"/>, which is how the tests provoke a dropped step.
/// </summary>
internal sealed class FakeStepType : IStepType
{
    private FakeStepType()
    {
    }

    public static FakeStepType Instance { get; } = new();

    /// <summary>A registry holding only this type.</summary>
    public static StepRegistry Registry { get; } = new([Instance]);

    public string Key => "fake";

    public string DisplayName => "Fake";

    public StepCategory Category => StepCategory.System;

    public bool IsPlatformNeutral => false;

    public IStep CreateDefault() => new FakeStep("default");

    public IStep Read(JsonObject parameters)
    {
        var node = parameters["text"];
        if (node is null)
        {
            return CreateDefault();
        }

        return node is JsonValue value && value.TryGetValue(out string? text)
            ? new FakeStep(text)
            : throw new StepFormatException("'text' must be a string.");
    }

    public JsonObject Write(IStep step) => new() { ["text"] = ((FakeStep)step).Text };

    public StepResult Execute(IStep step, StepExecutionContext context) => StepResult.Done;
}
