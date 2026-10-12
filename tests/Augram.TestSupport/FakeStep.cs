using Augram.Core.Steps;

namespace Augram.TestSupport;

/// <summary>
/// The step of a <see cref="FakeStepType"/>: one string parameter (also its summary) and the type that owns it, null meaning
/// <see cref="FakeStepType.Instance"/> (the mapping tests' <c>new FakeStep("min")</c>). Enough to prove the envelope
/// round-trips and overrides resolve, and to count each executor-test step's runs apart.
/// </summary>
public sealed record FakeStep(string Text, FakeStepType? Owner = null) : IStep
{
    public IStepType Type => Owner ?? FakeStepType.Instance;

    public string Summary => Text;
}
