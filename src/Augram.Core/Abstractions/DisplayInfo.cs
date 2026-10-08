namespace Augram.Core.Abstractions;

/// <summary>
/// One active display as <see cref="IDisplayModes.Displays"/> reads it, fresh on every call. <see cref="Id"/> is the
/// adapter's handle for it while it stays connected (<c>\\.\DISPLAY1</c> on Windows, the <c>CGDirectDisplayID</c> on
/// macOS); <see cref="Name"/> is for people and logs ("SONY TV", "Built-in display"). <see cref="Modes"/> holds every
/// mode the display offers, each once, in no promised order; <see cref="Current"/> need not be among them.
/// </summary>
public sealed record DisplayInfo(
    string Id,
    string Name,
    DisplayBounds Bounds,
    bool IsMain,
    VideoMode Current,
    IReadOnlyList<VideoMode> Modes,
    HdrState Hdr = HdrState.Unsupported);
