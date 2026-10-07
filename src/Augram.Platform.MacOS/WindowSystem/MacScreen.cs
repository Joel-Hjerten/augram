namespace Augram.Platform.MacOS.WindowSystem;

/// <summary>
/// One display as AppKit sees it, in global top-left points. <see cref="Visible"/> is <c>NSScreen.visibleFrame</c>:
/// the frame without the menu bar and the Dock, the counterpart of the Windows work area.
/// </summary>
internal sealed record MacScreen(MacRect Frame, MacRect Visible);
