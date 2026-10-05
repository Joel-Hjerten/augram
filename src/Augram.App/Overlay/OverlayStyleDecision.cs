namespace Augram.App.Overlay;

/// <summary>What <see cref="OverlayStylePolicy"/> allows for the overlay window given the OS's style report.</summary>
public enum OverlayStyleDecision
{
    /// <summary>The window may cover the screen.</summary>
    Show,

    /// <summary>The window would swallow input; keep it hidden and make the trail a no-op.</summary>
    Hide,
}
