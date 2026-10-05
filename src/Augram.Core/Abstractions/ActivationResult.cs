namespace Augram.Core.Abstractions;

/// <summary>
/// Outcome of <see cref="IWindowSystem.Activate"/>. <see cref="Technique"/> names what worked
/// (or <c>not-needed</c>, or <c>none</c> when everything failed) so the activation log (B1) can tally it.
/// </summary>
public sealed record ActivationResult(bool Succeeded, string Technique, int ElapsedMs)
{
    public static ActivationResult NotNeeded { get; } = new(true, "not-needed", 0);
}
