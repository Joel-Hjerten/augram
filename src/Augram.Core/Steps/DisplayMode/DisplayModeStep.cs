using Augram.Core.Abstractions;

namespace Augram.Core.Steps.DisplayMode;

/// <summary>
/// Sets a display's resolution and/or refresh rate (Joel's Display Changer presets). A null <paramref name="Resolution"/>
/// or <paramref name="Refresh"/> is Auto: keep the current one, so "24 Hz" changes the rate and leaves the resolution.
/// Both are targets resolved against what the display offers when the step runs (<see cref="DisplayModeResolver"/>).
/// </summary>
public sealed record DisplayModeStep(
    DisplayResolution? Resolution = null,
    RefreshRate? Refresh = null,
    DisplayTarget Target = DisplayTarget.UnderGesture) : IStep
{
    public IStepType Type => DisplayModeStepType.Instance;

    /// <summary>Both Auto: the step has nothing to change.</summary>
    public bool ChangesNothing => Resolution is null && Refresh is null;

    /// <summary>"Display 1920×1080 at 119.88 Hz", "Display refresh 24 Hz", "Display 3840×2160", with " (main display)" when targeted.</summary>
    public string Summary => Target == DisplayTarget.Main ? $"{What} (main display)" : What;

    private string What => (Resolution, Refresh) switch
    {
        ({ } size, { } rate) => $"Display {size} at {rate}",
        ({ } size, null) => $"Display {size}",
        (null, { } rate) => $"Display refresh {rate}",
        _ => "Display (no change)",
    };
}
