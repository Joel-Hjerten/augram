/// <summary>Every size the tool writes, in one place. <c>IconAssetsTests</c> in App.Tests pins the same numbers.</summary>
internal static class IconSizes
{
    /// <summary>Windows .exe icon entries (PNG-compressed, Vista and later); 20 and 40 are the 125 % sizes of 16 and 32.</summary>
    public static readonly int[] Ico = [16, 20, 24, 32, 40, 48, 64, 128, 256];

    /// <summary>
    /// macOS .icns entries that take PNG data, as Eyeris measured them (2026-10-07): NOT icp4/icp5/icp6 (16/32/48 px),
    /// which macOS 26 decodes as raw pixels and shows as noise; it scales the small sizes from the @2x entries instead.
    /// </summary>
    public static readonly (string Type, int Size)[] Icns =
    [
        ("ic07", 128), ("ic08", 256), ("ic09", 512), ("ic10", 1024),
        ("ic11", 32), ("ic12", 64), ("ic13", 256), ("ic14", 512),
    ];

    /// <summary>The Windows tray asset: drawn at 16 px per 100 % scaling (20 at 125 %, 32 at 200 %), so 32 scales down cleanly.</summary>
    public const int Tray = 32;

    /// <summary>The macOS menu-bar template: 18 pt, written at @2x (36 px); Avalonia hands it to the menu bar as one image.</summary>
    public const int MacTray = 36;
}
