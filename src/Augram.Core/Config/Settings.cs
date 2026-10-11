using Augram.Core.Capture;
using Augram.Core.Recognition;

namespace Augram.Core.Config;

/// <summary>
/// Everything on the Options page, one record per subsystem. Immutable; a change is a
/// <c>with</c> expression committed through <see cref="SettingsStore"/>. The constructor
/// takes every section as optional so a section missing from (or null in) the JSON file
/// takes its default: the serializer binds constructor parameters, not initialisers.
/// </summary>
public sealed record Settings
{
    public Settings(
        GeneralSettings? general = null,
        CaptureThresholds? capture = null,
        TrailSettings? trail = null,
        RecognitionOptions? recognition = null,
        NoMatchBehaviour noMatch = NoMatchBehaviour.DoNothing,
        SyncSettings? sync = null,
        AppearanceSettings? appearance = null)
    {
        General = general ?? GeneralSettings.Default;
        Capture = capture ?? CaptureThresholds.Default;
        Trail = trail ?? TrailSettings.Default;
        Recognition = recognition ?? RecognitionOptions.Default;
        NoMatch = noMatch;
        Sync = sync ?? SyncSettings.Default;
        Appearance = appearance ?? AppearanceSettings.Default;
    }

    public GeneralSettings General { get; init; }

    public CaptureThresholds Capture { get; init; }

    public TrailSettings Trail { get; init; }

    public RecognitionOptions Recognition { get; init; }

    public NoMatchBehaviour NoMatch { get; init; }

    /// <summary>Machine-to-machine sync (F8): local to this machine and never synced itself, like every section here.</summary>
    public SyncSettings Sync { get; init; }

    /// <summary>Theme, window background, tint, corner rounding and accent (plan 0006): this machine's look, left out of an export like the sync section.</summary>
    public AppearanceSettings Appearance { get; init; }

    public static Settings Default { get; } = new();
}
