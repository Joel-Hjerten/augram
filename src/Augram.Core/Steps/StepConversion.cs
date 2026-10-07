namespace Augram.Core.Steps;

/// <summary>
/// The step to run on a platform (F8, Joel 2026-10-07: both ways, best effort): the step itself
/// (<see cref="StepConversionKind.Unchanged"/>), a best-guess conversion (<see cref="StepConversionKind.Converted"/>),
/// or nothing, with the reason the executor logs and the UI shows ("Win+D needs a macOS version").
/// Computed at execution and display time, never stored.
/// </summary>
public sealed record StepConversion(StepConversionKind Kind, IStep? Step, string? Reason)
{
    public static StepConversion Same(IStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return new(StepConversionKind.Unchanged, step, null);
    }

    public static StepConversion To(IStep step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return new(StepConversionKind.Converted, step, null);
    }

    public static StepConversion None(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        return new(StepConversionKind.NotConvertible, null, reason);
    }
}
