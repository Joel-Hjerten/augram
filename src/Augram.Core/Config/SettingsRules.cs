using Augram.Core.Capture;

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
        Require(
            capture.ButtonDragDistancePx is >= 1 and <= CaptureThresholds.MaxButtonDragDistancePx,
            $"Button drag distance must be between 1 and {CaptureThresholds.MaxButtonDragDistancePx} px.");

        var recognition = settings.Recognition;
        Require(recognition.Precision >= MinPrecision, $"Precision must be at least {MinPrecision}.");
        Require(recognition.Threshold is >= 0 and <= 100, "Threshold must be between 0 and 100.");

        SyncSettingsRules.EnsureValid(settings.Sync);

        var appearance = settings.Appearance;
        Require(Enum.IsDefined(appearance.Theme), "Theme must be Dark, Light or System.");
        Require(Enum.IsDefined(appearance.WindowBackground), "Window background must be Frosted glass, Wallpaper tint or Solid.");
        Require(
            appearance.TintPercent is >= 0 and <= AppearanceSettings.MaxTintPercent,
            $"Tint must be between 0 and {AppearanceSettings.MaxTintPercent} %.");
        Require(
            appearance.CornerRadiusPx is >= AppearanceSettings.MinCornerRadiusPx and <= AppearanceSettings.MaxCornerRadiusPx,
            $"Corner rounding must be between {AppearanceSettings.MinCornerRadiusPx} and {AppearanceSettings.MaxCornerRadiusPx} px.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new SettingsValidationException(message);
        }
    }
}
