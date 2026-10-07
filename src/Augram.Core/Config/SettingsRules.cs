namespace Augram.Core.Config;

/// <summary>
/// Range rules for the Options page values (ADR-0002 §5a: one home). The form displays the
/// message; it never re-implements the check.
/// </summary>
public static class SettingsRules
{
    public const double MaxTrailWidthPx = 100;
    public const int MinPrecision = 2;

    /// <exception cref="SettingsValidationException">A value is out of range.</exception>
    public static void EnsureValid(Settings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var trail = settings.Trail;
        Require(trail.WidthPx > 0 && trail.WidthPx <= MaxTrailWidthPx, $"Trail width must be between 0 and {MaxTrailWidthPx} px.");
        Require(trail.Opacity is >= 0 and <= 1, "Trail opacity must be between 0 and 1.");

        var capture = settings.Capture;
        Require(capture.StartDistancePx >= 1, "Start distance must be at least 1 px.");
        Require(capture.MinSegmentPx >= 1, "Minimum segment must be at least 1 px.");
        Require(capture.CancelDelayMs >= 0, "Cancel delay cannot be negative.");

        var recognition = settings.Recognition;
        Require(recognition.Precision >= MinPrecision, $"Precision must be at least {MinPrecision}.");
        Require(recognition.Threshold is >= 0 and <= 100, "Threshold must be between 0 and 100.");

        SyncSettingsRules.EnsureValid(settings.Sync);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new SettingsValidationException(message);
        }
    }
}
