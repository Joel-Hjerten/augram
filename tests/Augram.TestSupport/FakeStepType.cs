using System.Diagnostics;
using System.Text.Json.Nodes;
using Augram.Core.Abstractions;
using Augram.Core.Steps;

namespace Augram.TestSupport;

/// <summary>
/// The step type tests register, so nothing depends on a real step type existing. Parameters: <c>{ "text": "..." }</c>; a
/// missing text is "default"; a non-string text is a <see cref="StepFormatException"/>, which is how the mapping tests
/// provoke a dropped step. <see cref="Instance"/> (System, not platform-neutral) and its <see cref="Registry"/> are the
/// mapping and sync tests'. The executor tests make one instance per step, so each step's runs are counted apart: it declares
/// the <see cref="StepCategory"/> the test needs (Keyboard to attract the settle delay, System to prove it is not waited
/// for), records a <see cref="Stopwatch"/> timestamp per run, and answers with <see cref="Result"/> or throws
/// <see cref="Throws"/>; <see cref="Step"/> is the step to put in a command.
/// </summary>
public sealed class FakeStepType : IStepType
{
    private readonly object _gate = new();
    private readonly List<long> _runs = [];

    public FakeStepType(StepCategory category = StepCategory.System, bool isPlatformNeutral = true)
    {
        Category = category;
        IsPlatformNeutral = isPlatformNeutral;
    }

    public static FakeStepType Instance { get; } = new(StepCategory.System, isPlatformNeutral: false);

    /// <summary>A registry holding only <see cref="Instance"/>.</summary>
    public static StepRegistry Registry { get; } = new([Instance]);

    public string Key => "fake";

    public string DisplayName => "Fake";

    public StepCategory Category { get; }

    public bool IsPlatformNeutral { get; }

    /// <summary>What a run on another platform than the authored one gets; null runs the step as is.</summary>
    public Func<IStep, StepConversion>? Converts { get; set; }

    public StepResult Result { get; set; } = StepResult.Done;

    public Exception? Throws { get; set; }

    /// <summary>Stopwatch timestamps, one per <see cref="Execute"/>.</summary>
    public IReadOnlyList<long> Runs
    {
        get
        {
            lock (_gate)
            {
                return [.. _runs];
            }
        }
    }

    /// <summary>This instance's step, summarised "Fake Keyboard step".</summary>
    public FakeStep Step => Make($"Fake {Category} step");

    public StepConversion Convert(IStep step, HostPlatform from, HostPlatform to) => Converts?.Invoke(step) ?? StepConversion.Same(step);

    public IStep CreateDefault() => Make("default");

    public IStep Read(JsonObject parameters)
    {
        var node = parameters["text"];
        if (node is null)
        {
            return CreateDefault();
        }

        return node is JsonValue value && value.TryGetValue(out string? text)
            ? Make(text)
            : throw new StepFormatException("'text' must be a string.");
    }

    public JsonObject Write(IStep step) => new() { ["text"] = ((FakeStep)step).Text };

    public StepResult Execute(IStep step, StepExecutionContext context)
    {
        lock (_gate)
        {
            _runs.Add(Stopwatch.GetTimestamp());
        }

        if (Throws is { } exception)
        {
            throw exception;
        }

        return Result;
    }

    private FakeStep Make(string text) => new(text, ReferenceEquals(this, Instance) ? null : this);
}
