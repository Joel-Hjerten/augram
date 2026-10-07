namespace Augram.Core.Steps.Delay;

/// <summary>
/// Waits <paramref name="Milliseconds"/> before the next step (F5: keystroke sequences with delays;
/// Joel's SP.net config has 24 of these). Not the settle delay of A8: that one the executor inserts
/// itself after moving focus, and this step never stands in for it.
/// </summary>
public sealed record DelayStep(int Milliseconds) : IStep
{
    public IStepType Type => DelayStepType.Instance;

    public string Summary => $"Wait {Milliseconds} ms";
}
