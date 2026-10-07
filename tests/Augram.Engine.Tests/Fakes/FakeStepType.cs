using System.Diagnostics;
using System.Text.Json.Nodes;
using Augram.Core.Steps;

namespace Augram.Engine.Tests.Fakes;

/// <summary>
/// A step type for executor tests: declares the <see cref="StepCategory"/> the test needs (Keyboard to
/// attract the settle delay, System to prove it is not waited for), records a <see cref="Stopwatch"/>
/// timestamp per run, and answers with <see cref="Result"/> or throws <see cref="Throws"/>. One instance
/// per step so each step's runs are counted apart; <see cref="Step"/> is the step to put in a command.
/// </summary>
internal sealed class FakeStepType : IStepType
{
    private readonly object _gate = new();
    private readonly List<long> _runs = [];

    public FakeStepType(StepCategory category)
    {
        Category = category;
    }

    public string Key => "fake";

    public string DisplayName => "Fake";

    public StepCategory Category { get; }

    public bool IsPlatformNeutral => true;

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

    public FakeStep Step => new(this);

    public IStep CreateDefault() => Step;

    public IStep Read(JsonObject parameters) => Step;

    public JsonObject Write(IStep step) => [];

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
}
