using Augram.Core.Abstractions;

namespace Augram.Core.Steps.DisplayMode;

/// <summary>
/// Sets a display's resolution and/or refresh rate (Joel's Display Changer presets). A null <paramref name="Resolution"/>
/// or <paramref name="Refresh"/> is Auto: keep the current one, so "24 Hz" changes the rate and leaves the resolution.
/// <paramref name="HighestRefresh"/> is the third refresh choice (Joel, 2026-10-09; Display Changer's <c>-refresh=max</c>):
/// the highest rate the display offers at the resolution it ends up at; <paramref name="Refresh"/> is then null. Both
/// are targets resolved against what the display offers when the step runs (<see cref="DisplayModeResolver"/>).
/// </summary>
public sealed record DisplayModeStep(
    DisplayResolution? Resolution = null,
    RefreshRate? Refresh = null,
    DisplayTarget Target = DisplayTarget.UnderGesture,
    bool HighestRefresh = false) : IStep
{
    public IStepType Type => DisplayModeStepType.Instance;

    /// <summary>Resolution and refresh both Auto: the step has nothing to change.</summary>
    public bool ChangesNothing => Resolution is null && Refresh is null && !HighestRefresh;

    /// <summary>
    /// "Display 1920×1080 at 119.88 Hz", "Display refresh 24 Hz", "Display 3840×2160", "Display refresh highest available",
    /// with " (main display)" when targeted.
    /// </summary>
    public string Summary => Target == DisplayTarget.Main ? $"{What} (main display)" : What;

    private string What => (Resolution, Refresh, HighestRefresh) switch
    {
        ({ } size, _, true) => $"Display {size} at the highest refresh",
        (null, _, true) => "Display refresh highest available",
        ({ } size, { } rate, _) => $"Display {size} at {rate}",
        ({ } size, null, _) => $"Display {size}",
        (null, { } rate, _) => $"Display refresh {rate}",
        _ => "Display (no change)",
    };
}
