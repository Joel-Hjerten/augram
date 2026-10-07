namespace Augram.Core.Steps;

public enum StepOutcome
{
    /// <summary>The step ran.</summary>
    Done,

    /// <summary>The step was not run on purpose (not supported on this platform, no target window, inactive) and the command continues.</summary>
    Skipped,

    /// <summary>The step tried and failed; the command stops here.</summary>
    Failed,
}
