using Augram.Core.Steps;

namespace Augram.Core.Tests.Mapping.Support;

/// <summary>A step with one string parameter: enough to prove the envelope round-trips and overrides resolve.</summary>
internal sealed record FakeStep(string Text) : IStep
{
    public IStepType Type => FakeStepType.Instance;

    public string Summary => Text;
}
