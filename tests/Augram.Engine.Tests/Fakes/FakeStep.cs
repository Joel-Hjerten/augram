using Augram.Core.Steps;

namespace Augram.Engine.Tests.Fakes;

/// <summary>The step of a <see cref="FakeStepType"/>; carries nothing but its owner.</summary>
internal sealed record FakeStep(FakeStepType Owner) : IStep
{
    public IStepType Type => Owner;

    public string Summary => $"Fake {Owner.Category} step";
}
