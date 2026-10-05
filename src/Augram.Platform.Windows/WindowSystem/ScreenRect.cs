namespace Augram.Platform.Windows.WindowSystem;

/// <summary>A window or monitor rectangle in physical screen pixels, edges exclusive on the right and bottom.</summary>
internal readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom);
