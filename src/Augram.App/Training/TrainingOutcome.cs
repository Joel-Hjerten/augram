namespace Augram.App.Training;

/// <summary>How a training session ended; carried by <see cref="TrainingSession.Ended"/>.</summary>
public enum TrainingOutcome
{
    /// <summary>Cancel, Escape or the window's close button: nothing was stored.</summary>
    Cancelled,

    /// <summary>Accept: the gesture was added, or the sample appended, in the library.</summary>
    Accepted,
}
