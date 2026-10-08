namespace Augram.Platform.Windows.Display;

/// <summary>
/// One active CCD path: the GDI source it shows (<c>\\.\DISPLAY1</c>), the monitor's friendly name (empty when the
/// monitor gives none), the target's adapter (a packed <c>LUID</c>) and id for the HDR calls, and the raw advanced
/// colour values: <paramref name="AdvancedColorInfo2"/> from <c>GET_ADVANCED_COLOR_INFO_2</c> (null before Windows 11
/// 24H2) and <paramref name="AdvancedColorInfo"/> from <c>GET_ADVANCED_COLOR_INFO</c> (null when refused).
/// </summary>
internal sealed record Win32DisplayTarget(
    string SourceName,
    string FriendlyName,
    long AdapterId,
    uint TargetId,
    uint? AdvancedColorInfo2,
    uint? AdvancedColorInfo);
