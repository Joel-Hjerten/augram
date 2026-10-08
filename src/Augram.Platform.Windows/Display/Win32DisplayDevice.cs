namespace Augram.Platform.Windows.Display;

/// <summary>One <c>EnumDisplayDevices</c> entry: the GDI device name (<c>\\.\DISPLAY1</c>) and its <c>DISPLAY_DEVICE_*</c> state flags.</summary>
internal sealed record Win32DisplayDevice(string Name, uint StateFlags);
