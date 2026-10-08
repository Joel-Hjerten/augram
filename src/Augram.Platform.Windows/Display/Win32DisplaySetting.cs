namespace Augram.Platform.Windows.Display;

/// <summary>
/// The parts of a <c>DEVMODEW</c> the display adapter reads and writes: size in physical pixels, Windows' whole-hertz
/// <paramref name="Frequency"/>, colour depth, interlacing, the fixed-output variant (default, stretch, center; only
/// meaningful when <paramref name="HasFixedOutput"/>) and, for the current settings, the position on the desktop.
/// </summary>
internal sealed record Win32DisplaySetting(
    int Width,
    int Height,
    int Frequency,
    int BitsPerPixel = 32,
    bool Interlaced = false,
    int FixedOutput = 0,
    bool HasFixedOutput = false,
    int X = 0,
    int Y = 0);
