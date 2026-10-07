namespace Augram.Platform.MacOS.WindowSystem;

/// <summary>
/// One entry of the window server's list (<c>CGWindowListCopyWindowInfo</c>), which comes front to back.
/// <see cref="Title"/> is null unless this process may record the screen; <see cref="Layer"/> 0 is the normal window
/// layer, below it the desktop, above it panels, the Dock and the menu bar.
/// </summary>
internal sealed record MacWindowInfo(uint Id, int ProcessId, string OwnerName, string? Title, int Layer, double Alpha, MacRect Bounds);
