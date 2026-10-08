using Augram.Core.Abstractions;

namespace Augram.Platform.MacOS.Display;

/// <summary>
/// The pure rules of the macOS display adapter, tested on every OS: a <see cref="MacNativeMode"/> is offered as its
/// points size ("looks like", what System Settings calls the resolution) and its rate rounded to three decimals; modes
/// the desktop cannot use are not offered; and the mode to apply for a resolved <see cref="VideoMode"/> is the HiDPI
/// one when a HiDPI and a 1x mode share size and rate (System Settings' default), else the one with the most pixels.
/// </summary>
internal static class MacModePick
{
    public static VideoMode ToMode(MacNativeMode mode)
    {
        ArgumentNullException.ThrowIfNull(mode);
        return new(new DisplayResolution(mode.Width, mode.Height), RefreshRate.FromHertz(mode.RefreshHz));
    }

    /// <summary>Each usable mode once as a <see cref="VideoMode"/>.</summary>
    public static IReadOnlyList<VideoMode> Offered(IEnumerable<MacNativeMode> modes)
    {
        ArgumentNullException.ThrowIfNull(modes);
        return [.. modes.Where(mode => mode.Usable).Select(ToMode).Distinct()];
    }

    /// <summary>The usable native mode to apply for <paramref name="wanted"/>; null when none carries it any more.</summary>
    public static MacNativeMode? Pick(IEnumerable<MacNativeMode> modes, VideoMode wanted)
    {
        ArgumentNullException.ThrowIfNull(modes);
        return modes
            .Where(mode => mode.Usable && ToMode(mode) == wanted)
            .OrderByDescending(mode => mode.IsHiDpi)
            .ThenByDescending(mode => (long)mode.PixelWidth * mode.PixelHeight)
            .FirstOrDefault();
    }
}
